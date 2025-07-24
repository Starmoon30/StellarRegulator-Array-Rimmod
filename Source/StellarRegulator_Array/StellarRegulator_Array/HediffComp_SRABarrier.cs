using RimWorld;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Verse;

namespace SRA
{
    public class HediffCompProperties_SRABarrier : HediffCompProperties
    {
        // 添加公共访问器
        public float maxBarrier = 100f;
        public float DamageTakenMult = 1f;
        public float DamageTakenMax = 0f;
        public float DamageTakenReduce = 0f;
        public float regenRate = 5f;
        public float regenDelay = 3f;
        public float rechargeCooldown = 10f;
        public bool RemoveWhenDestroy = false;

        public HediffCompProperties_SRABarrier() => compClass = typeof(HediffComp_SRABarrier);
    }

    public class HediffComp_SRABarrier : HediffComp
    {
        private float currentBarrier;
        private int lastDamageTick = -1;
        private int brokenTick = -1;
        private bool isActive = true;

        public HediffCompProperties_SRABarrier Props => 
            (HediffCompProperties_SRABarrier)props;

        public float CurrentBarrier
        {
            get => currentBarrier;
            set => currentBarrier = Mathf.Clamp(value, 0, Props.maxBarrier);
        }

        public bool InCooldown => 
            brokenTick > 0 && Find.TickManager.TicksGame < brokenTick + 
            (Props.rechargeCooldown * GenTicks.TicksPerRealSecond);

        public bool CanAbsorb => 
            isActive && CurrentBarrier > 0 && !InCooldown;

        public override void CompPostMake() => 
            CurrentBarrier = Props.maxBarrier;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref currentBarrier, "currentBarrier");
            Scribe_Values.Look(ref lastDamageTick, "lastDamageTick", -1);
            Scribe_Values.Look(ref brokenTick, "brokenTick", -1);
            Scribe_Values.Look(ref isActive, "isActive", true);
        }

        public float GetCooldownSeconds()
        {
            return Props.rechargeCooldown - 
                (Find.TickManager.TicksGame - brokenTick) / (float)GenTicks.TicksPerRealSecond;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!Pawn.Spawned || Pawn.Dead) return;
            
            if (Pawn.IsHashIntervalTick(GenTicks.TicksPerRealSecond))
            {

                if (InCooldown)
                {
                    // 冷却结束后重新激活屏障
                    if (Find.TickManager.TicksGame > brokenTick +
                        (Props.rechargeCooldown * GenTicks.TicksPerRealSecond))
                    {
                        isActive = true;
                        brokenTick = -1;
                    }
                    return;
                }

                if (CurrentBarrier >= Props.maxBarrier)
                {
                    return;
                }

                bool pastRegenDelay = lastDamageTick < 0 || 
                    Find.TickManager.TicksGame > lastDamageTick + 
                    (Props.regenDelay * GenTicks.TicksPerRealSecond);

                if (pastRegenDelay)
                {
                    CurrentBarrier += Props.regenRate;
                }
            }
        }

        public void AbsorbDamage(ref DamageInfo dinfo)
        {
            if (!CanAbsorb) return;
            if (Props.DamageTakenMax > 0)
            {
                dinfo.SetAmount(Mathf.Min(dinfo.Amount, Props.DamageTakenMax));
            }
            if (Props.DamageTakenReduce > 0)
            {
                dinfo.SetAmount(dinfo.Amount - Props.DamageTakenReduce);
            }
            float damageToAbsorb = dinfo.Amount;
            float absorbed;
            if (Props.DamageTakenMult <= 0)
            {
                absorbed = damageToAbsorb;
            }
            else
            {
                absorbed = Mathf.Min(CurrentBarrier / Props.DamageTakenMult, damageToAbsorb);
                CurrentBarrier -= absorbed * Props.DamageTakenMult;
            }
            if (Props.RemoveWhenDestroy)
            {
                parent.Severity = 0;
            }
            
            dinfo.SetAmount(dinfo.Amount - absorbed);
            lastDamageTick = Find.TickManager.TicksGame;
            
            if (CurrentBarrier <= 0.01f)
            {
                CurrentBarrier = 0;
                brokenTick = Find.TickManager.TicksGame;
                isActive = false;
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                return "SRA_BarrierTipExtra".Translate(
                Props.maxBarrier.ToString(),
                Props.regenRate.ToString(),
                Props.regenDelay.ToString(),
                Props.rechargeCooldown.ToString(),
                Props.DamageTakenMult.ToString(),
                Props.DamageTakenMax.ToString(),
                Props.DamageTakenReduce.ToString()
                );
            }
        }
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Find.Selector.SingleSelectedThing == Pawn)
            {
                yield return new SRABarrierGizmo(this);
            }
        }
    }
}