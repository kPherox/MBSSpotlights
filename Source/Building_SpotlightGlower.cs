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
    //This sets the spawned glower buildings to be a 'property' of the turret that spawned them.
    //This allows the game to keep track of which glower belongs to which turret and remove them if the turret dies.
    //Shown below are some examples for debugging through the in-game console.
	public class Building_SpotlightGlower : Building
	{
	    public Thing spawnedBy;

        public override void SpawnSetup(Map map)
        {
            base.SpawnSetup(map);
        }

        public override void Tick()
        {
            base.Tick();

            if (this.spawnedBy != null)
            {
                //Log.Message("DEBUG: " + this.spawnedBy.ToString());
                if (this.spawnedBy.Destroyed)
                {
                    //Log.Message("DEBUG: My parent is gone. Destroying");
                    this.Destroy();
                }
            }
            else
            {
                //Log.Warning("MBS Spotlights removed invalid glow effect building.");
                this.Destroy();
            }
        }

        /*public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.LookReference<Thing>(ref this.spawnedBy, "spawnedBy");
        }*/
    }
}