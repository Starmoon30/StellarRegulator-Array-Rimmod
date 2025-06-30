using System;
using RimWorld;
using Verse;

namespace SRA
{
    // Token: 0x02000022 RID: 34
    public class CompUseEffect_ActivateMech : CompUseEffect
    {
        // Token: 0x17000017 RID: 23
        // (get) Token: 0x06000075 RID: 117 RVA: 0x00003D07 File Offset: 0x00001F07
        private CompProperties_UseEffect_ActivateMech Props
        {
            get
            {
                return this.props as CompProperties_UseEffect_ActivateMech;
            }
        }

        // Token: 0x06000076 RID: 118 RVA: 0x00003D14 File Offset: 0x00001F14
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            bool requireMechanitor = this.Props.requireMechanitor;
            if (requireMechanitor)
            {
                bool flag = MechanitorUtility.IsMechanitor(p);
                if (!flag)
                {
                    return "RequiresMechanitor".Translate();
                }
                int num = p.mechanitor.TotalBandwidth - p.mechanitor.UsedBandwidth;
                float statValueAbstract = this.Props.pawnKindDef.race.GetStatValueAbstract(StatDefOf.BandwidthCost, null);
                bool flag2 = (float)num < statValueAbstract;
                if (flag2)
                {
                    return "CannotControlMechNotEnoughBandwidth".Translate();
                }
            }
            return true;
        }

        // Token: 0x06000077 RID: 119 RVA: 0x00003DB4 File Offset: 0x00001FB4
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Pawn pawn = PawnGenerator.GeneratePawn(this.Props.pawnKindDef, usedBy.Faction);
            bool flag = MechanitorUtility.EverControllable(pawn) && MechanitorUtility.IsMechanitor(usedBy);
            if (flag)
            {
                Pawn overseer = pawn.GetOverseer();
                if (overseer != null)
                {
                    overseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, pawn);
                }
                usedBy.relations.AddDirectRelation(PawnRelationDefOf.Overseer, pawn);
            }
            GenPlace.TryPlaceThing(pawn, this.parent.PositionHeld, this.parent.MapHeld, ThingPlaceMode.Near, null, null, default(Rot4));
            pawn.Rotation = this.parent.Rotation;
            this.parent.DeSpawn(DestroyMode.Vanish);
            this.parent.Destroy(DestroyMode.Vanish);
        }
    }
}
