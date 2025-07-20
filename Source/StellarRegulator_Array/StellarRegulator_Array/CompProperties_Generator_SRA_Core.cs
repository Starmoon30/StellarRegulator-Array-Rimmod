using System;
using Verse;

namespace SRA
{
    // Token: 0x0200009D RID: 157
    public class CompProperties_Generator_SRA_Core : CompProperties
    {
        // Token: 0x06000319 RID: 793 RVA: 0x0001356D File Offset: 0x0001176D
        public CompProperties_Generator_SRA_Core()
        {
            this.compClass = typeof(CompGenerator_SRA_Core);
        }

        // Token: 0x040000D5 RID: 213
        public int productPerGenBase = 1;

        // Token: 0x040000D6 RID: 214
        public ThingDef product;
    }
}
