using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SRA
{
    // Token: 0x02000121 RID: 289
    public class WorkGiver_EmptySRA_CoreContainer : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return (from t in pawn.Map.listerThings.AllThings
                    where t.def.defName == "SRA_Astronomical_Fabrications_Main"
                    select t).ToList<Thing>();
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return (from t in pawn.Map.listerThings.AllThings
                    where t.def.defName == "SRA_Astronomical_Fabrications_Main"
                    select t).ToList<Thing>().NullOrEmpty<Thing>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            bool flag = t.IsForbidden(pawn);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                LocalTargetInfo target = t;
                bool flag2 = !pawn.CanReserve(target, 1, -1, this.GetReservationLayer(pawn, t), forced);
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    CompGenerator_SRA_Core compGenerator_SRA_Core = t.TryGetComp<CompGenerator_SRA_Core>();
                    bool flag3 = compGenerator_SRA_Core == null || !compGenerator_SRA_Core.CanEmptyNow;
                    if (flag3)
                    {
                        result = false;
                    }
                    else
                    {
                        IntVec3 intVec;
                        IHaulDestination haulDestination;
                        bool flag4 = !StoreUtility.TryFindBestBetterStorageFor(compGenerator_SRA_Core.CoreItem, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out intVec, out haulDestination, false);
                        if (flag4)
                        {
                            JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
                            result = false;
                        }
                        else
                        {
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompGenerator_SRA_Core compGenerator_SRA_Core = t.TryGetComp<CompGenerator_SRA_Core>();
            bool flag = compGenerator_SRA_Core == null || !compGenerator_SRA_Core.CanEmptyNow;
            Job result;
            if (flag)
            {
                result = null;
            }
            else
            {
                IntVec3 c;
                IHaulDestination haulDestination;
                bool flag2 = !StoreUtility.TryFindBestBetterStorageFor(compGenerator_SRA_Core.CoreItem, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out c, out haulDestination, true);
                if (flag2)
                {
                    JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
                    result = null;
                }
                else
                {
                    Job job = JobMaker.MakeJob(SRA_DefOf.SRA_EmptySRA_CoreContainer, t, compGenerator_SRA_Core.CoreItem, c);
                    job.count = compGenerator_SRA_Core.CoreItem.stackCount;
                    result = job;
                }
            }
            return result;
        }

        public override ReservationLayerDef GetReservationLayer(Pawn pawn, LocalTargetInfo t)
        {
            return ReservationLayerDefOf.Empty;
        }
    }
}
