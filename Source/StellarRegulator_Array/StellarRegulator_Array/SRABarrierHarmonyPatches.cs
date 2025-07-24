using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SRA
{
    [StaticConstructorOnStartup]
    public static class SRABarrierHarmonyPatches
    {
        static SRABarrierHarmonyPatches()
        {
            try
            {
                var harmony = new Harmony("rimworld.SRA.SRAbarriersystem");
                harmony.Patch(
                    original: AccessTools.Method(typeof(Pawn), nameof(Pawn.PreApplyDamage)),
                    prefix: new HarmonyMethod(typeof(SRABarrierHarmonyPatches), nameof(PreApplyDamage_Prefix))
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[SRA Barrier] Failed to apply Harmony patches: {ex}");
            }
        }

        public static void PreApplyDamage_Prefix(Pawn __instance, ref DamageInfo dinfo)
        {
            try
            {
                if (__instance == null || __instance.Dead || __instance.health == null) return;

                foreach (Hediff hediff in __instance.health.hediffSet.hediffs)
                {
                    if (hediff.TryGetComp<HediffComp_SRABarrier>() is HediffComp_SRABarrier barrier)
                    {
                        barrier.AbsorbDamage(ref dinfo);
                        if (dinfo.Amount <= 0.001f) return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SRA Barrier] Error in damage absorption: {ex}");
            }
        }
    }
}