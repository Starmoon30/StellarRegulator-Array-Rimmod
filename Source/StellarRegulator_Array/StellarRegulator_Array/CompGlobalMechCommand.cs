using HarmonyLib;
using Verse;

namespace SRA
{
    /// <summary>
    /// A CompProperties class that, when added to a Pawn's ThingDef, grants them global command range.
    /// </summary>
    public class CompProperties_GlobalMechCommand : CompProperties
    {
        public CompProperties_GlobalMechCommand()
        {
            this.compClass = typeof(CompGlobalMechCommand);
        }
    }

    /// <summary>
    /// The actual Comp that does nothing but act as a marker.
    /// </summary>
    public class CompGlobalMechCommand : ThingComp
    {
        // This component doesn't need to do anything. Its presence is what matters.
    }

    [HarmonyPatch(typeof(MechanitorUtility), "InMechanitorCommandRange")]
    public static class MechanitorUtility_InMechanitorCommandRange_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn mech, ref bool __result)
        {
            // Check if the pawn's def has the CompProperties_GlobalMechCommand
            if (mech.def.HasComp(typeof(CompGlobalMechCommand)))
            {
                __result = true; // Grant global command range
                return false;    // Skip original method
            }

            return true; // Execute original method
        }
    }
}