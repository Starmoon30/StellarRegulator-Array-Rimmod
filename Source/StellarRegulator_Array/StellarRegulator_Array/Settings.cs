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
            NoseScar = false,
            EyeRender = true,
            AllowDuplicateSRA_SR = false;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref NoseScar, "NoseScar", defaultValue: false);
            Scribe_Values.Look(ref EyeRender, "EyeRender", defaultValue: true);
            Scribe_Values.Look(ref AllowDuplicateSRA_SR, "AllowDuplicateSRA_SR", defaultValue: false);
        }
    }

    public class SRAMod : Mod
    {
        public static Setting settings;

        public static GameComponent_SRA_SRUtilTracker Tracker => Current.Game?.GetComponent<GameComponent_SRA_SRUtilTracker>();

        public static bool NoseScar => settings.NoseScar;
        public static bool EyeRender => ModLister.GetActiveModWithIdentifier("nals.facialanimation") == null && settings.EyeRender;
        public static bool AllowDuplicateSRA_SR => settings.AllowDuplicateSRA_SR;

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
            listing_Standard.CheckboxLabeled("SRA_NoseScar_Title".Translate(), ref settings.NoseScar, "SRA_NoseScar_Desc".Translate());
            if (ModLister.GetActiveModWithIdentifier("nals.facialanimation") == null)
            {
                listing_Standard.CheckboxLabeled("SRA_EyeRender_Title".Translate(), ref settings.EyeRender, "SRA_EyeRender_Desc".Translate());
            }
            listing_Standard.CheckboxLabeled("SRA_AllowDuplicateSRA_SR_Title".Translate(), ref settings.AllowDuplicateSRA_SR, "SRA_AllowDuplicateSRA_SR_Desc".Translate());
            listing_Standard.Gap();
            if (Current.Game != null)
            {
                if (Tracker.SRA_SR != null)
                {
                    listing_Standard.Label("SRA_SRTracked".Translate(Tracker.SRA_SR.Name.ToStringFull, Tracker.SRA_SR.ThingID));
                }
                else
                {
                    listing_Standard.Label("SRA_SRNotTracked".Translate());

                }
            }

            listing_Standard.End();
        }

        public override string SettingsCategory()
        {
            return "SRA_Setting".Translate();
        }
    }
}
