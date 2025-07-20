using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    // Token: 0x0200009E RID: 158
    public class CompGenerator_SRA_Core : ThingComp
    {
        // Token: 0x170000D4 RID: 212
        // (get) Token: 0x0600031A RID: 794 RVA: 0x0001358E File Offset: 0x0001178E
        private Thing thing
        {
            get
            {
                return ThingMaker.MakeThing(this.Props.product, null);
            }
        }

        // Token: 0x170000D5 RID: 213
        // (get) Token: 0x0600031B RID: 795 RVA: 0x000135A1 File Offset: 0x000117A1
        private CompExplosive compExplosive
        {
            get
            {
                return this.parent.TryGetComp<CompExplosive>();
            }
        }

        // Token: 0x170000D6 RID: 214
        // (get) Token: 0x0600031C RID: 796 RVA: 0x000135AE File Offset: 0x000117AE
        public CompProperties_Generator_SRA_Core Props
        {
            get
            {
                return (CompProperties_Generator_SRA_Core)this.props;
            }
        }

        // Token: 0x170000D7 RID: 215
        // (get) Token: 0x0600031D RID: 797 RVA: 0x000135BB File Offset: 0x000117BB
        public Thing SRA_Core
        {
            get
            {
                return this.parent.TryGetInnerInteractableThingOwner().FirstOrFallback((Thing t) => t.def == SRA_DefOf.SRA_Core, null);
            }
        }

        // Token: 0x170000D8 RID: 216
        // (get) Token: 0x0600031E RID: 798 RVA: 0x000135ED File Offset: 0x000117ED
        public CompThingContainer compThingContainer
        {
            get
            {
                return this.parent.TryGetComp<CompThingContainer>();
            }
        }

        // Token: 0x170000D9 RID: 217
        // (get) Token: 0x0600031F RID: 799 RVA: 0x000135FA File Offset: 0x000117FA
        public CompAffectedByFacilities compAffectedByFacilities
        {
            get
            {
                return this.parent.TryGetComp<CompAffectedByFacilities>();
            }
        }

        // Token: 0x170000DA RID: 218
        // (get) Token: 0x06000320 RID: 800 RVA: 0x00013607 File Offset: 0x00011807
        public int FacilitiesNum
        {
            get
            {
                return this.compAffectedByFacilities.LinkedFacilitiesListForReading.Count<Thing>();
            }
        }

        // Token: 0x170000DB RID: 219
        // (get) Token: 0x06000321 RID: 801 RVA: 0x00013619 File Offset: 0x00011819
        public int ProductPerGen
        {
            get
            {
                return this.Props.productPerGenBase;
            }
        }

        // Token: 0x170000DC RID: 220
        // (get) Token: 0x06000322 RID: 802 RVA: 0x00013644 File Offset: 0x00011844
        public bool CanEmptyNow
        {
            get
            {
                bool flag = this.parent == null;
                return !flag && this.SRA_Core != null;
            }
        }

        // Token: 0x170000DE RID: 222
        // (get) Token: 0x06000324 RID: 804 RVA: 0x000136F4 File Offset: 0x000118F4
        private bool CanWork
        {
            get
            {
                return (float)this.FacilitiesNum > 0f;
            }
        }

        // Token: 0x06000326 RID: 806 RVA: 0x00013753 File Offset: 0x00011953
        public override void PostExposeData()
        {
            Scribe_Values.Look<int>(ref this.percentage, "percentage", 0, false);
            Scribe_Values.Look<int>(ref this.tickNum, "tickNum", 0, false);
        }

        // Token: 0x06000327 RID: 807 RVA: 0x0001377C File Offset: 0x0001197C
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }

        // Token: 0x06000328 RID: 808 RVA: 0x00013788 File Offset: 0x00011988
        public override void CompTick()
        {
            bool flag = this.parent != null && !this.compThingContainer.Full && this.CanWork;
            if (flag)
            {
                this.tickNum++;
                bool flag2 = (float)this.tickNum > CompGenerator_SRA_Core.FacilitiesNumToEfficiencyCurve.Evaluate((float)this.FacilitiesNum);
                if (flag2)
                {
                    this.tickNum = 0;
                    this.percentage++;
                    bool flag3 = this.percentage >= 100;
                    if (flag3)
                    {
                        this.percentage = this.percentage - 100;
                        this.ProduceFuel(this.ProductPerGen);
                    }
                }
            }
        }

        // Token: 0x06000329 RID: 809 RVA: 0x00013834 File Offset: 0x00011A34
        public void ProduceFuel(int amountToMake)
        {
            int num = Mathf.CeilToInt((float)amountToMake / (float)this.thing.def.stackLimit);
            for (int i = 0; i < num; i++)
            {
                Thing thing = ThingMaker.MakeThing(this.Props.product, null);
                thing.stackCount = Mathf.Min(amountToMake, thing.def.stackLimit);
                bool flag = this.parent.TryGetInnerInteractableThingOwner().TryAdd(thing, true);
                if (!flag)
                {
                    break;
                }
                amountToMake -= thing.stackCount;
            }
        }

        // Token: 0x170000E0 RID: 224
        // (get) Token: 0x0600032A RID: 810 RVA: 0x000138C0 File Offset: 0x00011AC0
        public string LabelCapWithTotalCount
        {
            get
            {
                bool flag = this.parent != null;
                string result;
                if (flag)
                {
                    result = this.thing.LabelCapNoCount + " x" + this.ProductPerGen.ToStringCached();
                }
                else
                {
                    result = null;
                }
                return result;
            }
        }

        // Token: 0x0600032B RID: 811 RVA: 0x00013904 File Offset: 0x00011B04
        public override string CompInspectStringExtra()
        {
            return "SRA_CompGenerator_SRA_Core_Percentage".Translate() + this.LabelCapWithTotalCount + ": " + this.percentage.ToString() + "%";
        }

        // Token: 0x040000D7 RID: 215
        private int percentage = 0;

        // Token: 0x040000D8 RID: 216
        private int tickNum = 0;

        // Token: 0x040000D9 RID: 217
        public static readonly SimpleCurve FacilitiesNumToEfficiencyCurve = new SimpleCurve
        {
            {
                new CurvePoint(0f, 500f),
                true
            },
            {
                new CurvePoint(100f, 10f),
                true
            }
        };
    }
}
