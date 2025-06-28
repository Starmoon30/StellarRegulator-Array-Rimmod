using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SRA
{
    public class PredefinedCharacterParmDef : Def
    {
        public BodyTypeDef forcedBodyTypeDef;

        public ThingDef raceDef, weaponDef, weaponStuffDef;

        public PawnKindDef basePawnKindDef;

        public FactionDef fixedFaction;

        public Color hairColor, skinColor, eyeColor;

        public HairDef forcedHairDef;

        public List<AbilityDef> abilities = new List<AbilityDef>();

        public float fixedAge;

        public Gender gender;

        public HeadTypeDef headType;

        public List<ThingDef> forcedApparels = new List<ThingDef>();

        public List<BackstoryDef> fixedChildBackStories = new List<BackstoryDef>(), fixedAdultBackStories = new List<BackstoryDef>();

        public List<TraitRequirement> traits = new List<TraitRequirement>();

        public bool useFactionIdeo, overrideFactionLeader;

        [MustTranslate]
        public string firstName, nickname, lastname, title;

        public TattooDef faceTattoo, bodyTattoo;

        public List<ScarParm> scars = new List<ScarParm>();
        public List<HediffParm> hediffs = new List<HediffParm>();

        #region Xenotype
        public List<GeneDef> endoGenes = new List<GeneDef>(), xenoGenes = new List<GeneDef>(), removeGenes = new List<GeneDef>();

        public XenotypeDef baseXenotype;
        #endregion

        #region Ideo
        public List<PreceptDef> disallowedPreceptDefs = new List<PreceptDef>();

        public List<MemeDef> disallowedMemes = new List<MemeDef>(), forcedMemes = new List<MemeDef>();

        public List<StyleCategoryDef> styles = new List<StyleCategoryDef>();
        #endregion

        public override IEnumerable<string> ConfigErrors()
        {
            if (basePawnKindDef == null)
            {
                basePawnKindDef = PawnKindDefOf.Colonist;
            }
            if (baseXenotype == null)
            {
                baseXenotype = XenotypeDefOf.Baseliner;
            }
            return base.ConfigErrors();
        }
    }

    public class ScarParm
    {
        public DamageDef damage;

        public BodyPartDef bodyPart;

        public float damageAmount;
    }
    public class HediffParm
    {
        public HediffDef hediff;

        public BodyPartDef bodyPart;

        public string partCustomLabel;
    }
}
