using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace SRA
{

    // 爆炸属性定义类
    public class MultiExplosionProperties
    {
        public float radius;
        public DamageDef damageDef;
        public int damageAmount = 1;
        public float armorPenetration = 1f;
        public SoundDef explosionSound;
        public bool explosionDamageFalloff = true;
        public EffecterDef explosionEffect;
        public int explosionEffectLifetimeTicks;
        public bool onlyAntiHostile = false;
    }

    public class MultiExplosiveExtension : DefModExtension
    {
        public List<MultiExplosionProperties> multiexplosions = new List<MultiExplosionProperties>();
    }
    public class Projectile_MultiExplosive : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var extension = this.def.GetModExtension<MultiExplosiveExtension>();
            if (extension != null && extension.multiexplosions != null && extension.multiexplosions.Count > 0)
            {
                foreach (var explosion in extension.multiexplosions)
                {
                    ExecuteExplosion(explosion);
                }
            }
            base.Impact(hitThing);

        }
        private void ExecuteExplosion(MultiExplosionProperties properties)
        {

            if (properties.explosionEffect != null)
            {
                Effecter effecter = properties.explosionEffect.Spawn();
                if (properties.explosionEffectLifetimeTicks != 0)
                {
                    Map.effecterMaintainer.AddEffecterToMaintain(effecter, Position.ToVector3().ToIntVec3(), properties.explosionEffectLifetimeTicks);
                }
                else
                {
                    effecter.Trigger(new TargetInfo(Position, Map, false), new TargetInfo(Position, Map, false), -1);
                    effecter.Cleanup();
                }
            }
            List<Thing> thingsIgnoredByExplosion = new List<Thing>();
            if (properties.onlyAntiHostile)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, properties.radius, true))
                {
                    if (!cell.InBounds(Map)) continue;
                    foreach (Thing thing in Map.thingGrid.ThingsListAt(cell))
                    {
                        // 敌我识别
                        if (!GenHostility.HostileTo(thing, launcher))
                        {
                            thingsIgnoredByExplosion.Add(thing);
                        }
                    }
                }
            }
            GenExplosion.DoExplosion(
                center: Position,
                map: Map, 
                radius: properties.radius,
                damType: properties.damageDef,
                instigator: launcher,
                damAmount: properties.damageAmount,
                armorPenetration: properties.armorPenetration,
                explosionSound: properties.explosionSound,
                weapon: equipmentDef,
                projectile: def,
                intendedTarget: intendedTarget.Thing,
                damageFalloff: properties.explosionDamageFalloff,
                ignoredThings: thingsIgnoredByExplosion
            );
        }
    }
}