using HarmonyLib;
using Verse;
using RimWorld;

namespace SRA
{
    /// <summary>
    /// This CompProperties class is used in the XML defs to add the CompForceTargetable to a Thing.
    /// </summary>
    public class CompProperties_ForceTargetable : CompProperties
    {
        public CompProperties_ForceTargetable()
        {
            this.compClass = typeof(CompForceTargetable);
        }
    }

    /// <summary>
    /// A simple marker component. Any Building_TurretGun that has this component
    /// will be forcefully made targetable by players via a Harmony patch.
    /// </summary>
    public class CompForceTargetable : ThingComp
    {
        // This component doesn't need any specific logic.
        // Its mere presence on a turret is checked by the Harmony patch.
    }

    [HarmonyPatch(typeof(Building_TurretGun), "get_CanSetForcedTarget")]
    public static class Patch_Building_TurretGun_CanSetForcedTarget
    {
        /// <summary>
        /// Postfix patch to allow turrets with CompForceTargetable to be manually targeted.
        /// </summary>
        public static void Postfix(Building_TurretGun __instance, ref bool __result)
        {
            // If the result is already true, no need to do anything.
            if (__result)
            {
                return;
            }

            // Check if the turret has our marker component and belongs to the player.
            if (__instance.GetComp<CompForceTargetable>() != null && __instance.Faction == Faction.OfPlayer)
            {
                __result = true;
            }
        }
    }
}

