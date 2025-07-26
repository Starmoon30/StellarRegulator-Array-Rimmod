
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
        public float explosionRadius = 1.9f;
        public int explosionDamage = 0;
        public float explosionArmorPenetration = 0f;
        public DamageDef damageDef = DamageDefOf.Bullet;
        public SoundDef explosionSound = SRA_DefOf.SRABulle_Railgun_Explosion;
    }
    public class Projectile_SRA_Railgun : Projectile
    {
        // 穿透配置
        private int penetrationsLeft;
        private int lastPenetrationTick;
        private Thing LasthitThing;

        // 获取XML配置
        private SRA_RailgunProjectileExtension PenExt =>
            def.GetModExtension<SRA_RailgunProjectileExtension>();
        

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (PenExt.explosionDamage <= 0)
            {
                PenExt.explosionDamage = def.projectile.GetDamageAmount(equipment);
            }
            if (PenExt.explosionArmorPenetration <= 0)
            {
                PenExt.explosionArmorPenetration = def.projectile.GetArmorPenetration(equipment);
            }
            Map map = base.Map;
            IntVec3 position = base.Position;
            Thing launcher = base.launcher;

            // 检查是否为预定目标或需要穿透
            bool isIntendedTarget =
                intendedTarget.Thing == null ||
                hitThing == intendedTarget.Thing || hitThing == null;

            bool canPenetrate =
                PenExt != null &&
                penetrationsLeft > 0 &&
                (Find.TickManager.TicksGame - lastPenetrationTick) >= PenExt.penetrationDelayTicks;
            if (hitThing != LasthitThing)
            {
                // 生成穿透爆炸
                GenExplosion.DoExplosion(
                    center: position,
                    map: map,
                    radius: PenExt.explosionRadius,
                    damType: PenExt.damageDef,
                    instigator: launcher,
                    damAmount: PenExt.explosionDamage,
                    armorPenetration: PenExt.explosionArmorPenetration,
                    explosionSound: PenExt.explosionSound,
                    weapon: equipmentDef,
                    projectile: def,
                    intendedTarget: intendedTarget.Thing
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
            if (PenExt != null)
            {
                penetrationsLeft = PenExt.maxPenetrations;
                lastPenetrationTick = -PenExt.penetrationDelayTicks; // 允许立即穿透
            }
        }
    }
}