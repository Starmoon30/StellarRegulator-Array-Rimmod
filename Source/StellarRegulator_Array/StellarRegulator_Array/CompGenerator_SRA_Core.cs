using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    public class CompGenerator_SRA_Core : ThingComp
    {
        private const int TicksPerCycle = 60;
        private float progressPercentage;
        private int cycleTicks;

        public CompProperties_Generator_SRA_Core Props =>
            (CompProperties_Generator_SRA_Core)props;

        private Thing ProductPrototype =>
            ThingMaker.MakeThing(Props.product);

        private CompThingContainer ContainerComp =>
            parent.GetComp<CompThingContainer>();

        private CompAffectedByFacilities FacilityComp =>
            parent.GetComp<CompAffectedByFacilities>();

        public bool CanEmptyNow =>
            parent != null && CoreItem != null;

        public int FacilitiesNum
        {
            get
            {
                return this.FacilityComp.LinkedFacilitiesListForReading.Count<Thing>();
            }
        }

        public Thing CoreItem =>
            parent.TryGetInnerInteractableThingOwner()
                .FirstOrDefault(t => t.def == SRA_DefOf.SRA_Core);

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref progressPercentage, "progressPercentage", 0f);
            Scribe_Values.Look(ref cycleTicks, "cycleTicks", 0);
        }

        public override void CompTick()
        {
            if (++cycleTicks >= TicksPerCycle)
            {
                cycleTicks = 0;
                // 计算进度增量：基础值 + 建筑数量 * 系数
                progressPercentage += Props.baseProgressPerCycle +
                                     this.FacilitiesNum *
                                     Props.progressPerFacilityPerCycle;
                // 处理完整生产周期
                while (progressPercentage >= 100f)
                {
                    progressPercentage -= 100f;
                    ProduceItems(Props.itemsPerCycle);
                    if (ContainerComp.Full) break;
                }
            }
        }

        private void ProduceItems(int amount)
        {
            while (amount > 0)
            {
                Thing newItem = ProductPrototype;
                newItem.stackCount = Math.Min(amount, newItem.def.stackLimit);

                if (!this.parent.TryGetInnerInteractableThingOwner().TryAdd(newItem))
                    break;

                amount -= newItem.stackCount;
            }
        }

        public override string CompInspectStringExtra()
        {
            float displayPercent = progressPercentage;
            return "SRA_ProductionProgress".Translate(
                Props.itemsPerCycle,
                ProductPrototype.LabelCap,
                displayPercent
            );
        }
    }

    public class PlaceWorker_Generator_SRA_Core : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef def, IntVec3 center, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            List<IntVec3> list = new List<IntVec3>();
            foreach (IntVec3 item in map)
            {
                list.Add(item);
            }
            foreach (IntVec3 c in list)
            {
                bool flag = !c.InBounds(map);
                if (!flag)
                {
                    List<Thing> thingList = c.GetThingList(map);
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        bool flag2 = thingList[i].def == def || thingList[i].def.entityDefToBuild == def;
                        if (flag2)
                        {
                            return "MustNotGenerator_SRA_Core".Translate(def);
                        }
                    }
                }
            }
            return true;
        }
    }
}