using Verse;

namespace SRA
{
    public abstract class HediffComp_StatusFlag : HediffComp
    {
        //If it was handled properly, it is added or removed by the tracker, thus it should not add itself to the tracker
        public bool handledImproperly = true;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (handledImproperly)
            {
                var t = parent.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 1800;
                Add(t);
            }
            handledImproperly = true;
        }

        public abstract void Add(int duration);

        public void Remove()
        {
            handledImproperly = false;
            Pawn.health.RemoveHediff(parent);
        }

        public void SetHandleStatus(bool b)
        {
            handledImproperly = b;
        }
    }

    public class HediffComp_InvincibilityFlag : HediffComp_StatusFlag
    {
        public override void Add(int duration)
        {
            SRAMod.Tracker.RegisterInvincibility(Pawn, duration);
        }

        public override void CompPostPostRemoved()
        {
            if (handledImproperly && SRAMod.Tracker.ValidateInvincibility(Pawn))
            {
                SRAMod.Tracker.DeregisterInvincibility(Pawn);
            }
        }
    }

    public class HediffComp_ResurrectabilityFlag : HediffComp_StatusFlag
    {
        public override void Add(int duration)
        {
            SRAMod.Tracker.RegisterResurrectability(Pawn, duration);
        }

        public override void CompPostPostRemoved()
        {
            if (handledImproperly && SRAMod.Tracker.ValidateResurrectability(Pawn))
            {
                SRAMod.Tracker.DeregisterInvincibility(Pawn);
            }
        }
    }
}
