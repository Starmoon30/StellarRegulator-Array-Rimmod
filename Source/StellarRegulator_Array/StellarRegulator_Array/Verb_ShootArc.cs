using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SRA
{
    public class VerbProperties_Arc : VerbProperties
    {
        public DamageDef damageDef;

        public float EMPDamageAmount = -1f;

        public int damageAmount = -1;

        public float armorPenetration = -1f;

        public float affectedAngle;

        public bool isConductible = false;

        public int conductNum;

        public bool conductFriendly = false;

        public bool conductHostile = true;
    }

    public class Verb_ShootArc : Verb
    {
        private VerbProperties_Arc Props
        {
            get
            {
                return (VerbProperties_Arc)this.verbProps;
            }
        }

        private int damageAmount
        {
            get
            {
                if (this.Props.damageAmount > 0)
                {
                    return this.Props.damageAmount;
                }
                if (this.verbProps.beamDamageDef != null)
                {
                    return this.verbProps.beamDamageDef.defaultDamage;
                }
                Log.ErrorOnce(string.Format("Verb_ShootArc on {0} has no damageAmount and no beamDamageDef.", (this.caster != null) ? this.caster.def.defName : "null"), this.GetHashCode());
                return 0;
            }
        }

        private float armorPenetration
        {
            get
            {
                if (this.Props.armorPenetration > 0f)
                {
                    return this.Props.armorPenetration;
                }
                if (this.verbProps.beamDamageDef != null)
                {
                    return this.verbProps.beamDamageDef.defaultArmorPenetration;
                }
                return 0f;
            }
        }

        public override void WarmupComplete()
        {
            this.TryCastShot();
        }

        protected override bool TryCastShot()
        {
            this.MakeExplosion();
            bool flag = this.verbProps.soundCast != null;
            bool flag3 = flag;
            if (flag3)
            {
                this.verbProps.soundCast.PlayOneShot(new TargetInfo(this.caster.Position, this.caster.MapHeld, false));
            }
            bool flag2 = this.verbProps.soundCastTail != null;
            bool flag4 = flag2;
            if (flag4)
            {
                this.verbProps.soundCastTail.PlayOneShotOnCamera(this.caster.Map);
            }
            return true;
        }

        private bool IsTargetImmobile(LocalTargetInfo target)
        {
            Thing thing = target.Thing;
            Pawn pawn = thing as Pawn;
            return pawn != null && !pawn.Downed && pawn.GetPosture() == PawnPosture.Standing;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            bool flag = this.caster == null || !this.caster.Spawned;
            bool flag2 = flag;
            return !flag2 && (targ == this.caster || this.CanHitTargetFrom(this.caster.Position, targ));
        }

        protected void MakeExplosion()
        {
            Pawn casterPawn = this.CasterPawn;
            if (!casterPawn.Spawned || this.Props == null)
            {
                return;
            }

            // 技能学习逻辑 (只在目标是站立Pawn时)
            if (this.currentTarget.Thing is Pawn targetPawn && !targetPawn.Downed && targetPawn.GetPosture() == PawnPosture.Standing && casterPawn.skills != null)
            {
                casterPawn.skills.Learn(SkillDefOf.Shooting, 250f * verbProps.AdjustedFullCycleTime(this, casterPawn), false, false);
            }

            float weaponDamageMultiplier = base.EquipmentSource.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier, true, -1);
            int damageMultiplier = this.GetDamageAmount(weaponDamageMultiplier, null);
            float armorPenetrationMultiplier = this.GetArmorPenetration(weaponDamageMultiplier, null);

            // 总是先收集范围内的Pawn，为后续决策做准备
            List<IntVec3> cells = Verb_ShootArc.circularSectorCellsStartedCaster(casterPawn.Position, casterPawn.Map, this.currentTarget.Cell, this.Props.range, this.Props.affectedAngle, false).ToList<IntVec3>();
            HashSet<IntVec3> hashSet = this.HashSetConverter(cells);
            this.pawnConduct.Add(casterPawn);

            foreach (IntVec3 cell in hashSet)
            {
                List<Thing> list = casterPawn.Map.thingGrid.ThingsListAt(cell);
                for (int num = list.Count - 1; num >= 0; num--)
                {
                    if (list[num] is Pawn p)
                    {
                        bool isFriendly = p.Faction != null && casterPawn.Faction != null && !p.Faction.HostileTo(casterPawn.Faction);
                        if ((!this.Props.conductFriendly && isFriendly) || (!this.Props.conductHostile && p.HostileTo(casterPawn)))
                        {
                            continue;
                        }
                        bool isInvalidPosture = p.GetPosture() != PawnPosture.Standing && this.currentTarget.Thing != p;
                        if (!isInvalidPosture)
                        {
                            this.pawnConduct.Add(p);
                        }
                    }
                }
            }

            // 决策：如果设为导电模式且有至少一个传导目标，则进行链式攻击
            if (this.Props.isConductible && this.pawnConduct.Count > 1)
            {
                for (int i = 0; i < this.Props.conductNum && i < this.pawnConduct.Count - 1; i++)
                {
                    if (this.Props.EMPDamageAmount > 0f)
                    {
                        this.TargetTakeDamage(casterPawn, this.pawnConduct[i + 1], DamageDefOf.EMP, this.Props.EMPDamageAmount, -1f);
                    }
                    this.TargetTakeDamage(casterPawn, this.pawnConduct[i + 1], this.Props.damageDef, (float)damageMultiplier, armorPenetrationMultiplier);
                    if (this.verbProps.beamMoteDef != null)
                    {
                        MoteMaker.MakeInteractionOverlay(this.verbProps.beamMoteDef, new TargetInfo(this.pawnConduct[i].Position, this.caster.Map, false), new TargetInfo(this.pawnConduct[i + 1].Position, this.caster.Map, false));
                    }
                }
            }
            // 否则（非导电模式，或没有传导目标），执行一次普通的单体攻击
            else
            {
                Thing primaryTarget = this.currentTarget.Thing;
                if (primaryTarget != null)
                {
                    float angle = (primaryTarget.Position - this.caster.Position).AngleFlat;
                    DamageInfo dinfo = new DamageInfo(this.Props.damageDef, (float)damageMultiplier, armorPenetrationMultiplier, angle, this.caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, this.currentTarget.Thing);
                    primaryTarget.TakeDamage(dinfo);
                }

                // 无论是否命中，都显示视觉效果
                if (this.verbProps.beamMoteDef != null)
                {
                    MoteMaker.MakeInteractionOverlay(this.verbProps.beamMoteDef, new TargetInfo(this.caster.Position, this.caster.Map, false), new TargetInfo(this.currentTarget.Cell, this.caster.Map, false));
                }
            }
            this.pawnConduct.Clear();
        }

        private void DoExplosion(Pawn casterPawn, int damAmount, float armorPenetration, FloatRange? affectedAngle)
        {
            GenExplosion.DoExplosion(
                center: casterPawn.Position,
                map: this.caster.MapHeld,
                radius: this.verbProps.range,
                damType: this.Props.damageDef,
                instigator: casterPawn, // Corrected
                damAmount: damAmount,
                armorPenetration: armorPenetration,
                explosionSound: null,
                weapon: this.CasterPawn.equipment?.Primary?.def, // Safety check
                projectile: null,
                intendedTarget: this.currentTarget.Thing, // Corrected
                postExplosionSpawnThingDef: null, // Simplified
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: null,
                postExplosionGasRadiusOverride: null,
                postExplosionGasAmount: 0,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null,
                affectedAngle: affectedAngle,
                doVisualEffects: true,
                propagationSpeed: 0.6f,
                excludeRadius: 0f,
                doSoundEffects: false,
                postExplosionSpawnThingDefWater: null,
                screenShakeFactor: 1f,
                flammabilityChanceCurve: null,
                overrideCells: null,
                postExplosionSpawnSingleThingDef: null,
                preExplosionSpawnSingleThingDef: null
            );
        }


        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            bool isValid = target.IsValid;
            bool flag = isValid;
            if (flag)
            {
                IntVec3 position = this.caster.Position;
                float num = Mathf.Atan2(-(float)(target.Cell.z - position.z), (float)(target.Cell.x - position.x)) * 57.29578f;
                Verb_ShootArc.RenderPredictedAreaOfEffect(this.caster.Position, this.Props.range, this.verbProps.explosionRadiusRingColor, new FloatRange(num - this.Props.affectedAngle, num + this.Props.affectedAngle));
            }
        }

        public static void RenderPredictedAreaOfEffect(IntVec3 loc, float radius, Color color, FloatRange affectedAngle)
        {
            bool flag = affectedAngle.min < -180f || affectedAngle.max > 180f;
            bool flag2 = flag;
            List<IntVec3> cellsSum;
            if (flag2)
            {
                DamageWorker worker = DamageDefOf.Bomb.Worker;
                Map currentMap = Find.CurrentMap;
                FloatRange? affectedAngle2 = new FloatRange?(new FloatRange(Verb_ShootArc.AngleWrapped(affectedAngle.min), 180f));
                List<IntVec3> cells = worker.ExplosionCellsToHit(loc, currentMap, radius, null, null, affectedAngle2).ToList<IntVec3>();
                DamageWorker worker2 = DamageDefOf.Bomb.Worker;
                Map currentMap2 = Find.CurrentMap;
                affectedAngle2 = new FloatRange?(new FloatRange(-180f, Verb_ShootArc.AngleWrapped(affectedAngle.max)));
                List<IntVec3> cells2 = worker2.ExplosionCellsToHit(loc, currentMap2, radius, null, null, affectedAngle2).ToList<IntVec3>();
                cellsSum = cells.Concat(cells2).ToList<IntVec3>();
            }
            else
            {
                DamageWorker worker3 = DamageDefOf.Bomb.Worker;
                Map currentMap3 = Find.CurrentMap;
                FloatRange? affectedAngle3 = new FloatRange?(affectedAngle);
                cellsSum = worker3.ExplosionCellsToHit(loc, currentMap3, radius, null, null, affectedAngle3).ToList<IntVec3>();
            }
            GenDraw.DrawFieldEdges(cellsSum, color, null);
        }

        public static float AngleWrapped(float angle)
        {
            while (angle > 180f)
            {
                angle -= 360f;
            }
            while (angle < -180f)
            {
                angle += 360f;
            }
            return (angle == 180f) ? -180f : angle;
        }

        private static IEnumerable<IntVec3> circularSectorCellsStartedCaster(IntVec3 center, Map map, IntVec3 target, float radius, float angle, bool useCenter = false)
        {
            float num = Mathf.Atan2(-(float)(target.z - center.z), (float)(target.x - center.x)) * 57.29578f;
            FloatRange affectedAngle = new FloatRange(num - angle, num + angle);
            bool flag = affectedAngle.min < -180f || affectedAngle.max > 180f;
            bool flag2 = flag;
            List<IntVec3> cellsSum;
            if (flag2)
            {
                DamageWorker worker = DamageDefOf.Bomb.Worker;
                FloatRange? affectedAngle2 = new FloatRange?(new FloatRange(Verb_ShootArc.AngleWrapped(affectedAngle.min), 180f));
                List<IntVec3> cells = worker.ExplosionCellsToHit(center, map, radius, null, null, affectedAngle2).ToList<IntVec3>();
                DamageWorker worker2 = DamageDefOf.Bomb.Worker;
                affectedAngle2 = new FloatRange?(new FloatRange(-180f, Verb_ShootArc.AngleWrapped(affectedAngle.max)));
                List<IntVec3> cells2 = worker2.ExplosionCellsToHit(center, map, radius, null, null, affectedAngle2).ToList<IntVec3>();
                cellsSum = cells.Concat(cells2).ToList<IntVec3>();
            }
            else
            {
                DamageWorker worker3 = DamageDefOf.Bomb.Worker;
                FloatRange? affectedAngle3 = new FloatRange?(affectedAngle);
                cellsSum = worker3.ExplosionCellsToHit(center, map, radius, null, null, affectedAngle3).ToList<IntVec3>();
            }
            return cellsSum;
        }

        protected virtual HashSet<IntVec3> HashSetConverter(IEnumerable<IntVec3> points)
        {
            HashSet<IntVec3> hashSet = new HashSet<IntVec3>();
            bool flag = points.Any<IntVec3>();
            bool flag2 = flag;
            if (flag2)
            {
                foreach (IntVec3 point in points)
                {
                    hashSet.Add(point);
                }
            }
            return hashSet;
        }

        private void TargetTakeDamage(Pawn caster, Pawn target, DamageDef damageDef, float damageAmount, float armorPenetration = -1f)
        {
            bool flag = caster == null || target == null;
            bool flag2 = flag;
            if (flag2)
            {
                Log.Error("TargetTakeDamage has null caster or target");
            }
            else
            {
                float angleFlat = (this.currentTarget.Cell - caster.Position).AngleFlat;
                BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(caster, target, this.currentTarget.Thing, base.EquipmentSource.def, null, null);
                DamageInfo dinfo = new DamageInfo(damageDef, damageAmount, armorPenetration, angleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, this.currentTarget.Thing, true, true, QualityCategory.Normal, true);
                target.TakeDamage(dinfo).AssociateWithLog(log);
            }
        }

        public int GetDamageAmount(float weaponDamageMultiplier, StringBuilder explanation = null)
        {
            int num = this.damageAmount;
            bool flag = explanation != null;
            bool flag3 = flag;
            if (flag3)
            {
                explanation.AppendLine("StatsReport_BaseValue".Translate() + ": " + num.ToString());
                explanation.Append("StatsReport_QualityMultiplier".Translate() + ": " + weaponDamageMultiplier.ToStringPercent());
            }
            num = Mathf.RoundToInt((float)num * weaponDamageMultiplier);
            bool flag2 = explanation != null;
            bool flag4 = flag2;
            if (flag4)
            {
                explanation.AppendLine();
                explanation.AppendLine();
                explanation.Append("StatsReport_FinalValue".Translate() + ": " + num.ToString());
            }
            return num;
        }

        public float GetArmorPenetration(float weaponDamageMultiplier, StringBuilder explanation = null)
        {
            float num = this.armorPenetration;
            bool flag = num < 0f;
            bool flag4 = flag;
            if (flag4)
            {
                num = (float)this.damageAmount * 0.015f;
            }
            bool flag2 = explanation != null;
            bool flag5 = flag2;
            if (flag5)
            {
                explanation.AppendLine("StatsReport_BaseValue".Translate() + ": " + num.ToStringPercent());
                explanation.AppendLine();
                explanation.Append("StatsReport_QualityMultiplier".Translate() + ": " + weaponDamageMultiplier.ToStringPercent());
            }
            num *= weaponDamageMultiplier;
            bool flag3 = explanation != null;
            bool flag6 = flag3;
            if (flag6)
            {
                explanation.AppendLine();
                explanation.AppendLine();
                explanation.Append("StatsReport_FinalValue".Translate() + ": " + num.ToStringPercent());
            }
            return num;
        }

        public List<Pawn> pawnConduct = new List<Pawn>();
    }
}

