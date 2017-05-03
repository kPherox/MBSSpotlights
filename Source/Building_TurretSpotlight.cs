using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using Spotlights;

//Creator: Master Bucketsmith
//License: https://creativecommons.org/licenses/by-nc-sa/4.0/

namespace Spotlights
{
    [StaticConstructorOnStartup]
    public class Building_TurretSpotlight : Building_Turret
    {
        public static Material ForcedTargetLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath,
            ShaderDatabase.Transparent, new Color(1f, 0.5f, 0.5f));

        protected LocalTargetInfo currentTargetInt = LocalTargetInfo.Invalid;
        private Thing gunInt;
        protected TurretTop top;
        protected CompPowerTrader powerComp;
        protected CompMannable mannableComp;
        public bool loaded;
        private bool holdFire;
        protected int burstWarmupTicksLeft;
        protected int burstCooldownTicksLeft;

        public CompEquippable GunCompEq
        {
            get { return this.Gun.TryGetComp<CompEquippable>(); }
        }

        public override LocalTargetInfo CurrentTarget
        {
            get { return this.currentTargetInt; }
        }

        private bool WarmingUp
        {
            get { return this.burstWarmupTicksLeft > 0; }
        }

        public Thing Gun
        {
            get
            {
                if (this.gunInt == null)
                {
                    this.gunInt = ThingMaker.MakeThing(this.def.building.turretGunDef, (ThingDef) null);
                    for (int index = 0; index < this.GunCompEq.AllVerbs.Count; ++index)
                    {
                        Verb allVerb = this.GunCompEq.AllVerbs[index];
                        allVerb.caster = (Thing) this;
                        allVerb.castCompleteCallback = new Action(this.BurstComplete);
                    }
                }
                return this.gunInt;
            }
        }

        public override Verb AttackVerb
        {
            get { return this.GunCompEq.verbTracker.PrimaryVerb; }
        }

        private bool MannedByColonist
        {
            get
            {
                if (this.mannableComp != null && this.mannableComp.ManningPawn != null)
                    return this.mannableComp.ManningPawn.Faction == Faction.OfPlayer;
                return false;
            }
        }

        private bool CanSetForcedTarget
        {
            get { return this.MannedByColonist; }
        }

        private bool CanToggleHoldFire
        {
            get
            {
                if (this.Faction != Faction.OfPlayer)
                    return this.MannedByColonist;
                return true;
            }
        }

