using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace SRA
{
    public class HediffCompProperties_TopTurret : HediffCompProperties
    {
        public HediffCompProperties_TopTurret()
        {
            this.compClass = typeof(HediffComp_TopTurret);
        }

        public ThingDef turretDef;

        public float angleOffset;

        public bool autoAttack = true;
    }

    [StaticConstructorOnStartup]
    public class HediffComp_TopTurret : HediffComp, IAttackTargetSearcher
    {
        public Thing Thing
        {
            get
            {
                return this.Pawn;
            }
        }

        private HediffCompProperties_TopTurret Props
        {
            get
            {
                return (HediffCompProperties_TopTurret)this.props;
            }
        }

        public Verb CurrentEffectiveVerb
        {
            get
            {
                return this.AttackVerb;
            }
        }

        public LocalTargetInfo LastAttackedTarget
        {
            get
            {
                return this.lastAttackedTarget;
            }
        }

        public int LastAttackTargetTick
        {
            get
            {
                return this.lastAttackTargetTick;
            }
        }

        public CompEquippable GunCompEq
        {
            get
            {
                return this.gun.TryGetComp<CompEquippable>();
            }
        }

        public Verb AttackVerb
        {
            get
            {
                return this.GunCompEq.PrimaryVerb;
            }
        }

        private bool WarmingUp
        {
            get
            {
                return this.burstWarmupTicksLeft > 0;
            }
        }

        private bool CanShoot
        {
            get
            {
                Pawn pawn;
                if ((pawn = (this.Pawn)) != null)
                {
                    if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
                    {
                        return false;
                    }
                    if (pawn.stances.stunner.Stunned)
                    {
                        return false;
                    }
                    if (this.TurretDestroyed)
                    {
                        return false;
                    }
                    if (pawn.IsColonyMechPlayerControlled && !this.fireAtWill)
                    {
                        return false;
                    }
                }
                CompCanBeDormant compCanBeDormant = this.Pawn.TryGetComp<CompCanBeDormant>();
                return compCanBeDormant == null || compCanBeDormant.Awake;
            }
        }

        public bool TurretDestroyed
        {
            get
            {
                Pawn pawn;
                return (pawn = (this.Pawn)) != null && this.AttackVerb.verbProps.linkedBodyPartsGroup != null && this.AttackVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable && PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(pawn.health.hediffSet, this.AttackVerb.verbProps.linkedBodyPartsGroup) <= 0f;
            }
        }

        private Material TurretMat
        {
            get
            {
                if (this.turretMat == null)
                {
                    this.turretMat = MaterialPool.MatFrom(this.Props.turretDef.graphicData.texPath);
                }
                return this.turretMat;
            }
        }

        public bool AutoAttack
        {
            get
            {
                return this.Props.autoAttack;
            }
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            this.MakeGun();
        }

        private void MakeGun()
        {
            this.gun = ThingMaker.MakeThing(this.Props.turretDef, null);
            this.UpdateGunVerbs();
        }

        private void UpdateGunVerbs()
        {
            List<Verb> allVerbs = this.gun.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this.Pawn;
                verb.castCompleteCallback = delegate ()
                {
                    this.burstCooldownTicksLeft = this.AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
                };
            }
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!this.CanShoot)
            {
                return;
            }
            if (this.currentTarget.IsValid)
            {
                this.curRotation = (this.currentTarget.Cell.ToVector3Shifted() - this.Pawn.DrawPos).AngleFlat() + this.Props.angleOffset;
            }
            this.AttackVerb.VerbTick();
            if (this.AttackVerb.state != VerbState.Bursting)
            {
                if (this.WarmingUp)
                {
                    this.burstWarmupTicksLeft--;
                    if (this.burstWarmupTicksLeft == 0)
                    {
                        this.AttackVerb.TryStartCastOn(this.currentTarget, false, true, false, true);
                        this.lastAttackTargetTick = Find.TickManager.TicksGame;
                        this.lastAttackedTarget = this.currentTarget;
                        return;
                    }
                }
                else
                {
                    if (this.burstCooldownTicksLeft > 0)
                    {
                        this.burstCooldownTicksLeft--;
                    }
                    if (this.burstCooldownTicksLeft <= 0 && this.Pawn.IsHashIntervalTick(10))
                    {
                        this.currentTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, null, 0f, 9999f);
                        if (this.currentTarget.IsValid)
                        {
                            this.burstWarmupTicksLeft = 1;
                            return;
                        }
                        this.ResetCurrentTarget();
                    }
                }
            }
        }

        private void ResetCurrentTarget()
        {
            this.currentTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<int>(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<int>(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_TargetInfo.Look(ref this.currentTarget, "currentTarget");
            Scribe_Deep.Look<Thing>(ref this.gun, "gun", Array.Empty<object>());
            Scribe_Values.Look<bool>(ref this.fireAtWill, "fireAtWill", true, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.gun == null)
                {
                    Log.Error("CompTurrentGun had null gun after loading. Recreating.");
                    this.MakeGun();
                    return;
                }
                this.UpdateGunVerbs();
            }
        }

        private const int StartShootIntervalTicks = 10;

        private static readonly CachedTexture ToggleTurretIcon = new CachedTexture("UI/Gizmos/ToggleTurret");

        public Thing gun;

        protected int burstCooldownTicksLeft;

        protected int burstWarmupTicksLeft;

        protected LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

        private bool fireAtWill = true;

        private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;

        private int lastAttackTargetTick;

        private float curRotation;

        [Unsaved(false)]
        public Material turretMat;
    }
}
