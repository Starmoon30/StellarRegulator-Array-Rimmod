using HarmonyLib;
using RimWorld;
using Verse;
namespace SRA
{
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class SRAResurrectionBeacon_Patch
    {
        static bool Prefix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            // 检查是否绑定有效信标
            foreach (Map map in Find.Maps)
            {
                foreach (SRABuilding_ResurrectionBeacon beacon in
                    map.listerBuildings.AllBuildingsColonistOfClass<SRABuilding_ResurrectionBeacon>())
                {
                    if (beacon.IsBound(__instance))
                    {
                        beacon.TriggerResurrection(__instance);
                        return false; // 阻止原始死亡事件
                    }
                }
            }
            return true; // 继续原始死亡流程
        }
    }
}
