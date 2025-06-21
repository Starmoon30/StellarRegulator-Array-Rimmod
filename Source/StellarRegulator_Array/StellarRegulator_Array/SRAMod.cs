using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static UnityEngine.Experimental.Rendering.RayTracingAccelerationStructure;

namespace SRA
{
	// Token: 0x0200001F RID: 31
	internal class SRAMod : Mod
	{
		public SRAMod(ModContentPack mcp) : base(mcp)
		{
			base.GetSettings<SRASettings>();
            new Harmony("DiZhuan.StellarRegulatorArray").PatchAll();
        }

		public override void WriteSettings()
		{
			base.WriteSettings();
			SRAMod.ApplySettings();
		}

		public static void ApplySettings()
		{
			//StatUtility.SetStatValueInList(ref GDDefOf.GD_HitArmor.stages[0].statFactors, StatDefOf.IncomingDamageFactor, 1.0f - (float)GDSettings.damageBlockRate/100);
			return;
		}

		public override string SettingsCategory()
		{
			return base.Content.Name;
        }
		public override void DoSettingsWindowContents(Rect inRect)
		{
            SRASettings.DoWindowContents(inRect);
		}
	}
}