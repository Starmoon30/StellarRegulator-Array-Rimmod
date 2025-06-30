using System;
using RimWorld;
using SRA;
using Verse;

namespace SRA
{
    // Token: 0x02000023 RID: 35
    public class CompProperties_UseEffect_ActivateMech : CompProperties_UseEffect
    {
        // Token: 0x06000079 RID: 121 RVA: 0x00003E85 File Offset: 0x00002085
        public CompProperties_UseEffect_ActivateMech()
        {
            this.compClass = typeof(CompUseEffect_ActivateMech);
        }

        // Token: 0x04000037 RID: 55
        public PawnKindDef pawnKindDef;

        // Token: 0x04000038 RID: 56
        public bool requireMechanitor = true;
    }
}