        public Building_TurretSpotlight()
        {
            this.top = new TurretTop((Building_Turret) this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.powerComp = this.GetComp<CompPowerTrader>();
            this.mannableComp = this.GetComp<CompMannable>();
            this.currentTargetInt = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
            this.burstCooldownTicksLeft = 0;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<bool>(ref this.loaded, "loaded", false, false);
            Scribe_Values.Look<bool>(ref this.holdFire, "holdFire", false, false);
        }

        public override void OrderAttack(LocalTargetInfo targ)
        {
            if ((double) (targ.Cell - this.Position).LengthHorizontal <
                (double) this.GunCompEq.PrimaryVerb.verbProps.minRange)
                Messages.Message("MessageTargetBelowMinimumRange".Translate(), (TargetInfo) (this),
                    MessageSound.RejectInput);
            else if ((double) (targ.Cell - this.Position).LengthHorizontal >
                     (double) this.GunCompEq.PrimaryVerb.verbProps.range)
            {
                Messages.Message("MessageTargetBeyondMaximumRange".Translate(), (TargetInfo) (this),
                    MessageSound.RejectInput);
            }
            else
            {
                if (!(this.forcedTarget != targ))
                    return;
                this.forcedTarget = targ;
                if (this.burstCooldownTicksLeft > 0)
                    return;
                this.TryStartShootSomething();
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (this.powerComp != null && !this.powerComp.PowerOn ||
                this.mannableComp != null && !this.mannableComp.MannedNow)
                return;
            if (!this.CanSetForcedTarget && this.forcedTarget.IsValid)
                this.ResetForcedTarget();
            if (!this.CanToggleHoldFire)
                this.holdFire = false;
            this.GunCompEq.verbTracker.VerbsTick();
            if (this.stunner.Stunned || this.GunCompEq.PrimaryVerb.state == VerbState.Bursting)
                return;
            if (this.WarmingUp)
            {
                --this.burstWarmupTicksLeft;
//                if (this.burstWarmupTicksLeft == 0)
//                {
//                    this.BeginBurst();
//                }
            }
            else
            {
                if (this.burstCooldownTicksLeft > 0)
                    --this.burstCooldownTicksLeft;
                if (this.burstCooldownTicksLeft == 0)
                {
                    this.TryStartShootSomething();
                }
            }
            this.top.TurretTopTick();
        }

        protected void TryStartShootSomething()
        {
            if (this.forcedTarget.ThingDestroyed)
                this.forcedTarget = null;
            if (this.holdFire && this.CanToggleHoldFire || this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead && Find.VisibleMap.roofGrid.Roofed(this.Position))
                return;
            bool isValid = this.currentTargetInt.IsValid;
            this.currentTargetInt = !this.forcedTarget.IsValid ? (LocalTargetInfo)this.TryFindNewTarget() : this.forcedTarget;
            if (!isValid && this.currentTargetInt.IsValid)
            {
                SoundDef.Named("SpotlightOn").PlayOneShot(new TargetInfo(Position, Map, false));
            }
            if (!this.currentTargetInt.IsValid)
                return;
            if (this.def.building.turretBurstWarmupTime > 0)
                this.burstWarmupTicksLeft = this.def.building.turretBurstWarmupTime.SecondsToTicks();
            else
            {
//                Log.Message("MBS Pew Pew TryStartShootSomething");
//                this.BeginBurst();
            }
        }

        protected TargetInfo TryFindNewTarget()
        {
            IAttackTargetSearcher attackTargetSearcher = this.TargSearcher();
            Faction faction = attackTargetSearcher.Thing.Faction;
            float range = this.GunCompEq.PrimaryVerb.verbProps.range;
            float minRange = this.GunCompEq.PrimaryVerb.verbProps.minRange;
            Building t;
            if (Rand.Value < 0.5f && this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead && faction.HostileTo(Faction.OfPlayer) && Find.VisibleMap.listerBuildings.allBuildingsColonist.Where(delegate (Building x)
                {
                    float num = x.Position.DistanceToSquared(this.Position);
                    return num > minRange*minRange && num < range*range;
                }).TryRandomElement(out t))
            {
                return t;
            }
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat;
            if (!this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead)
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToAll;
            }
            if (this.GunCompEq.PrimaryVerb.verbProps.ai_IsIncendiary)
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }
            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, new Predicate<Thing>(this.IsValidTarget),
                range, minRange, targetScanFlags);
        }

        private IAttackTargetSearcher TargSearcher()
        {
            if (this.mannableComp != null && this.mannableComp.MannedNow)
            {
                return this.mannableComp.ManningPawn;
            }
            return this;
        }

        private bool IsValidTarget(Thing t)
        {
            Pawn p = t as Pawn;
            if (p != null)
            {
                if (this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead)
                {
                    RoofDef roofDef = Find.VisibleMap.roofGrid.RoofAt(t.Position);
                    if (roofDef != null && roofDef.isThickRoof)
                        return false;
                }
                if (this.mannableComp == null)
                    return !GenAI.MachinesLike(this.Faction, p);
                if (p.RaceProps.Animal && p.Faction == Faction.OfPlayer)
                    return false;
            }
            return true;
        }

