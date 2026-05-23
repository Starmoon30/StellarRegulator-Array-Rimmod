using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace SRA
{

    public class Setting : ModSettings
    {
        public bool
            AngledSRAWall = false;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AngledSRAWall, "AngledSRAWall", defaultValue: false);
        }
    }

    public class SRAMod : Mod
    {
        public static Setting settings;
        public static bool AngledSRAWall => settings.AngledSRAWall;

        public SRAMod(ModContentPack content)
            : base(content)
        {
            settings = GetSettings<Setting>();
            new Harmony("DiZhuan.StellarRegulatorArray").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.ColumnWidth = (inRect.width - 17f) / 2f;
            listing_Standard.Begin(inRect);
            Text.Font = GameFont.Small;
            listing_Standard.GapLine();
            listing_Standard.CheckboxLabeled("SRA_AngledSRAWall_Title".Translate(), ref settings.AngledSRAWall, "SRA_AngledSRAWall_Desc".Translate());
            listing_Standard.Gap();

            listing_Standard.End();
        }

        public override string SettingsCategory()
        {
            return "SRA_Setting".Translate();
        }
    }
}
