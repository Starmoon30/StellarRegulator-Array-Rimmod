using System;
using Verse;

namespace SRA
{
    public class CompProperties_Generator_SRA_Core : CompProperties
    {
        public ThingDef product;
        public int itemsPerCycle = 1;
        public float baseProgressPerCycle = 10f;
        public float progressPerFacilityPerCycle = 2.5f;

        public long baseStorageCapacity = 250000L;
        public long storageCapacityPerFacility = 50000L;
        // Legacy receive fields are left in the schema so existing Defs/save data remain readable.
        // The redesign uses the scalable link bandwidth fields below instead of mass-energy downlinks.
        public long baseReceiveLimit = 25000L;
        public long receiveLimitPerFacility = 10000L;
        public int orbitalGenerationIntervalTicks = 60000;
        public long unitYieldPerInterval = 2500L;
        public long orbitalMassEnergyCapacityPerUnit = 250000L;

        public int defaultLinkIntervalTicks = 60000;
        public int minimumLinkIntervalTicks = 60;
        public int maximumLinkIntervalTicks = 3600000;
        public long baseBandwidthPerDefaultInterval = 25000L;
        public long bandwidthPerFacilityPerDefaultInterval = 10000L;
        public int outputItemCapacity = 5000;
        public int downloadAnimationTicks = 180;
        public string controlPanelIconPath;

        public long industrialUnitLaunchCost = 50000L;
        public int industrialUnitLaunchTicks = 600;
        public int industrialUnitLaunchCooldownTicks = 2500;
        public int uploadChargeTicks = 120;
        public int uploadBeamTicks = 45;

        public int fabricationIntervalTicks = 300;
        public float scanEfficiency = 0.75f;
        public float fabricationCostFactor = 1.25f;

        public CompProperties_Generator_SRA_Core() =>
            compClass = typeof(CompGenerator_SRA_Core);
    }

    public class CompProperties_OrbitalScannerTransporter : RimWorld.CompProperties_Transporter
    {
        public CompProperties_OrbitalScannerTransporter()
        {
            compClass = typeof(CompOrbitalScannerTransporter);
        }
    }
}
