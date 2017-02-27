using Spotlights;
using System;
using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;

//Creator: Master Bucketsmith
//License: https://creativecommons.org/licenses/by-nc-sa/4.0/

namespace Spotlights
{
    //This class is an extension of a custom Building_TurretGun.
    public class SpotlightPewPew : Building_TurretSpotlight 
    {
        //This points the class to the other class 'Building_SpotlightGlower', so that any glowers spawned by this instance of the turret are destroyed when this instance is destroyed.
        //Would otherwise leave the glower on the map, as there was no check to remove them outside of this code.
        public Building_SpotlightGlower searchLight;
        public Building_SpotlightGlower selfLight;

        //Altered the vanilla Destroy method from Building, so that it allows proper destruction of assets to this turret.
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            InstallBlueprintUtility.CancelBlueprintsFor((Thing)this);

            if (!this.selfLight.DestroyedOrNull())
            {
                this.selfLight = null;
            }
            if (!this.searchLight.DestroyedOrNull())
            {
                this.searchLight = null;
            }

            if (mode != DestroyMode.Deconstruct)
                return;
            SoundStarter.PlayOneShot(SoundDef.Named("BuildingDeconstructed"), (SoundInfo)this.Position);
        }

        //Based on vanilla Tick for turretguns. Added lines to do SwitchOnSearchLight and CheckLitNow, shown below.
        public override void Tick()
        {
            base.Tick();
            SwitchOnSearchLight();
            CheckLitNow();
        }

        //The main powerhouse of this mod.
        public void SwitchOnSearchLight()
        {
            //If the current target isn't 'null' AND the current target is a valid one, do below.
            if (this.CurrentTarget != null && this.CurrentTarget.IsValid)
            {
                //Within the previous 'if statement', if the current self-glower is destroyed or non-existant, spawn a new one on position of the turret.
                //If an existing one is found, it will not spawn a new one.
                //Also tells the game that the glower building spawned by this turret is spawned by this turret - for destruction synchronisation.
                if (this.selfLight.DestroyedOrNull())
                {
                    this.selfLight = (Building_SpotlightGlower)GenSpawn.Spawn(ThingDef.Named("SpotlightSelfDeployed"), this.Position);
                    this.selfLight.spawnedBy = this;
                }
//                IntVec3 newPosition = getTargetThing().DrawPos.ToIntVec3();
//                Log.Warning("MBS Target position of spotlight: " + newPosition.ToString());
                //Same as above, but spawns larger glower on target and checks if target changed position.
                IntVec3 newPosition = this.CurrentTarget.Thing.DrawPos.ToIntVec3();
                if (this.searchLight.DestroyedOrNull())
                {
                    this.searchLight = (Building_SpotlightGlower)GenSpawn.Spawn(ThingDef.Named("SpotlightDeployed"), newPosition);
                    this.searchLight.spawnedBy = this;
                }
                //If target is at new position, destroy old target-glower and spawn new one at new position.
                if (newPosition != this.searchLight.Position)
                {
                    SwitchOffSearchLight();
                    this.searchLight = (Building_SpotlightGlower)GenSpawn.Spawn(ThingDef.Named("SpotlightDeployed"), newPosition);
                    this.searchLight.spawnedBy = this;
                }
            }
            //If current target is null or is not valid, do this. (Turns off the lights.)
            else
            {
                SwitchOffSearchLight();
                SwitchOffSelfLight();
            }
        }

//        public Thing getTargetThing()
//        {
//            Thing target = Verse.AI.AttackTargetFinder.BestAttackTarget(this, Verse.AI.TargetScanFlags.NeedReachable | Verse.AI.TargetScanFlags.NeedThreat,
//                (Predicate<Thing>)(x => x is Pawn), 0, 50);
//            //add custom targeting here
//            Log.Message("MBS Targeting");
//            return target;
//        }

        //Destroys the self-glower. (Turns off the light on the turret itself.)
        public void SwitchOffSelfLight()
        {
            if (!this.selfLight.DestroyedOrNull())
            {
                this.selfLight.Destroy();
                this.selfLight = null;
            }
        }

        //Destroys the target-glower. (Turns off the light on the target.)
        public void SwitchOffSearchLight()
        {
            if (!this.searchLight.DestroyedOrNull())
            {
                this.searchLight.Destroy();
                this.searchLight = null;
            }
        }

        //Checks if the spotlights require power & are powered OR if the spotlight is mannable & is being manned right now.
        //If those criteria aren't met, the glowers are destroyed. (I.e. power is lost or the pawn manning it moves away.)
        public void CheckLitNow()
        {
            if ((this.powerComp != null && !this.powerComp.PowerOn) ||
                (this.mannableComp != null && !this.mannableComp.MannedNow))
            {
                SwitchOffSearchLight();
                SwitchOffSelfLight();
            }
                
        }
    }
}