using RimWorld;
using Verse;

namespace SRA
{
    public class CompWeaponHediffGiver : ThingComp
    {
        public CompProperties_WeaponHediffGiver Props => (CompProperties_WeaponHediffGiver)props;

        // 当装备被添加时调用
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (Props.hediff != null && pawn != null && !pawn.Dead)
            {
                // 添加 hediff 到装备者
                Hediff hediff = HediffMaker.MakeHediff(Props.hediff, pawn);
                pawn.health.AddHediff(hediff);
            }
        }

        // 当装备被移除时调用
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (Props.hediff != null && pawn != null && !pawn.Dead)
            {
                // 移除 hediff
                Hediff target = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
                if (target != null)
                {
                    pawn.health.RemoveHediff(target);
                }
            }
        }
    }

    public class CompProperties_WeaponHediffGiver : CompProperties
    {
        public HediffDef hediff;

        public CompProperties_WeaponHediffGiver()
        {
            compClass = typeof(CompWeaponHediffGiver);
        }
    }
}