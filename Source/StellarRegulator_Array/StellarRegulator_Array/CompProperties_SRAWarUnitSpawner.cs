using System;
using System.Collections.Generic;
using Verse;

namespace SRA
{
    // Token: 0x0200000B RID: 11
    public class CompProperties_SRAWarUnitSpawner : CompProperties
    {
        // Token: 0x06000024 RID: 36 RVA: 0x00002AEE File Offset: 0x00000CEE
        public CompProperties_SRAWarUnitSpawner()
        {
            this.compClass = typeof(Comp_SRAWarUnitSpawner);
        }

        // Token: 0x04000007 RID: 7
        public List<PawnKindDef> pawnKindDef;

        public int maxWarUnits = 20; // 默认值20，可在XML中覆盖
    }
}
