using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SRA
{
    public sealed class OrbitalFabricationBlueprint : IExposable
    {
        public ThingDef thingDef;
        public ThingDef stuffDef;

        public OrbitalFabricationBlueprint()
        {
        }

        public OrbitalFabricationBlueprint(ThingDef thingDef, ThingDef stuffDef)
        {
            this.thingDef = thingDef;
            this.stuffDef = stuffDef;
        }

        public string Label => thingDef == null
            ? "SRA_OrbitalMissingBlueprint".Translate().ToString()
            : GenLabel.ThingLabel(thingDef, stuffDef, 1).CapitalizeFirst();

        public bool Matches(ThingDef product, ThingDef stuff)
        {
            return thingDef == product && stuffDef == stuff;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Defs.Look(ref stuffDef, "stuffDef");
        }
    }

    public enum OrbitalOrderMode
    {
        Forever,
        RepeatCount,
        TargetCount
    }

    public enum OrbitalOrderStatus
    {
        Unknown,
        Ready,
        Suspended,
        Complete,
        TargetSatisfied,
        WaitingBandwidth,
        WaitingOrbitalEnergy,
        WaitingOutputSpace,
        Downloading
    }

    // The orbital network owns its own queue and only samples colony stock when a scheduled
    // downlink is actually evaluated.
    public sealed class OrbitalFabricationOrder : IExposable
    {
        public int id;
        public ThingDef productDef;
        public ThingDef stuffDef;
        public int batchSize = 1;
        public OrbitalOrderMode mode = OrbitalOrderMode.Forever;
        public int remainingBatches = 1;
        public int targetCount = 1;
        public bool includeEquipped;
        public bool suspended;
        public int qualityMin = (int)QualityCategory.Awful;
        public int qualityMax = (int)QualityCategory.Legendary;

        // Cached telemetry is deliberately updated only by a link event, never by the UI.
        public int lastStockCount = -1;
        public int lastEvaluationTick = -1;
        public OrbitalOrderStatus lastStatus = OrbitalOrderStatus.Unknown;

        public OrbitalFabricationOrder()
        {
        }

        public OrbitalFabricationOrder(int id, ThingDef productDef, ThingDef stuffDef)
        {
            this.id = id;
            this.productDef = productDef;
            this.stuffDef = stuffDef;
        }

        public string Label => productDef == null
            ? "SRA_OrbitalMissingBlueprint".Translate().ToString()
            : GenLabel.ThingLabel(productDef, stuffDef, 1).CapitalizeFirst();

        public bool ProductSupportsQuality => OrbitalFabricationUtility.ProductSupportsQuality(productDef);

        public QualityRange QualityRange
        {
            get
            {
                NormalizeQualityRange();
                return new QualityRange((QualityCategory)qualityMin, (QualityCategory)qualityMax);
            }
            set
            {
                qualityMin = (int)value.min;
                qualityMax = (int)value.max;
                NormalizeQualityRange();
            }
        }

        public bool NeedsDownload(int currentStock)
        {
            if (suspended || productDef == null)
            {
                return false;
            }

            switch (mode)
            {
                case OrbitalOrderMode.RepeatCount:
                    return remainingBatches > 0;
                case OrbitalOrderMode.TargetCount:
                    return currentStock < Math.Max(1, targetCount);
                default:
                    return true;
            }
        }

        public void NotifyBatchCompleted()
        {
            if (mode == OrbitalOrderMode.RepeatCount && remainingBatches > 0)
            {
                remainingBatches--;
            }
        }

        public void Normalize()
        {
            batchSize = Math.Max(1, Math.Min(batchSize, OrbitalFabricationUtility.MaximumBatchSize));
            remainingBatches = Math.Max(0, Math.Min(remainingBatches, OrbitalFabricationUtility.MaximumBatchSize));
            targetCount = Math.Max(1, targetCount);
            NormalizeQualityRange();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Defs.Look(ref productDef, "productDef");
            Scribe_Defs.Look(ref stuffDef, "stuffDef");
            Scribe_Values.Look(ref batchSize, "batchSize", 1);
            Scribe_Values.Look(ref mode, "mode", OrbitalOrderMode.Forever);
            Scribe_Values.Look(ref remainingBatches, "remainingBatches", 1);
            Scribe_Values.Look(ref targetCount, "targetCount", 1);
            Scribe_Values.Look(ref includeEquipped, "includeEquipped", false);
            Scribe_Values.Look(ref suspended, "suspended", false);
            Scribe_Values.Look(ref qualityMin, "qualityMin", (int)QualityCategory.Awful);
            Scribe_Values.Look(ref qualityMax, "qualityMax", (int)QualityCategory.Legendary);
            Scribe_Values.Look(ref lastStockCount, "lastStockCount", -1);
            Scribe_Values.Look(ref lastEvaluationTick, "lastEvaluationTick", -1);
            Scribe_Values.Look(ref lastStatus, "lastStatus", OrbitalOrderStatus.Unknown);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Normalize();
            }
        }

        private void NormalizeQualityRange()
        {
            int min = Math.Max((int)QualityCategory.Awful, Math.Min(qualityMin, (int)QualityCategory.Legendary));
            int max = Math.Max((int)QualityCategory.Awful, Math.Min(qualityMax, (int)QualityCategory.Legendary));
            qualityMin = Math.Min(min, max);
            qualityMax = Math.Max(min, max);
        }
    }

    public sealed class OrbitalIndustryNetworkState : IExposable
    {
        public int networkId = -1;
        // pendingMassEnergy is retained as the save key used by the earlier implementation.
        // It now represents the authoritative orbital-side mass-energy balance.
        public int industrialUnits = 1;
        public long pendingMassEnergy;
        public int lastGenerationTick = -1;
        public List<OrbitalFabricationBlueprint> blueprints = new List<OrbitalFabricationBlueprint>();

        public OrbitalIndustryNetworkState()
        {
        }

        public OrbitalIndustryNetworkState(int networkId)
        {
            this.networkId = networkId;
            industrialUnits = 1;
            lastGenerationTick = Find.TickManager?.TicksGame ?? 0;
        }

        public long OrbitalMassEnergy => pendingMassEnergy;

        public long GetOrbitalMassEnergyCapacity(long capacityPerUnit)
        {
            return OrbitalFabricationUtility.SaturatingMultiply(
                Math.Max(1, industrialUnits),
                Math.Max(0L, capacityPerUnit));
        }

        public bool Knows(ThingDef thingDef, ThingDef stuffDef)
        {
            return blueprints.Any(blueprint => blueprint != null && blueprint.Matches(thingDef, stuffDef));
        }

        public bool AddBlueprint(ThingDef thingDef, ThingDef stuffDef)
        {
            if (thingDef == null || Knows(thingDef, stuffDef))
            {
                return false;
            }

            blueprints.Add(new OrbitalFabricationBlueprint(thingDef, stuffDef));
            return true;
        }

        public bool RemoveBlueprint(ThingDef thingDef, ThingDef stuffDef)
        {
            OrbitalFabricationBlueprint blueprint = blueprints.FirstOrDefault(item => item != null && item.Matches(thingDef, stuffDef));
            if (blueprint == null)
            {
                return false;
            }

            blueprints.Remove(blueprint);
            return true;
        }

        public bool TryAddIndustrialUnit()
        {
            if (industrialUnits >= OrbitalFabricationUtility.MaximumIndustrialUnits)
            {
                industrialUnits = OrbitalFabricationUtility.MaximumIndustrialUnits;
                return false;
            }

            industrialUnits++;
            return true;
        }

        public void ClampOrbitalMassEnergy(long capacity)
        {
            capacity = OrbitalFabricationUtility.ClampMassEnergy(capacity);
            pendingMassEnergy = Math.Min(OrbitalFabricationUtility.ClampMassEnergy(pendingMassEnergy), capacity);
        }

        public void AddOrbitalMassEnergy(long amount, long capacity)
        {
            ClampOrbitalMassEnergy(capacity);
            if (amount <= 0L)
            {
                return;
            }

            pendingMassEnergy = Math.Min(
                OrbitalFabricationUtility.ClampMassEnergy(capacity),
                OrbitalFabricationUtility.SaturatingAdd(pendingMassEnergy, amount));
        }

        public bool TrySpendOrbitalMassEnergy(long amount, long capacity)
        {
            ClampOrbitalMassEnergy(capacity);
            amount = OrbitalFabricationUtility.ClampMassEnergy(amount);
            if (amount <= 0L || pendingMassEnergy < amount)
            {
                return false;
            }

            pendingMassEnergy -= amount;
            return true;
        }

        public void GenerateUntil(int currentTick, int intervalTicks, long yieldPerUnit, long capacity)
        {
            ClampOrbitalMassEnergy(capacity);
            if (lastGenerationTick < 0)
            {
                lastGenerationTick = currentTick;
                return;
            }

            if (intervalTicks <= 0 || currentTick <= lastGenerationTick || industrialUnits <= 0 || yieldPerUnit <= 0)
            {
                return;
            }

            int elapsed = currentTick - lastGenerationTick;
            int cycles = elapsed / intervalTicks;
            if (cycles <= 0)
            {
                return;
            }

            lastGenerationTick += cycles * intervalTicks;
            AddOrbitalMassEnergy(
                OrbitalFabricationUtility.SaturatingMultiply(industrialUnits, yieldPerUnit, cycles),
                capacity);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref networkId, "networkId", -1);
            Scribe_Values.Look(ref industrialUnits, "industrialUnits", 0);
            Scribe_Values.Look(ref pendingMassEnergy, "pendingMassEnergy", 0L);
            Scribe_Values.Look(ref lastGenerationTick, "lastGenerationTick", -1);
            Scribe_Collections.Look(ref blueprints, "blueprints", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (blueprints == null)
                {
                    blueprints = new List<OrbitalFabricationBlueprint>();
                }

                blueprints = blueprints
                    .Where(blueprint => blueprint != null && blueprint.thingDef != null)
                    .GroupBy(blueprint => new { blueprint.thingDef, blueprint.stuffDef })
                    .Select(group => group.First())
                    .ToList();
                industrialUnits = Math.Max(1, Math.Min(industrialUnits, OrbitalFabricationUtility.MaximumIndustrialUnits));
                pendingMassEnergy = OrbitalFabricationUtility.ClampMassEnergy(pendingMassEnergy);
            }
        }
    }

    public sealed class WorldComponent_SRAOrbitalIndustry : WorldComponent
    {
        private List<OrbitalIndustryNetworkState> networks = new List<OrbitalIndustryNetworkState>();

        public WorldComponent_SRAOrbitalIndustry(World world)
            : base(world)
        {
        }

        public OrbitalIndustryNetworkState GetOrCreateNetwork(int networkId)
        {
            if (networks == null)
            {
                networks = new List<OrbitalIndustryNetworkState>();
            }

            OrbitalIndustryNetworkState network = networks.FirstOrDefault(state => state != null && state.networkId == networkId);
            if (network == null)
            {
                network = new OrbitalIndustryNetworkState(networkId);
                networks.Add(network);
            }

            return network;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref networks, "orbitalIndustryNetworks", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                networks = (networks ?? new List<OrbitalIndustryNetworkState>())
                    .Where(state => state != null)
                    .GroupBy(state => state.networkId)
                    .Select(group => group.First())
                    .ToList();
            }
        }
    }

    public class OrbitalFabricationExtension : DefModExtension
    {
        public bool scanAllowed = true;
        public long scanValueOverride = -1L;
        public long fabricationCostOverride = -1L;
    }

    public static class OrbitalFabricationInventory
    {
        // This mirrors vanilla's normal map inventory source (listerThings) and supplements it
        // with the base station's hidden output container, whose contents are not map-spawned.
        public static int CountProducts(Map map, CompThingContainer outputContainer, OrbitalFabricationOrder order)
        {
            if (map == null || order?.productDef == null)
            {
                return 0;
            }

            long count = 0L;
            List<Thing> mapThings = map.listerThings.ThingsOfDef(order.productDef);
            for (int i = 0; i < mapThings.Count; i++)
            {
                if (MatchesOrder(mapThings[i], order))
                {
                    count += mapThings[i].stackCount;
                    if (count >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }
            }

            if (outputContainer?.innerContainer != null)
            {
                foreach (Thing thing in outputContainer.innerContainer)
                {
                    if (MatchesOrder(thing, order))
                    {
                        count += thing.stackCount;
                        if (count >= int.MaxValue)
                        {
                            return int.MaxValue;
                        }
                    }
                }
            }

            if (order.includeEquipped)
            {
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn.equipment != null)
                    {
                        List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading;
                        for (int j = 0; j < equipment.Count; j++)
                        {
                            if (MatchesOrder(equipment[j], order))
                            {
                                count += equipment[j].stackCount;
                            }
                        }
                    }

                    if (pawn.apparel != null)
                    {
                        List<Apparel> apparel = pawn.apparel.WornApparel;
                        for (int j = 0; j < apparel.Count; j++)
                        {
                            if (MatchesOrder(apparel[j], order))
                            {
                                count += apparel[j].stackCount;
                            }
                        }
                    }

                    if (count >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }
            }

            return (int)Math.Min(count, int.MaxValue);
        }

        private static bool MatchesOrder(Thing thing, OrbitalFabricationOrder order)
        {
            if (thing == null || thing.def != order.productDef || thing.Stuff != order.stuffDef)
            {
                return false;
            }

            if (!order.ProductSupportsQuality)
            {
                return true;
            }

            CompQuality quality = thing.TryGetComp<CompQuality>();
            return quality != null && order.QualityRange.Includes(quality.Quality);
        }
    }

    public static class OrbitalFabricationUtility
    {
        // Leave headroom below long.MaxValue so every addition can remain deterministic and non-wrapping.
        public const long MaximumMassEnergy = 9000000000000000000L;
        public const int MaximumIndustrialUnits = 2000000000;
        public const int MaximumBatchSize = 1000000;

        public static bool CanScan(Thing thing, out string reason)
        {
            reason = null;
            if (thing == null || thing.Destroyed || thing.def == null)
            {
                reason = "SRA_OrbitalScanInvalid".Translate();
                return false;
            }

            OrbitalFabricationExtension extension = thing.def.GetModExtension<OrbitalFabricationExtension>();
            if (extension != null && !extension.scanAllowed)
            {
                reason = "SRA_OrbitalScanUnsupported".Translate(thing.LabelCap);
                return false;
            }

            if (thing is Pawn || thing is Corpse || thing is MinifiedThing ||
                thing.def.category != ThingCategory.Item || !thing.def.EverHaulable)
            {
                reason = "SRA_OrbitalScanUnsupported".Translate(thing.LabelCap);
                return false;
            }

            return true;
        }

        public static long ScanYield(Thing thing, CompProperties_Generator_SRA_Core props)
        {
            OrbitalFabricationExtension extension = thing.def.GetModExtension<OrbitalFabricationExtension>();
            double unitValue = extension != null && extension.scanValueOverride >= 0L
                ? extension.scanValueOverride
                : Math.Max(1f, thing.MarketValue) * props.scanEfficiency;
            return ClampPositiveLong(unitValue * Math.Max(1, thing.stackCount));
        }

        public static long FabricationUnitCost(ThingDef thingDef, ThingDef stuffDef, CompProperties_Generator_SRA_Core props)
        {
            if (thingDef == null)
            {
                return long.MaxValue;
            }

            OrbitalFabricationExtension extension = thingDef.GetModExtension<OrbitalFabricationExtension>();
            if (extension != null && extension.fabricationCostOverride >= 0L)
            {
                return Math.Max(1L, extension.fabricationCostOverride);
            }

            float marketValue = thingDef.GetStatValueAbstract(StatDefOf.MarketValue, stuffDef);
            return ClampPositiveLong(Math.Max(1f, marketValue) * props.fabricationCostFactor);
        }

        public static long BatchCost(OrbitalFabricationOrder order, CompProperties_Generator_SRA_Core props)
        {
            if (order == null)
            {
                return long.MaxValue;
            }

            long unitCost = FabricationUnitCost(order.productDef, order.stuffDef, props);
            return SaturatingMultiply(unitCost, Math.Max(1, Math.Min(order.batchSize, MaximumBatchSize)));
        }

        public static bool ProductSupportsQuality(ThingDef thingDef)
        {
            return thingDef != null && thingDef.HasComp(typeof(CompQuality));
        }

        public static long ClampMassEnergy(long value)
        {
            return Math.Max(0L, Math.Min(value, MaximumMassEnergy));
        }

        public static long SaturatingAdd(long left, long right)
        {
            left = ClampMassEnergy(left);
            if (right <= 0L)
            {
                return left;
            }

            return left >= MaximumMassEnergy - right ? MaximumMassEnergy : left + right;
        }

        public static long SaturatingMultiply(long first, long second)
        {
            if (first <= 0L || second <= 0L)
            {
                return 0L;
            }

            if (first > MaximumMassEnergy / second)
            {
                return MaximumMassEnergy;
            }

            return first * second;
        }

        public static long SaturatingMultiply(long first, long second, long third)
        {
            return SaturatingMultiply(SaturatingMultiply(first, second), third);
        }

        public static string FormatMassEnergy(long value)
        {
            value = ClampMassEnergy(value);
            if (value >= 1000000000L)
            {
                return (value / 1000000000d).ToString("0.##") + "G";
            }

            if (value >= 1000000L)
            {
                return (value / 1000000d).ToString("0.##") + "M";
            }

            if (value >= 1000L)
            {
                return (value / 1000d).ToString("0.##") + "K";
            }

            return value.ToString();
        }

        private static long ClampPositiveLong(double value)
        {
            if (double.IsNaN(value) || value <= 1d)
            {
                return 1L;
            }

            if (double.IsInfinity(value) || value >= MaximumMassEnergy)
            {
                return MaximumMassEnergy;
            }

            return ClampMassEnergy((long)Math.Ceiling(value));
        }
    }
}
