using System;
using Verse;

namespace SRA
{
    public class CompProperties_Generator_SRA_Core : CompProperties
    {
        public ThingDef product;
        public int itemsPerCycle = 1;
        public float baseProgressPerCycle = 10f;
        public float progressPerFacilityPerCycle = 2.5f;

        public CompProperties_Generator_SRA_Core() =>
            compClass = typeof(CompGenerator_SRA_Core);
    }
}
