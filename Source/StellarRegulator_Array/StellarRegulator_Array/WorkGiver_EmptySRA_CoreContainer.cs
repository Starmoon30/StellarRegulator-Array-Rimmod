using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SRA
{
    // The station has both a transporter and a hidden output cache. Use a dedicated job driver so
    // extraction always reads the output cache rather than whichever container vanilla resolves first.
    public class WorkGiver_EmptySRA_CoreContainer : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                yield break;
            }

            List<Thing> stations = pawn.Map.listerThings.ThingsOfDef(SRA_DefOf.SRA_Astronomical_Fabrications_Main);
            for (int i = 0; i < stations.Count; i++)
            {
                Thing station = stations[i];
                if (station.TryGetComp<CompGenerator_SRA_Core>()?.OutputItem != null)
                {
                    yield return station;
                }
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn?.Map == null || !pawn.Map.listerThings
                .ThingsOfDef(SRA_DefOf.SRA_Astronomical_Fabrications_Main)
                .Any(station => station.TryGetComp<CompGenerator_SRA_Core>()?.OutputItem != null);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.IsForbidden(pawn) ||
                !pawn.CanReserve(t, 1, -1, GetReservationLayer(pawn, t), forced))
            {
                return false;
            }

            CompGenerator_SRA_Core core = t.TryGetComp<CompGenerator_SRA_Core>();
            Thing outputItem = core?.OutputItem;
            if (outputItem == null)
            {
                return false;
            }

            if (!StoreUtility.TryFindBestBetterStorageFor(
                    outputItem,
                    pawn,
                    pawn.Map,
                    StoragePriority.Unstored,
                    pawn.Faction,
                    out _,
                    out _,
                    false))
            {
                JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing outputItem = t.TryGetComp<CompGenerator_SRA_Core>()?.OutputItem;
            if (outputItem == null)
            {
                return null;
            }

            if (!StoreUtility.TryFindBestBetterStorageFor(
                    outputItem,
                    pawn,
                    pawn.Map,
                    StoragePriority.Unstored,
                    pawn.Faction,
                    out IntVec3 cell,
                    out _,
                    true))
            {
                JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
                return null;
            }

            Job job = JobMaker.MakeJob(SRA_DefOf.SRA_EmptySRA_CoreContainer, t, outputItem, cell);
            job.count = outputItem.stackCount;
            return job;
        }

        public override ReservationLayerDef GetReservationLayer(Pawn pawn, LocalTargetInfo t)
        {
            return ReservationLayerDefOf.Empty;
        }
    }

    public class JobDriver_ExtractSRAOrbitalOutput : JobDriver
    {
        private Thing Station => job.targetA.Thing;
        private Thing OutputItem => job.targetB.Thing;
        private CompSRAHiddenThingContainer OutputContainer => Station?.TryGetComp<CompSRAHiddenThingContainer>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, ReservationLayerDefOf.Empty, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_General.WaitWith(TargetIndex.A, 60, true, true, false, TargetIndex.A, PathEndMode.InteractionCell);

            Toil takeFromOutput = new Toil
            {
                initAction = delegate
                {
                    Thing item = OutputItem;
                    CompSRAHiddenThingContainer output = OutputContainer;
                    if (item == null || output?.innerContainer == null || !output.innerContainer.Contains(item))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    Thing taken = output.innerContainer.Take(item, Math.Max(1, Math.Min(job.count, item.stackCount)));
                    if (taken == null)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (pawn.carryTracker.TryStartCarry(taken, taken.stackCount, false) > 0)
                    {
                        return;
                    }

                    output.innerContainer.TryAdd(taken);
                    EndJobWith(JobCondition.Incompletable);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return takeFromOutput;
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.C, PathEndMode.ClosestTouch);
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, null, false);
        }
    }
}
