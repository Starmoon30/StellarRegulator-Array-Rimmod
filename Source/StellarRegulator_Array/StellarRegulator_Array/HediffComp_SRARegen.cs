using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SRA
{
    public class HediffCompProperties_SRARegen : HediffCompProperties
    {
        // Token: 0x060000DC RID: 220 RVA: 0x00005D44 File Offset: 0x00003F44
        public HediffCompProperties_SRARegen()
        {
            this.compClass = typeof(HediffComp_SRARegen);
        }

        // Token: 0x0400005A RID: 90
        public int checkInterval = 60;

        // Token: 0x0400005B RID: 91
        public float healPerSecond = 1f;

    }
    public class HediffComp_SRARegen : HediffComp
    {
        private HediffCompProperties_SRARegen Props
        {
            get
            {
                return this.props as HediffCompProperties_SRARegen;
            }
        }
        private float healPerSecond
        {
            get
            {
                return this.Props.healPerSecond;
            }
        }
        private float healPerCycle
        {
            get
            {
                return this.healPerSecond * (float)this.Props.checkInterval / 60f;
            }
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            bool shouldCheck = base.Pawn.IsHashIntervalTick(this.Props.checkInterval);
            if (shouldCheck)
            {
                HediffComp_SRARegen.hediffsToHeal.Clear();
                HediffComp_SRARegen.ignoreParts.Clear();
                foreach (Hediff hediff in base.Pawn.health.hediffSet.hediffs.InRandomOrder(null))
                {
                    bool isAddedPart = hediff is Hediff_AddedPart;
                    if (isAddedPart)
                    {
                        foreach (BodyPartRecord item in hediff.Part.GetPartAndAllChildParts())
                        {
                            HediffComp_SRARegen.ignoreParts.Add(item);
                        }
                    }
                    else
                    {
                        bool isInjury = hediff is Hediff_Injury;
                        if (isInjury)
                        {
                            HediffComp_SRARegen.hediffsToHeal.Add(hediff);
                        }
                        else
                        {
                            Hediff_MissingPart hediff_MissingPart = hediff as Hediff_MissingPart;
                            bool isValidMissingPart = hediff_MissingPart != null && base.Pawn.health.hediffSet.GetMissingPartFor(hediff_MissingPart.Part.parent) == null && !base.Pawn.health.hediffSet.GetInjuredParts().Contains(hediff_MissingPart.Part.parent);
                            if (isValidMissingPart)
                            {
                                HediffComp_SRARegen.hediffsToHeal.Add(hediff);
                            }
                        }
                    }
                }
                bool anyHediffsToHeal = HediffComp_SRARegen.hediffsToHeal.Any<Hediff>();
                if (anyHediffsToHeal)
                {
                    float healingLeft = this.healPerCycle;
                    int remainingHediffCount = HediffComp_SRARegen.hediffsToHeal.Count;
                    foreach (Hediff hediff2 in HediffComp_SRARegen.hediffsToHeal)
                    {
                        bool noHealingLeft = healingLeft <= 0f;
                        if (noHealingLeft)
                        {
                            break;
                        }
                        bool isInjuryToHeal = hediff2 is Hediff_Injury;
                        if (isInjuryToHeal)
                        {
                            float healAmountForThisInjury = Math.Min(healingLeft / (float)remainingHediffCount, hediff2.Severity);
                            hediff2.Severity -= healAmountForThisInjury;
                            bool shouldRemoveInjury = hediff2.Severity <= 0f;
                            if (shouldRemoveInjury)
                            {
                                base.Pawn.health.RemoveHediff(hediff2);
                            }
                            healingLeft -= healAmountForThisInjury;
                            remainingHediffCount--;
                        }
                        else
                        {
                            Hediff_MissingPart hediff_MissingPart2 = hediff2 as Hediff_MissingPart;
                            bool isMissingPart = hediff_MissingPart2 != null;
                            if (isMissingPart)
                            {
                                bool isIgnoredPart = HediffComp_SRARegen.ignoreParts.Contains(hediff_MissingPart2.Part);
                                if (!isIgnoredPart)
                                {
                                    float maxHealth = hediff_MissingPart2.Part.def.GetMaxHealth(base.Pawn);
                                    float healAmountForThisMissingPart = healingLeft / (float)remainingHediffCount;
                                    bool healingNotEnoughForWholePart = healAmountForThisMissingPart < maxHealth;
                                    if (healingNotEnoughForWholePart)
                                    {
                                        BodyPartRecord part = hediff_MissingPart2.Part;
                                        Hediff hediff3 = HediffMaker.MakeHediff(hediff_MissingPart2.lastInjury ?? HediffDefOf.Cut, base.Pawn, part);
                                        base.Pawn.health.RemoveHediff(hediff2);
                                        base.Pawn.health.AddHediff(hediff3, part, null, null);
                                        hediff3.Severity = maxHealth - healAmountForThisMissingPart;
                                        healingLeft -= healAmountForThisMissingPart;
                                    }
                                    else
                                    {
                                        healingLeft -= maxHealth;
                                        base.Pawn.health.RemoveHediff(hediff2);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                return "SRA_RegenTipExtra".Translate(
                this.healPerSecond.ToString()
                );
            }
        }
        private static List<Hediff> hediffsToHeal = new List<Hediff>();

        private static List<BodyPartRecord> ignoreParts = new List<BodyPartRecord>();

    }
}
