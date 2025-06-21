using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SRA
{
    public class CompCommunicator : ThingComp
    {

        private CompFlickable flickComp => parent.TryGetComp<CompFlickable>();

        public float Percent => portionYieldPct;


        public bool CanOperateNow()
        {
            if (flickComp != null && !flickComp.SwitchIsOn)
            {
                return false;
            }
            return true;
        }



        public override void PostExposeData()
        {
            Scribe_Values.Look(ref portionProgress, "portionProgress", 0f);
            Scribe_Values.Look(ref portionYieldPct, "portionYieldPct", 0f);
            Scribe_Values.Look(ref toggle, "toggle", false);
        }

        private bool toggle;

        private float portionProgress;

        private float portionYieldPct;
    }
}