//        protected void BeginBurst()
//        {
//            this.GunCompEq.PrimaryVerb.TryStartCastOn(this.CurrentTarget, false, true);
//        }

        protected void BurstComplete()
        {
            this.burstCooldownTicksLeft = this.def.building.turretBurstCooldownTime.SecondsToTicks() > 0
                ? this.GunCompEq.PrimaryVerb.verbProps.defaultCooldownTime.SecondsToTicks()
                : this.def.building.turretBurstCooldownTime.SecondsToTicks();
            this.loaded = false;
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            string inspectString = base.GetInspectString();
            if (!inspectString.NullOrEmpty())
                stringBuilder.AppendLine(inspectString);
            stringBuilder.AppendLine("GunInstalled".Translate() + ": " + this.Gun.Label);
            if ((double) this.GunCompEq.PrimaryVerb.verbProps.minRange > 0.0)
                stringBuilder.AppendLine("MinimumRange".Translate() + ": " +
                                         this.GunCompEq.PrimaryVerb.verbProps.minRange.ToString("F0"));
            if (this.burstCooldownTicksLeft > 0)
                stringBuilder.AppendLine("CanFireIn".Translate() + ": " +
                                         this.burstCooldownTicksLeft.TicksToSecondsString());
            if (this.def.building.turretShellDef != null)
            {
                if (this.loaded)
                    stringBuilder.AppendLine("ShellLoaded".Translate());
                else
                    stringBuilder.AppendLine("ShellNotLoaded".Translate());
            }
            return stringBuilder.ToString();
        }

        public override void Draw()
        {
            this.top.DrawTurret();
            base.Draw();
        }

        public override void DrawExtraSelectionOverlays()
        {
            float range = this.GunCompEq.PrimaryVerb.verbProps.range;
            if ((double) range < 90.0)
                GenDraw.DrawRadiusRing(this.Position, range);
            float minRange = this.GunCompEq.PrimaryVerb.verbProps.minRange;
            if ((double) minRange < 90.0 && (double) minRange > 0.100000001490116)
                GenDraw.DrawRadiusRing(this.Position, minRange);
            if (this.burstWarmupTicksLeft > 0)
                GenDraw.DrawAimPie((Thing) this, this.CurrentTarget, (int) ((double) this.burstWarmupTicksLeft*0.5),
                    (float) this.def.size.x*0.5f);
            if (!this.forcedTarget.IsValid || this.forcedTarget.HasThing && !this.forcedTarget.Thing.Spawned)
                return;
            Vector3 B = !this.forcedTarget.HasThing
                ? this.forcedTarget.Cell.ToVector3Shifted()
                : this.forcedTarget.Thing.TrueCenter();
            Vector3 A = this.TrueCenter();
            B.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
            A.y = B.y;
            GenDraw.DrawLineBetween(A, B, Building_TurretSpotlight.ForcedTargetLineMat);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo baseGizmo in base.GetGizmos())
            {
                yield return baseGizmo;
            }

            if (this.CanSetForcedTarget)
            {
                {
                    Command_VerbPewPew attack = new Command_VerbPewPew();
                    attack.defaultLabel = "CommandSetForceAttackTarget".Translate();
                    attack.defaultDesc = "CommandSetForceAttackTargetDesc".Translate();
                    attack.icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true);
                    attack.verb = GunCompEq.PrimaryVerb;
                    attack.hotKey = KeyBindingDefOf.Misc4;
                    yield return attack;
                }
                {
                    Command_Action stop = new Command_Action();
                    stop.defaultLabel = "CommandStopForceAttack".Translate();
                    stop.defaultDesc = "CommandStopForceAttackDesc".Translate();
                    stop.icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt", true);
                    stop.action = delegate
                    {
                        this.ResetForcedTarget();
                        SoundDefOf.TickLow.PlayOneShotOnCamera();
                    };
                    if (!this.forcedTarget.IsValid)
                    {
                        stop.Disable("CommandStopAttackFailNotForceAttacking".Translate());
                    }
                    stop.hotKey = KeyBindingDefOf.Misc5;
                    yield return stop;
                }
            }

            if (this.CanToggleHoldFire)
            {
                Command_Toggle toggleHoldFire = new Command_Toggle();
                toggleHoldFire.defaultLabel = "CommandHoldFire".Translate();
                toggleHoldFire.defaultDesc = "CommandHoldFireDesc".Translate();
                toggleHoldFire.icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire", true);
                toggleHoldFire.hotKey = KeyBindingDefOf.Misc6;
                toggleHoldFire.toggleAction = delegate
                {
                    this.holdFire = !this.holdFire;
                    if (this.holdFire)
                    {
                        this.currentTargetInt = LocalTargetInfo.Invalid;
                        this.burstWarmupTicksLeft = 0;
                    }
                };
                toggleHoldFire.isActive = (() => this.holdFire);
                yield return toggleHoldFire;
            }
        }

        private void ResetForcedTarget()
        {
            this.forcedTarget = LocalTargetInfo.Invalid;
            if (this.burstCooldownTicksLeft > 0)
                return;
            this.TryStartShootSomething();
        }
    }
}