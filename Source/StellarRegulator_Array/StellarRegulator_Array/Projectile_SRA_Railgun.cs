
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SRA
{
    public class SRA_RailgunProjectileExtension : DefModExtension
    {
        // 穿透配置
        public int maxPenetrations = 1;
        public int penetrationDelayTicks = 15; // 默认0.25秒(60tick/秒)

        // 爆炸配置
        public float explosionRadius = 1.5f;
        public int explosionDamage = 0;
        public float explosionArmorPenetration = 0f;
        public DamageDef damageDef = DamageDefOf.Bullet;
        public SoundDef explosionSound = SRA_DefOf.SRABulle_Railgun_Explosion;

        public HediffDef explosionHediff;
        public float explosionHediffSeverity = 0f;

    }
    public class Projectile_SRA_Railgun : Projectile
    {
        // 穿透配置
        private int penetrationsLeft;
        private int lastPenetrationTick;
        private Thing LasthitThing;

        // 获取XML配置
        private SRA_RailgunProjectileExtension ProjectileExt =>
            def.GetModExtension<SRA_RailgunProjectileExtension>();
        

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (ProjectileExt.explosionDamage <= 0)
            {
                ProjectileExt.explosionDamage = def.projectile.GetDamageAmount(equipment);
            }
            if (ProjectileExt.explosionArmorPenetration <= 0)
            {
                ProjectileExt.explosionArmorPenetration = def.projectile.GetArmorPenetration(equipment);
            }
            Map map = base.Map;
            IntVec3 position = base.Position;
            Thing launcher = base.launcher;

            // 检查是否为预定目标或需要穿透
            bool isIntendedTarget =
                intendedTarget.Thing == null ||
                hitThing == intendedTarget.Thing || hitThing == null;

            bool canPenetrate =
                ProjectileExt != null &&
                penetrationsLeft > 0 &&
                (Find.TickManager.TicksGame - lastPenetrationTick) >= ProjectileExt.penetrationDelayTicks;
            if (hitThing != LasthitThing)
            {
                List<Thing> thingsIgnoredByExplosion = new List<Thing>();
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, ProjectileExt.explosionRadius, true))
                {
                    if (!cell.InBounds(map)) continue;
                    foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
                    {
                        // 敌我识别
                        if (thing != hitThing && !GenHostility.HostileTo(thing, launcher))
                        {
                            thingsIgnoredByExplosion.Add(thing);
                        }
                    }
                }
                // 生成穿透爆炸
                GenExplosion.DoExplosion(
                    center: position,
                    map: map,
                    radius: ProjectileExt.explosionRadius,
                    damType: ProjectileExt.damageDef,
                    instigator: launcher,
                    damAmount: ProjectileExt.explosionDamage,
                    armorPenetration: ProjectileExt.explosionArmorPenetration,
                    explosionSound: ProjectileExt.explosionSound,
                    weapon: equipmentDef,
                    projectile: def,
                    intendedTarget: intendedTarget.Thing,
                    ignoredThings: thingsIgnoredByExplosion
                );
                LasthitThing = hitThing;

                // 施加爆炸范围内的 Hediff
                if (ProjectileExt?.explosionHediff != null)
                {
                    ApplyHediffInExplosionRadius(position, map);
                }
            }
            // 非目标且满足穿透条件
            if (!isIntendedTarget && canPenetrate)
            {
                // 更新穿透状态
                penetrationsLeft--;
                lastPenetrationTick = Find.TickManager.TicksGame;

                // 继续飞行（不销毁）
                return;
            }

            // 命中目标或无法穿透时执行标准命中
            base.Impact(hitThing, blockedByShield);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref penetrationsLeft, "penetrationsLeft", 0);
            Scribe_Values.Look(ref lastPenetrationTick, "lastPenetrationTick", 0);
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(
                launcher,
                origin,
                usedTarget,
                intendedTarget,
                hitFlags,
                preventFriendlyFire,
                equipment,
                targetCoverDef
            );

            // 初始化穿透计数器
            if (ProjectileExt != null)
            {
                penetrationsLeft = ProjectileExt.maxPenetrations;
                lastPenetrationTick = -ProjectileExt.penetrationDelayTicks; // 允许立即穿透
            }
        }

        // 给单个目标施加 Hediff
        private void ApplyHediffToTarget(Pawn target, HediffDef hediffDef, float severity = -1f)
        {
            Hediff hediff = HediffMaker.MakeHediff(hediffDef, target);
            // 设置严重程度（如果配置了）
            if (severity > 0)
            {
                hediff.Severity = Mathf.Clamp(severity, 0, hediffDef.maxSeverity);
            }
            // 施加 Hediff
            target.health.AddHediff(hediff);
            // 检查 Pawn 是否已有该 Hediff
            if (target.health.hediffSet.HasHediff(hediffDef))
            {
                // 增加现有 Hediff 的严重程度
                Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                existing.Severity += ProjectileExt.explosionHediffSeverity;
            }
            else
            {
                // 施加新 Hediff
                Hediff newHediff = HediffMaker.MakeHediff(hediffDef, target);
                newHediff.Severity = ProjectileExt.explosionHediffSeverity;
                target.health.AddHediff(newHediff);
            }
        }
        // 在爆炸半径内施加 Hediff
        private void ApplyHediffInExplosionRadius(IntVec3 center, Map map)
        {
            if (map == null) return;

            // 获取爆炸半径内的所有单元格
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, ProjectileExt.explosionRadius, true))
            {
                if (!cell.InBounds(map)) continue;
                // 获取单元格内的所有 Pawn
                List<Thing> things = cell.GetThingList(map);
                foreach (Thing thing in things)
                {
                    if (thing is Pawn pawn && GenHostility.HostileTo(pawn, launcher))
                    {
                        ApplyHediffToTarget(pawn, ProjectileExt.explosionHediff, ProjectileExt.explosionHediffSeverity);
                    }
                }
            }
        }
        // 检查目标是否对发射者友好
        private bool IsFriendlyToLauncher(Thing target, Faction launcherFaction)
        {
            if (target == null || launcherFaction == null) return false;

            // 1. 检查动物和野生动物
            if (target is Pawn pawn)
            {
                // 玩家动物视为友方
                if (pawn.Faction.IsPlayer && pawn.RaceProps.Animal)
                    return true;
                else return false;
            }
            // 2. 检查派系关系
            if (target.Faction == null) return false;

            // 3. 同派系视为友方
            if (target.Faction == launcherFaction) return true;

            // 4. 检查盟友关系
            FactionRelation relation = launcherFaction.RelationWith(target.Faction, false);
            if (relation != null)
            {
                // 盟友
                if (relation.kind == FactionRelationKind.Ally) return true;
            }
            // 5. 检查特殊关系（如奴隶、囚犯）
            if (target is Pawn targetPawn)
            {
                // 奴隶视为友方
                if (targetPawn.IsSlaveOfColony) return true;

                // 囚犯视为敌方
                if (targetPawn.IsPrisonerOfColony) return false;
            }

            return false;
        }
    }
}