
using System.Collections.Generic;
using System.Linq;
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
            if (ProjectileExt == null)
            {
                base.Impact(hitThing, blockedByShield);
                return;
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
                BattleLogEntry_RangedImpact battleLogEntry_RangedImpact = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
                Find.BattleLog.Add(battleLogEntry_RangedImpact);
                Pawn pawn;
                bool instigatorGuilty = (pawn = (launcher as Pawn)) == null || !pawn.Drafted;
                List<Thing> thingsIgnoredByExplosion = new List<Thing>();
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, ProjectileExt.explosionRadius, true))
                {
                    if (!cell.InBounds(map)) continue;

                    // 创建事物列表的副本以避免枚举时修改集合
                    List<Thing> thingsInCell = map.thingGrid.ThingsListAt(cell).ToList();

                    foreach (Thing thing in thingsInCell)
                    {
                        // 检查物体是否已被销毁
                        if (thing.Destroyed) continue;

                        // 敌我识别
                        if (thing != hitThing && !GenHostility.HostileTo(thing, launcher))
                        {
                            thingsIgnoredByExplosion.Add(thing);
                        }
                        else
                        {
                            if (def.projectile.extraDamages != null)
                            {
                                foreach (ExtraDamage extraDamage in def.projectile.extraDamages)
                                {
                                    if (Rand.Chance(extraDamage.chance))
                                    {
                                        DamageInfo dinfo2 = new DamageInfo(extraDamage.def, extraDamage.amount, extraDamage.AdjustedArmorPenetration(), ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
                                        thing.TakeDamage(dinfo2).AssociateWithLog(battleLogEntry_RangedImpact);
                                    }
                                }
                            }
                        }
                    }
                }
                // 施加爆炸范围内的 Hediff
                if (ProjectileExt?.explosionHediff != null)
                {
                    ApplyHediffInExplosionRadius(position, map);
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
            if (ProjectileExt.explosionDamage <= 0)
            {
                ProjectileExt.explosionDamage = def.projectile.GetDamageAmount(equipment);
            }
            if (ProjectileExt.explosionArmorPenetration <= 0)
            {
                ProjectileExt.explosionArmorPenetration = def.projectile.GetArmorPenetration(equipment);
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
            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
            {
                // 增加现有 Hediff 的严重程度
                existing.Severity += severity;
            }
            else
            {
                // 施加新 Hediff
                Hediff newHediff = HediffMaker.MakeHediff(hediffDef, target);
                newHediff.Severity = severity;
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
    }
}