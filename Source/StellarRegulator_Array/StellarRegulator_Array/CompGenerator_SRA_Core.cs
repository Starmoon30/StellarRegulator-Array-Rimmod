using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SRA
{
    public enum OrbitalTransferKind
    {
        None,
        ItemDownload
    }

    public class CompOrbitalScannerTransporter : CompTransporter
    {
        private static readonly FieldInfo GroupIdField = typeof(CompTransporter).GetField("groupID", BindingFlags.Instance | BindingFlags.NonPublic);

        public bool HasLoadedItems => innerContainer != null && innerContainer.Count > 0;

        public bool ClearPendingLoadPreservingContents(Map map)
        {
            bool hadPendingLoad = leftToLoad != null && leftToLoad.Count > 0;
            if (!hadPendingLoad && !LoadingInProgressOrReadyToLaunch)
            {
                return false;
            }

            // The stock cancel path also drops loaded contents. Keep the scan bay intact and only clear its hauling assignment.
            TryRemoveLord(map);
            leftToLoad?.Clear();
            GroupIdField?.SetValue(this, -1);
            return hadPendingLoad;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }
    }

    // The internal download cache stays hidden from the default inspect string; the console exposes it directly.
    public class CompSRAHiddenThingContainer : CompThingContainer
    {
        public override string CompInspectStringExtra()
        {
            return string.Empty;
        }
    }

    // Orbital production is fully self-managed by this component and never creates colonist work orders.
    public class CompGenerator_SRA_Core : ThingComp
    {
        // This is the local, finite upload buffer. The orbital balance is stored by OrbitalIndustryNetworkState.
        private long massEnergy;
        private bool autoLaunch;
        private long autoLaunchReserve;
        private int autoLaunchTargetUnits;
        private int launchCooldownTicks;

        private List<OrbitalFabricationOrder> orbitalOrders = new List<OrbitalFabricationOrder>();
        private int nextOrderId = 1;
        private int linkIntervalTicks;
        private int nextLinkTick;
        private bool downloadAnimationEnabled = true;
        private bool downloadSoundEnabled = true;
        private int lastLinkTick = -1;
        private long lastUploadAmount;
        private long lastDownloadCost;
        private int lastDownloadOrderId = -1;

        private int transferTicksLeft;
        private int transferTicksTotal;
        private OrbitalTransferKind transferKind;
        private bool orbitalBeamAnimationStarted;
        private bool beamSoundPlayed;
        private bool windupSoundPlayed;
        private string cachedControlPanelIconPath;
        private Texture2D cachedControlPanelIcon;

        // A selected batch is reserved before its animation begins, so save/load cannot duplicate a downlink.
        private int pendingDownloadOrderId = -1;
        private ThingDef pendingDownloadProductDef;
        private ThingDef pendingDownloadStuffDef;
        private int pendingDownloadCount;
        private int pendingDownloadQualityMin = (int)QualityCategory.Awful;
        private int pendingDownloadQualityMax = (int)QualityCategory.Legendary;

        public CompProperties_Generator_SRA_Core Props => (CompProperties_Generator_SRA_Core)props;

        private Texture2D ControlPanelIcon
        {
            get
            {
                string iconPath = Props.controlPanelIconPath;
                if (string.IsNullOrEmpty(iconPath))
                {
                    return TexButton.Info;
                }

                if (cachedControlPanelIconPath != iconPath)
                {
                    cachedControlPanelIconPath = iconPath;
                    cachedControlPanelIcon = ContentFinder<Texture2D>.Get(iconPath, reportFailure: false);
                }

                return cachedControlPanelIcon ?? TexButton.Info;
            }
        }

        public Map Map => parent.Map;

        public long MassEnergy => massEnergy;
        public long StorageCapacity => OrbitalFabricationUtility.SaturatingAdd(
            Props.baseStorageCapacity,
            OrbitalFabricationUtility.SaturatingMultiply(Props.storageCapacityPerFacility, FacilitiesNum));
        public long OrbitalMassEnergy => Network?.OrbitalMassEnergy ?? 0L;
        public long OrbitalMassEnergyCapacity => Network?.GetOrbitalMassEnergyCapacity(Props.orbitalMassEnergyCapacityPerUnit) ?? 0L;
        public int FacilitiesNum => FacilityComp?.LinkedFacilitiesListForReading.Count ?? 0;

        public bool AutoLaunch { get => autoLaunch; set => autoLaunch = value; }
        public long AutoLaunchReserve { get => autoLaunchReserve; set => autoLaunchReserve = OrbitalFabricationUtility.ClampMassEnergy(value); }
        public int AutoLaunchTargetUnits
        {
            get => autoLaunchTargetUnits;
            set => autoLaunchTargetUnits = Math.Max(0, Math.Min(value, OrbitalFabricationUtility.MaximumIndustrialUnits));
        }

        public int LaunchCooldownTicks => launchCooldownTicks;
        public bool DownloadAnimationEnabled { get => downloadAnimationEnabled; set => downloadAnimationEnabled = value; }
        public bool DownloadSoundEnabled
        {
            get => downloadSoundEnabled;
            set => downloadSoundEnabled = value;
        }
        public int LinkIntervalTicks => NormalizeLinkInterval(linkIntervalTicks > 0 ? linkIntervalTicks : Props.defaultLinkIntervalTicks);
        public int LinkIntervalSeconds => Math.Max(1, Mathf.RoundToInt(LinkIntervalTicks / 60f));
        public int NextLinkTicks => Math.Max(0, nextLinkTick - (Find.TickManager?.TicksGame ?? 0));
        public long LinkBandwidthPerEvent
        {
            get
            {
                int defaultInterval = Math.Max(1, Props.defaultLinkIntervalTicks);
                long defaultBandwidth = OrbitalFabricationUtility.SaturatingAdd(
                    Math.Max(0L, Props.baseBandwidthPerDefaultInterval),
                    OrbitalFabricationUtility.SaturatingMultiply(
                        Math.Max(0L, Props.bandwidthPerFacilityPerDefaultInterval),
                        FacilitiesNum));
                long scaled = OrbitalFabricationUtility.SaturatingMultiply(defaultBandwidth, LinkIntervalTicks);
                return Math.Max(1L, scaled / defaultInterval);
            }
        }

        public int OutputItemCapacity => Math.Max(1, Props.outputItemCapacity);
        public int OutputItemCount
        {
            get
            {
                long count = 0L;
                if (OutputContainer?.innerContainer == null)
                {
                    return 0;
                }

                foreach (Thing thing in OutputContainer.innerContainer)
                {
                    count += Math.Max(0, thing.stackCount);
                    if (count >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }

                return (int)count;
            }
        }

        public IEnumerable<Thing> OutputItems => OutputContainer?.innerContainer ?? Enumerable.Empty<Thing>();
        public IReadOnlyList<OrbitalFabricationOrder> OrbitalOrders => orbitalOrders;
        public int LastLinkTick => lastLinkTick;
        public long LastUploadAmount => lastUploadAmount;
        public long LastDownloadCost => lastDownloadCost;
        public int LastDownloadOrderId => lastDownloadOrderId;
        public OrbitalTransferKind TransferKind => transferKind;
        public bool TransferActive => transferKind == OrbitalTransferKind.ItemDownload;
        public float TransferProgress => transferTicksTotal <= 0 ? 0f : 1f - transferTicksLeft / (float)transferTicksTotal;
        public string GenerationPeriodLabel => Math.Max(1, Props.orbitalGenerationIntervalTicks).ToStringTicksToPeriod();

        public CompOrbitalScannerTransporter ScannerComp => parent.GetComp<CompOrbitalScannerTransporter>();
        public CompSRAHiddenThingContainer OutputContainer => parent.GetComp<CompSRAHiddenThingContainer>();

        public Thing OutputItem => OutputContainer?.innerContainer?.FirstOrDefault();

        private CompAffectedByFacilities FacilityComp => parent.GetComp<CompAffectedByFacilities>();
        private CompPowerTrader PowerComp => parent.GetComp<CompPowerTrader>();
        private CompBreakdownable BreakdownComp => parent.GetComp<CompBreakdownable>();
        private CompFlickable FlickComp => parent.GetComp<CompFlickable>();

        public OrbitalIndustryNetworkState Network
        {
            get
            {
                if (Find.World == null || parent.Map == null)
                {
                    return null;
                }

                return Find.World.GetComponent<WorldComponent_SRAOrbitalIndustry>()?.GetOrCreateNetwork(parent.Map.uniqueID);
            }
        }

        public bool Operational => parent.Spawned &&
                                   (PowerComp == null || PowerComp.PowerOn) &&
                                   (BreakdownComp == null || !BreakdownComp.BrokenDown) &&
                                   (FlickComp == null || FlickComp.SwitchIsOn);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureLinkSchedule();
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref massEnergy, "massEnergy", 0L);
            Scribe_Values.Look(ref autoLaunch, "autoLaunch", false);
            Scribe_Values.Look(ref autoLaunchReserve, "autoLaunchReserve", 0L);
            Scribe_Values.Look(ref autoLaunchTargetUnits, "autoLaunchTargetUnits", 0);
            Scribe_Values.Look(ref launchCooldownTicks, "launchCooldownTicks", 0);
            Scribe_Collections.Look(ref orbitalOrders, "orbitalOrders", LookMode.Deep);
            Scribe_Values.Look(ref nextOrderId, "nextOrderId", 1);
            Scribe_Values.Look(ref linkIntervalTicks, "linkIntervalTicks", 0);
            Scribe_Values.Look(ref nextLinkTick, "nextLinkTick", 0);
            Scribe_Values.Look(ref downloadAnimationEnabled, "downloadAnimationEnabled", true);
            Scribe_Values.Look(ref downloadSoundEnabled, "downloadSoundEnabled", true);
            Scribe_Values.Look(ref lastLinkTick, "lastLinkTick", -1);
            Scribe_Values.Look(ref lastUploadAmount, "lastUploadAmount", 0L);
            Scribe_Values.Look(ref lastDownloadCost, "lastDownloadCost", 0L);
            Scribe_Values.Look(ref lastDownloadOrderId, "lastDownloadOrderId", -1);

            Scribe_Values.Look(ref transferTicksLeft, "transferTicksLeft", 0);
            Scribe_Values.Look(ref transferTicksTotal, "transferTicksTotal", 0);
            Scribe_Values.Look(ref transferKind, "transferKind", OrbitalTransferKind.None);
            Scribe_Values.Look(ref orbitalBeamAnimationStarted, "orbitalBeamAnimationStarted", false);
            Scribe_Values.Look(ref beamSoundPlayed, "beamSoundPlayed", false);
            Scribe_Values.Look(ref windupSoundPlayed, "windupSoundPlayed", false);
            Scribe_Values.Look(ref pendingDownloadOrderId, "pendingDownloadOrderId", -1);
            Scribe_Defs.Look(ref pendingDownloadProductDef, "pendingDownloadProductDef");
            Scribe_Defs.Look(ref pendingDownloadStuffDef, "pendingDownloadStuffDef");
            Scribe_Values.Look(ref pendingDownloadCount, "pendingDownloadCount", 0);
            Scribe_Values.Look(ref pendingDownloadQualityMin, "pendingDownloadQualityMin", (int)QualityCategory.Awful);
            Scribe_Values.Look(ref pendingDownloadQualityMax, "pendingDownloadQualityMax", (int)QualityCategory.Legendary);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                massEnergy = Math.Min(OrbitalFabricationUtility.ClampMassEnergy(massEnergy), StorageCapacity);
                autoLaunchReserve = OrbitalFabricationUtility.ClampMassEnergy(autoLaunchReserve);
                autoLaunchTargetUnits = Math.Max(0, Math.Min(autoLaunchTargetUnits, OrbitalFabricationUtility.MaximumIndustrialUnits));
                launchCooldownTicks = Math.Max(0, launchCooldownTicks);
                orbitalOrders = (orbitalOrders ?? new List<OrbitalFabricationOrder>())
                    .Where(order => order != null && order.productDef != null)
                    .ToList();
                int highestOrderId = 0;
                for (int i = 0; i < orbitalOrders.Count; i++)
                {
                    orbitalOrders[i].Normalize();
                    highestOrderId = Math.Max(highestOrderId, orbitalOrders[i].id);
                }

                int firstUnusedAfterLoadedOrders = highestOrderId >= int.MaxValue ? 1 : highestOrderId + 1;
                nextOrderId = Math.Max(1, Math.Max(firstUnusedAfterLoadedOrders, nextOrderId));
                linkIntervalTicks = NormalizeLinkInterval(linkIntervalTicks > 0 ? linkIntervalTicks : Props.defaultLinkIntervalTicks);
                NormalizePendingDownloadQuality();

                // Old transfer kinds were visual upload/launch operations. They are intentionally retired.
                if (transferKind != OrbitalTransferKind.ItemDownload || pendingDownloadProductDef == null || pendingDownloadCount <= 0)
                {
                    ClearTransferState();
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }

            int ticks = Find.TickManager.TicksGame;
            if (launchCooldownTicks > 0)
            {
                launchCooldownTicks--;
            }

            if (parent.IsHashIntervalTick(250))
            {
                Network?.GenerateUntil(ticks, Props.orbitalGenerationIntervalTicks, Props.unitYieldPerInterval, OrbitalMassEnergyCapacity);
            }

            if (TransferActive)
            {
                TickTransfer();
            }

            EnsureLinkSchedule();
            if (ticks >= nextLinkTick)
            {
                RunLinkEvent();
                nextLinkTick = ticks + LinkIntervalTicks;
            }

            OrbitalIndustryNetworkState network = Network;
            if (Operational && autoLaunch && launchCooldownTicks <= 0 && network != null &&
                autoLaunchTargetUnits > network.industrialUnits &&
                network.OrbitalMassEnergy >= Props.industrialUnitLaunchCost &&
                network.OrbitalMassEnergy - Props.industrialUnitLaunchCost >= autoLaunchReserve)
            {
                TryBeginIndustrialUnitLaunch(false);
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (TransferActive)
            {
                AstronomicalFabricationVisuals.DrawTransfer(parent.DrawPos, TransferProgress, BeamPhaseStart);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "SRA_OrbitalConsoleCommand".Translate(),
                defaultDesc = "SRA_OrbitalConsoleCommandDesc".Translate(),
                icon = ControlPanelIcon,
                action = delegate { Find.WindowStack.Add(new Dialog_SRAAstronomicalFabrications(this)); }
            };

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "dev: " + "SRA_OrbitalDevFillEnergy".Translate(),
                defaultDesc = "SRA_OrbitalDevFillEnergyDesc".Translate(),
                action = DebugFillMassEnergy
            };
            yield return new Command_Action
            {
                defaultLabel = "dev: " + "SRA_OrbitalDevFillUploadBuffer".Translate(),
                defaultDesc = "SRA_OrbitalDevFillUploadBufferDesc".Translate(),
                action = DebugFillUploadBuffer
            };
            yield return new Command_Action
            {
                defaultLabel = "dev: " + "SRA_OrbitalDevAddIndustrialUnit".Translate(),
                defaultDesc = "SRA_OrbitalDevAddIndustrialUnitDesc".Translate(),
                action = DebugAddIndustrialUnit
            };
            yield return new Command_Action
            {
                defaultLabel = "dev: " + "SRA_OrbitalDevRunLink".Translate(),
                defaultDesc = "SRA_OrbitalDevRunLinkDesc".Translate(),
                action = DebugRunLink
            };
            yield return new Command_Action
            {
                defaultLabel = "dev: " + "SRA_OrbitalDevCompleteTransfer".Translate(),
                defaultDesc = "SRA_OrbitalDevCompleteTransferDesc".Translate(),
                action = DebugCompleteTransfer
            };
        }

        public override string CompInspectStringExtra()
        {
            OrbitalIndustryNetworkState network = Network;
            string result = "SRA_OrbitalInspectUploadBuffer".Translate(
                OrbitalFabricationUtility.FormatMassEnergy(massEnergy),
                OrbitalFabricationUtility.FormatMassEnergy(StorageCapacity));
            if (network != null)
            {
                result += "\n" + "SRA_OrbitalInspectOrbitalEnergy".Translate(
                    OrbitalFabricationUtility.FormatMassEnergy(network.OrbitalMassEnergy),
                    OrbitalFabricationUtility.FormatMassEnergy(OrbitalMassEnergyCapacity));
                result += "\n" + "SRA_OrbitalInspectUnits".Translate(network.industrialUnits);
            }

            result += "\n" + "SRA_OrbitalInspectNextLink".Translate(NextLinkTicks.ToStringTicksToPeriod());
            if (OutputItemCount > 0)
            {
                result += "\n" + "SRA_OrbitalInspectOutput".Translate(OutputItemCount, OutputItemCapacity);
            }

            if (TransferActive)
            {
                result += "\n" + "SRA_OrbitalInspectDownload".Translate(TransferProgress.ToStringPercent());
            }

            return result;
        }

        public bool TryBeginScanUpload(out string reason)
        {
            return TryBeginScanUpload(false, out reason);
        }

        // allowOverflow is kept for existing callers. Uploads are now constrained by the local buffer,
        // because every converted unit must travel through the scheduled uplink.
        public bool TryBeginScanUpload(bool allowOverflow, out string reason)
        {
            reason = null;
            if (!Operational)
            {
                reason = "SRA_OrbitalNotOperational".Translate();
                return false;
            }

            CompOrbitalScannerTransporter scanner = ScannerComp;
            if (scanner == null || !scanner.HasLoadedItems)
            {
                reason = "SRA_OrbitalScannerEmpty".Translate();
                return false;
            }

            if (scanner.AnythingLeftToLoad)
            {
                reason = "SRA_OrbitalScannerLoading".Translate();
                return false;
            }

            return FinishScanUpload(out reason);
        }

        public bool TryStopLoadingAndUpload(bool allowOverflow, out string reason)
        {
            CancelPendingManifest();
            return TryBeginScanUpload(allowOverflow, out reason);
        }

        public bool TryBeginIndustrialUnitLaunch(bool sendMessage = true)
        {
            OrbitalIndustryNetworkState network = Network;
            network?.GenerateUntil(Find.TickManager.TicksGame, Props.orbitalGenerationIntervalTicks, Props.unitYieldPerInterval, OrbitalMassEnergyCapacity);
            if (!Operational || launchCooldownTicks > 0 || network == null ||
                network.industrialUnits >= OrbitalFabricationUtility.MaximumIndustrialUnits ||
                network.OrbitalMassEnergy < Props.industrialUnitLaunchCost)
            {
                if (sendMessage)
                {
                    Messages.Message(
                        network != null && network.industrialUnits >= OrbitalFabricationUtility.MaximumIndustrialUnits
                            ? "SRA_OrbitalIndustrialLimit".Translate()
                            : "SRA_OrbitalCannotLaunch".Translate(),
                        parent,
                        MessageTypeDefOf.RejectInput,
                        false);
                }

                return false;
            }

            if (!network.TrySpendOrbitalMassEnergy(Props.industrialUnitLaunchCost, OrbitalMassEnergyCapacity) || !network.TryAddIndustrialUnit())
            {
                if (sendMessage)
                {
                    Messages.Message("SRA_OrbitalCannotLaunch".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            launchCooldownTicks = Math.Max(0, Props.industrialUnitLaunchCooldownTicks);
            if (sendMessage)
            {
                Messages.Message("SRA_OrbitalLaunchCompleted".Translate(network.industrialUnits), parent, MessageTypeDefOf.PositiveEvent, false);
            }

            return true;
        }

        public void DebugFillMassEnergy()
        {
            OrbitalIndustryNetworkState network = Network;
            if (network != null)
            {
                network.AddOrbitalMassEnergy(OrbitalMassEnergyCapacity - network.OrbitalMassEnergy, OrbitalMassEnergyCapacity);
            }
        }

        public void DebugFillUploadBuffer()
        {
            massEnergy = StorageCapacity;
        }

        public void DebugAddIndustrialUnit()
        {
            Network?.TryAddIndustrialUnit();
        }

        public void DebugRunLink()
        {
            RunLinkEvent();
            nextLinkTick = (Find.TickManager?.TicksGame ?? 0) + LinkIntervalTicks;
        }

        public void DebugCompleteTransfer()
        {
            if (TransferActive)
            {
                transferTicksLeft = 1;
            }
        }

        public void ReplaceUploadManifest(IEnumerable<TransferableOneWay> transferables)
        {
            CompOrbitalScannerTransporter scanner = ScannerComp;
            if (scanner == null)
            {
                return;
            }

            List<TransferableOneWay> selected = (transferables ?? Enumerable.Empty<TransferableOneWay>())
                .Where(transferable => transferable != null && transferable.CountToTransfer > 0)
                .ToList();

            if (selected.Count == 0)
            {
                scanner.ClearPendingLoadPreservingContents(parent.Map);
                return;
            }

            if (!scanner.LoadingInProgressOrReadyToLaunch)
            {
                TransporterUtility.InitiateLoading(new List<CompTransporter> { scanner });
            }

            scanner.leftToLoad?.Clear();
            for (int i = 0; i < selected.Count; i++)
            {
                scanner.AddToTheToLoadList(selected[i], selected[i].CountToTransfer);
            }
        }

        public bool CancelPendingManifest()
        {
            CompOrbitalScannerTransporter scanner = ScannerComp;
            if (scanner == null || scanner.leftToLoad == null || scanner.leftToLoad.Count == 0)
            {
                return false;
            }

            return scanner.ClearPendingLoadPreservingContents(parent.Map);
        }

        public int PendingLoadCount
        {
            get
            {
                CompOrbitalScannerTransporter scanner = ScannerComp;
                if (scanner?.leftToLoad == null)
                {
                    return 0;
                }

                long count = 0L;
                for (int i = 0; i < scanner.leftToLoad.Count; i++)
                {
                    count += Math.Max(0, scanner.leftToLoad[i]?.CountToTransfer ?? 0);
                    if (count >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }

                return (int)count;
            }
        }

        public long GetLoadedScanYield()
        {
            CompOrbitalScannerTransporter scanner = ScannerComp;
            if (scanner?.innerContainer == null)
            {
                return 0L;
            }

            long result = 0L;
            foreach (Thing thing in scanner.innerContainer)
            {
                if (OrbitalFabricationUtility.CanScan(thing, out _))
                {
                    result = OrbitalFabricationUtility.SaturatingAdd(result, OrbitalFabricationUtility.ScanYield(thing, Props));
                }
            }

            return result;
        }

        public bool AddOrder(
            OrbitalFabricationBlueprint blueprint,
            int batchSize,
            QualityRange qualityRange,
            OrbitalOrderMode mode,
            int repeatCount,
            int targetCount,
            bool includeEquipped)
        {
            OrbitalIndustryNetworkState network = Network;
            if (blueprint?.thingDef == null || network == null || !network.Knows(blueprint.thingDef, blueprint.stuffDef))
            {
                return false;
            }

            OrbitalFabricationOrder order = new OrbitalFabricationOrder(AllocateOrderId(), blueprint.thingDef, blueprint.stuffDef)
            {
                batchSize = batchSize,
                mode = mode,
                remainingBatches = repeatCount,
                targetCount = targetCount,
                includeEquipped = includeEquipped
            };
            order.QualityRange = qualityRange;
            order.Normalize();
            orbitalOrders.Add(order);
            return true;
        }

        public bool UpdateOrder(
            OrbitalFabricationOrder order,
            int batchSize,
            QualityRange qualityRange,
            OrbitalOrderMode mode,
            int repeatCount,
            int targetCount,
            bool includeEquipped)
        {
            if (order == null || orbitalOrders == null || !orbitalOrders.Contains(order))
            {
                return false;
            }

            order.batchSize = batchSize;
            order.QualityRange = qualityRange;
            order.mode = mode;
            order.remainingBatches = repeatCount;
            order.targetCount = targetCount;
            order.includeEquipped = includeEquipped;
            order.Normalize();
            return true;
        }

        public bool RemoveOrder(OrbitalFabricationOrder order)
        {
            return order != null && orbitalOrders != null && orbitalOrders.Remove(order);
        }

        public bool MoveOrder(OrbitalFabricationOrder order, int direction)
        {
            if (order == null || direction == 0 || orbitalOrders == null)
            {
                return false;
            }

            int index = orbitalOrders.IndexOf(order);
            int destination = index + Math.Sign(direction);
            if (index < 0 || destination < 0 || destination >= orbitalOrders.Count)
            {
                return false;
            }

            orbitalOrders.RemoveAt(index);
            orbitalOrders.Insert(destination, order);
            return true;
        }

        public bool RemoveBlueprint(OrbitalFabricationBlueprint blueprint)
        {
            OrbitalIndustryNetworkState network = Network;
            if (blueprint?.thingDef == null || network == null || !network.RemoveBlueprint(blueprint.thingDef, blueprint.stuffDef))
            {
                return false;
            }

            orbitalOrders.RemoveAll(order => order != null && order.productDef == blueprint.thingDef && order.stuffDef == blueprint.stuffDef);
            return true;
        }

        public long GetOrderBatchCost(OrbitalFabricationOrder order)
        {
            return OrbitalFabricationUtility.BatchCost(order, Props);
        }

        public string GetOrderStatus(OrbitalFabricationOrder order)
        {
            if (order == null)
            {
                return string.Empty;
            }

            switch (order.lastStatus)
            {
                case OrbitalOrderStatus.Ready:
                    return "SRA_OrbitalOrderReady".Translate().ToString();
                case OrbitalOrderStatus.Suspended:
                    return "SRA_OrbitalOrderSuspended".Translate().ToString();
                case OrbitalOrderStatus.Complete:
                    return "SRA_OrbitalOrderComplete".Translate().ToString();
                case OrbitalOrderStatus.TargetSatisfied:
                    return "SRA_OrbitalOrderTargetSatisfied".Translate().ToString();
                case OrbitalOrderStatus.WaitingBandwidth:
                    return "SRA_OrbitalOrderWaitingBandwidth".Translate().ToString();
                case OrbitalOrderStatus.WaitingOrbitalEnergy:
                    return "SRA_OrbitalOrderWaitingEnergy".Translate().ToString();
                case OrbitalOrderStatus.WaitingOutputSpace:
                    return "SRA_OrbitalOrderWaitingOutput".Translate().ToString();
                case OrbitalOrderStatus.Downloading:
                    return "SRA_OrbitalOrderDownloading".Translate().ToString();
                default:
                    return "SRA_OrbitalOrderUnchecked".Translate().ToString();
            }
        }

        public void SetLinkIntervalSeconds(int seconds)
        {
            long ticks = Math.Max(1L, seconds) * 60L;
            SetLinkIntervalTicks(ticks > int.MaxValue ? int.MaxValue : (int)ticks);
        }

        public void SetLinkIntervalTicks(int ticks)
        {
            linkIntervalTicks = NormalizeLinkInterval(ticks);
            if (parent.Spawned)
            {
                nextLinkTick = Find.TickManager.TicksGame + linkIntervalTicks;
            }
        }

        private int AllocateOrderId()
        {
            int candidate = Math.Max(1, nextOrderId);
            while (orbitalOrders != null && orbitalOrders.Any(order => order != null && order.id == candidate))
            {
                candidate = candidate == int.MaxValue ? 1 : candidate + 1;
            }

            nextOrderId = candidate == int.MaxValue ? 1 : candidate + 1;
            return candidate;
        }

        private void EnsureLinkSchedule()
        {
            if (!parent.Spawned || nextLinkTick > 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            nextLinkTick = now + LinkIntervalTicks;
        }

        private int NormalizeLinkInterval(int ticks)
        {
            int min = Math.Max(1, Props.minimumLinkIntervalTicks);
            int max = Math.Max(min, Props.maximumLinkIntervalTicks);
            return Math.Max(min, Math.Min(ticks, max));
        }

        private bool FinishScanUpload(out string reason)
        {
            reason = null;
            CompOrbitalScannerTransporter scanner = ScannerComp;
            OrbitalIndustryNetworkState network = Network;
            if (scanner?.innerContainer == null || network == null)
            {
                reason = "SRA_OrbitalScannerMissing".Translate();
                return false;
            }

            List<Thing> things = scanner.innerContainer.ToList();
            long totalYield = 0L;
            for (int i = 0; i < things.Count; i++)
            {
                if (!OrbitalFabricationUtility.CanScan(things[i], out reason))
                {
                    return false;
                }

                totalYield = OrbitalFabricationUtility.SaturatingAdd(totalYield, OrbitalFabricationUtility.ScanYield(things[i], Props));
            }

            long freeStorage = Math.Max(0L, StorageCapacity - massEnergy);
            if (totalYield > freeStorage)
            {
                reason = "SRA_OrbitalStorageInsufficient".Translate(
                    OrbitalFabricationUtility.FormatMassEnergy(totalYield),
                    OrbitalFabricationUtility.FormatMassEnergy(freeStorage));
                return false;
            }

            int newBlueprints = 0;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (network.AddBlueprint(thing.def, thing.Stuff))
                {
                    newBlueprints++;
                }

                scanner.innerContainer.Remove(thing);
                thing.Destroy(DestroyMode.Vanish);
            }

            massEnergy = Math.Min(StorageCapacity, OrbitalFabricationUtility.SaturatingAdd(massEnergy, totalYield));
            scanner.TryRemoveLord(parent.Map);
            scanner.CleanUpLoadingVars(parent.Map);
            Messages.Message(
                "SRA_OrbitalScanCompleted".Translate(OrbitalFabricationUtility.FormatMassEnergy(totalYield), newBlueprints),
                parent,
                MessageTypeDefOf.PositiveEvent,
                false);
            return true;
        }

        private void RunLinkEvent()
        {
            lastLinkTick = Find.TickManager?.TicksGame ?? -1;
            lastUploadAmount = 0L;
            lastDownloadCost = 0L;
            lastDownloadOrderId = -1;

            if (!Operational)
            {
                return;
            }

            OrbitalIndustryNetworkState network = Network;
            if (network == null)
            {
                return;
            }

            network.GenerateUntil(Find.TickManager.TicksGame, Props.orbitalGenerationIntervalTicks, Props.unitYieldPerInterval, OrbitalMassEnergyCapacity);
            if (TransferActive)
            {
                return;
            }

            long remainingBandwidth = LinkBandwidthPerEvent;
            long batchCost;
            OrbitalFabricationOrder order = FindNextDownloadOrder(network, remainingBandwidth, out batchCost);
            if (order != null && network.TrySpendOrbitalMassEnergy(batchCost, OrbitalMassEnergyCapacity))
            {
                ReserveDownload(order);
                order.lastStatus = OrbitalOrderStatus.Downloading;
                remainingBandwidth -= batchCost;
                lastDownloadCost = batchCost;
                lastDownloadOrderId = order.id;

                if (downloadAnimationEnabled)
                {
                    BeginDownloadTransfer();
                }
                else
                {
                    if (downloadSoundEnabled && parent.Map != null)
                    {
                        SRA_DefOf.SRA_titan_laser_hit_01?.PlayOneShot(new TargetInfo(parent.Position, parent.Map, false));
                    }

                    DeliverPendingDownload();
                }
            }

            if (remainingBandwidth > 0L)
            {
                lastUploadAmount = UploadLocalMassEnergy(network, remainingBandwidth);
            }
        }

        // Queue ordering is strict: the first unsatisfied order is the active order. If it is too
        // expensive for this cadence, the player can lower its priority instead of silently bypassing it.
        private OrbitalFabricationOrder FindNextDownloadOrder(OrbitalIndustryNetworkState network, long bandwidth, out long batchCost)
        {
            batchCost = 0L;
            if (orbitalOrders == null)
            {
                return null;
            }

            for (int i = 0; i < orbitalOrders.Count; i++)
            {
                OrbitalFabricationOrder order = orbitalOrders[i];
                if (order == null || order.productDef == null)
                {
                    continue;
                }

                if (order.suspended)
                {
                    order.lastStatus = OrbitalOrderStatus.Suspended;
                    continue;
                }

                if (order.mode == OrbitalOrderMode.RepeatCount && order.remainingBatches <= 0)
                {
                    order.lastStatus = OrbitalOrderStatus.Complete;
                    continue;
                }

                int stock = OrbitalFabricationInventory.CountProducts(Map, OutputContainer, order);
                order.lastStockCount = stock;
                order.lastEvaluationTick = Find.TickManager.TicksGame;
                if (!order.NeedsDownload(stock))
                {
                    order.lastStatus = OrbitalOrderStatus.TargetSatisfied;
                    continue;
                }

                batchCost = GetOrderBatchCost(order);
                if (batchCost > bandwidth)
                {
                    order.lastStatus = OrbitalOrderStatus.WaitingBandwidth;
                    return null;
                }

                if (network.OrbitalMassEnergy < batchCost)
                {
                    order.lastStatus = OrbitalOrderStatus.WaitingOrbitalEnergy;
                    return null;
                }

                if (!CanStoreOutput(order.batchSize))
                {
                    order.lastStatus = OrbitalOrderStatus.WaitingOutputSpace;
                    return null;
                }

                order.lastStatus = OrbitalOrderStatus.Ready;
                return order;
            }

            return null;
        }

        private long UploadLocalMassEnergy(OrbitalIndustryNetworkState network, long availableBandwidth)
        {
            if (network == null || massEnergy <= 0L || availableBandwidth <= 0L)
            {
                return 0L;
            }

            long orbitalCapacity = OrbitalMassEnergyCapacity;
            long orbitalSpace = orbitalCapacity - network.OrbitalMassEnergy;
            long uploaded = Math.Min(massEnergy, Math.Min(availableBandwidth, Math.Max(0L, orbitalSpace)));
            if (uploaded <= 0L)
            {
                return 0L;
            }

            massEnergy -= uploaded;
            network.AddOrbitalMassEnergy(uploaded, orbitalCapacity);
            return uploaded;
        }

        private bool CanStoreOutput(int amount)
        {
            return OutputContainer?.innerContainer != null && amount > 0 &&
                   amount <= Math.Max(0L, (long)OutputItemCapacity - OutputItemCount);
        }

        private void ReserveDownload(OrbitalFabricationOrder order)
        {
            pendingDownloadOrderId = order.id;
            pendingDownloadProductDef = order.productDef;
            pendingDownloadStuffDef = order.stuffDef;
            pendingDownloadCount = order.batchSize;
            pendingDownloadQualityMin = order.qualityMin;
            pendingDownloadQualityMax = order.qualityMax;
            NormalizePendingDownloadQuality();
        }

        private float BeamPhaseStart
        {
            get
            {
                int beamTicks = Math.Max(30, transferTicksTotal / 3);
                return transferTicksTotal <= 0 ? 0.66f : Mathf.Clamp01(1f - beamTicks / (float)transferTicksTotal);
            }
        }

        private void BeginDownloadTransfer()
        {
            transferKind = OrbitalTransferKind.ItemDownload;
            transferTicksTotal = Math.Max(1, Props.downloadAnimationTicks);
            transferTicksLeft = transferTicksTotal;
            orbitalBeamAnimationStarted = false;
            beamSoundPlayed = false;
            windupSoundPlayed = false;
            BeginWindupPhaseAudio();
        }

        private void TickTransfer()
        {
            transferTicksLeft--;
            bool charging = TransferProgress < BeamPhaseStart;
            if (charging && parent.IsHashIntervalTick(12) && parent.Map != null)
            {
                float chargeProgress = Mathf.Clamp01(TransferProgress / Mathf.Max(0.05f, BeamPhaseStart));
                FleckMaker.ThrowLightningGlow(parent.DrawPos, parent.Map, Mathf.Lerp(0.45f, 1.1f, chargeProgress));
            }

            TryStartOrbitalBeamAnimation();
            if (transferTicksLeft > 0)
            {
                return;
            }

            ClearTransferState();
            DeliverPendingDownload();
        }

        private void TryStartOrbitalBeamAnimation()
        {
            if (!TransferActive || TransferProgress < BeamPhaseStart)
            {
                return;
            }

            BeginBeamPhaseAudio();
            if (orbitalBeamAnimationStarted)
            {
                return;
            }

            CompOrbitalBeam orbitalBeam = parent.GetComp<CompOrbitalBeam>();
            if (orbitalBeam == null)
            {
                return;
            }

            int duration = Math.Max(1, transferTicksLeft);
            int fadeOut = Math.Min(60, Math.Max(12, duration / 5));
            orbitalBeam.StartAnimation(duration, fadeOut, 0f);
            orbitalBeamAnimationStarted = true;
        }

        private void BeginWindupPhaseAudio()
        {
            if (!downloadSoundEnabled || windupSoundPlayed || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            SRA_DefOf.SRA_OrbitalWindup?.PlayOneShot(new TargetInfo(parent.Position, parent.Map, false));
            windupSoundPlayed = true;
        }

        private void BeginBeamPhaseAudio()
        {
            if (!downloadSoundEnabled || beamSoundPlayed || parent.Map == null)
            {
                return;
            }

            SRA_DefOf.SRA_titan_laser_hit_01?.PlayOneShot(new TargetInfo(parent.Position, parent.Map, false));
            beamSoundPlayed = true;
        }

        private void DeliverPendingDownload()
        {
            int delivered = 0;
            int requested = pendingDownloadCount;
            CompThingContainer output = OutputContainer;
            if (output?.innerContainer != null && pendingDownloadProductDef != null && pendingDownloadCount > 0 &&
                CanStoreOutput(pendingDownloadCount))
            {
                int remaining = pendingDownloadCount;
                while (remaining > 0)
                {
                    Thing product = ThingMaker.MakeThing(pendingDownloadProductDef, pendingDownloadStuffDef);
                    product.stackCount = Math.Min(remaining, product.def.stackLimit);
                    remaining -= product.stackCount;

                    if (OrbitalFabricationUtility.ProductSupportsQuality(pendingDownloadProductDef))
                    {
                        CompQuality quality = product.TryGetComp<CompQuality>();
                        if (quality != null)
                        {
                            quality.SetQuality(
                                (QualityCategory)Rand.RangeInclusive(pendingDownloadQualityMin, pendingDownloadQualityMax),
                                ArtGenerationContext.Colony);
                        }
                    }

                    if (!output.innerContainer.TryAdd(product))
                    {
                        product.Destroy(DestroyMode.Vanish);
                        break;
                    }

                    delivered += product.stackCount;
                }
            }

            int completedOrderId = pendingDownloadOrderId;
            if (delivered <= 0 && pendingDownloadProductDef != null)
            {
                Messages.Message("SRA_OrbitalDownloadOutputLost".Translate(), parent, MessageTypeDefOf.RejectInput, false);
            }

            OrbitalFabricationOrder completedOrder = orbitalOrders?.FirstOrDefault(order => order != null && order.id == completedOrderId);
            if (completedOrder != null && completedOrder.lastStatus == OrbitalOrderStatus.Downloading)
            {
                if (delivered == requested && requested > 0)
                {
                    completedOrder.NotifyBatchCompleted();
                    if (completedOrder.lastStockCount >= 0)
                    {
                        completedOrder.lastStockCount = (int)Math.Min(
                            int.MaxValue,
                            (long)completedOrder.lastStockCount + delivered);
                    }

                    completedOrder.lastStatus = completedOrder.mode == OrbitalOrderMode.RepeatCount && completedOrder.remainingBatches <= 0
                        ? OrbitalOrderStatus.Complete
                        : OrbitalOrderStatus.Ready;
                }
                else
                {
                    completedOrder.lastStatus = OrbitalOrderStatus.WaitingOutputSpace;
                }
            }

            ClearPendingDownload();
        }

        private void ClearTransferState()
        {
            transferKind = OrbitalTransferKind.None;
            transferTicksLeft = 0;
            transferTicksTotal = 0;
            orbitalBeamAnimationStarted = false;
            beamSoundPlayed = false;
            windupSoundPlayed = false;
        }

        private void ClearPendingDownload()
        {
            pendingDownloadOrderId = -1;
            pendingDownloadProductDef = null;
            pendingDownloadStuffDef = null;
            pendingDownloadCount = 0;
            pendingDownloadQualityMin = (int)QualityCategory.Awful;
            pendingDownloadQualityMax = (int)QualityCategory.Legendary;
        }

        private void NormalizePendingDownloadQuality()
        {
            int min = Math.Max((int)QualityCategory.Awful, Math.Min(pendingDownloadQualityMin, (int)QualityCategory.Legendary));
            int max = Math.Max((int)QualityCategory.Awful, Math.Min(pendingDownloadQualityMax, (int)QualityCategory.Legendary));
            pendingDownloadQualityMin = Math.Min(min, max);
            pendingDownloadQualityMax = Math.Max(min, max);
        }

    }

    public class PlaceWorker_Generator_SRA_Core : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef def,
            IntVec3 center,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            foreach (IntVec3 cell in map)
            {
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing existing = things[i];
                    if (existing != thingToIgnore && (existing.def == def || existing.def.entityDefToBuild == def))
                    {
                        return "MustNotGenerator_SRA_Core".Translate(def);
                    }
                }
            }

            return true;
        }
    }
}
