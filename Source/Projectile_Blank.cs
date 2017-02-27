using Spotlights;
using System;
using UnityEngine;
using Verse;
using RimWorld;

//Creator: Master Bucketsmith
//License: https://creativecommons.org/licenses/by-nc-sa/4.0/

namespace Spotlights
{
    //Empty override of the impact of a projectile, so that there's no sound and graphic spawned when the projectile hits.
    //Projectile does nothing in this mod, but haven't found a way to skip the entire firing mechanism so this is necessary.
    public class ProjectileBlank : Projectile
    {
        protected override void Impact(Thing hitThing)
        {
            base.Impact(hitThing);

            if (hitThing != null)
            {
                //Log.Message("MBS DEBUG: fired projectile");
            }
        }
    }
}