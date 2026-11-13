using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SRA
{
    // Downloaded products are virtual until a pawn actually takes them out of the station.
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
                if (stations[i].TryGetComp<CompGenerator_SRA_Core>()?.HasOutput == true)
                {
                    yield return stations[i];
                }
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn?.Map == null || !pawn.Map.listerThings
                .ThingsOfDef(SRA_DefOf.SRA_Astronomical_Fabrications_Main)
                .Any(station => station.TryGetComp<CompGenerator_SRA_Core>()?.HasOutput == true);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn == null || t == null || t.IsForbidden(pawn) ||
                !pawn.CanReserve(t, 1, -1, GetReservationLayer(pawn, t), forced))
            {
                return false;
            }

            CompGenerator_SRA_Core core = t.TryGetComp<CompGenerator_SRA_Core>();
            Thing preview = core?.MakeOutputPreview();
            if (preview == null)
            {
                return false;
            }

            int availableStackSpace = pawn.carryTracker?.AvailableStackSpace(preview.def) ?? 0;
            if (availableStackSpace <= 0)
            {
                preview.Destroy(DestroyMode.Vanish);
                JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
                return false;
            }

            preview.stackCount = Math.Min(preview.stackCount, availableStackSpace);
            bool hasStorage = StoreUtility.TryFindBestBetterStorageFor(
                preview,
                pawn,
                pawn.Map,
                StoragePriority.Unstored,
                pawn.Faction,
                out _,
                out _,
                false);
            preview.Destroy(DestroyMode.Vanish);
            if (!hasStorage)
            {
                JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
            }

            return hasStorage;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompGenerator_SRA_Core core = t?.TryGetComp<CompGenerator_SRA_Core>();
            Thing preview = core?.MakeOutputPreview();
            if (preview == null || pawn?.carryTracker == null)
            {
                preview?.Destroy(DestroyMode.Vanish);
                return null;
            }

            int availableStackSpace = pawn.carryTracker.AvailableStackSpace(preview.def);
            if (availableStackSpace <= 0)
            {
                preview.Destroy(DestroyMode.Vanish);
                return null;
            }

            preview.stackCount = Math.Min(preview.stackCount, availableStackSpace);
            if (!StoreUtility.TryFindBestBetterStorageFor(
                    preview,
                    pawn,
                    pawn.Map,
                    StoragePriority.Unstored,
                    pawn.Faction,
                    out IntVec3 cell,
                    out _,
                    true))
            {
                preview.Destroy(DestroyMode.Vanish);
                JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
                return null;
            }

            int count = preview.stackCount;
            preview.Destroy(DestroyMode.Vanish);
            if (count <= 0)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(SRA_DefOf.SRA_EmptySRA_CoreContainer, t, cell);
            job.count = count;
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
        private CompGenerator_SRA_Core Core => Station?.TryGetComp<CompGenerator_SRA_Core>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool stationReserved = pawn.Reserve(job.targetA, job, 1, -1, ReservationLayerDefOf.Empty, errorOnFailed);
            bool cellReserved = pawn.Reserve(job.targetB, job, 1, -1, ReservationLayerDefOf.Empty, errorOnFailed);
            return stationReserved && cellReserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_General.WaitWith(TargetIndex.A, 60, true, true, false, TargetIndex.A, PathEndMode.InteractionCell);

            yield return new Toil
            {
                initAction = delegate
                {
                    if (Core == null || !Core.TryTakeOutputToPawn(pawn, job.count, out _))
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, null, false);
        }
    }
}
