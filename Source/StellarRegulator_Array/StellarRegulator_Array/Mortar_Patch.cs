using System;
using HarmonyLib;
using Verse;
using RimWorld;

namespace SRA
{
	[HarmonyPatch(typeof(Building_TurretGun), "get_CanSetForcedTarget")]
	public static class Mortar_Patch
	{
		public static bool Prefix(Building_TurretGun __instance, ref bool __result)
		{
			if ((__instance.def.defName == "SRATurret_L_Particle_Launcher") && __instance.Faction != null && __instance.Faction == Faction.OfPlayer)
			{
				__result = true;
				return false;
			}
			return true;
		}
	}
}

