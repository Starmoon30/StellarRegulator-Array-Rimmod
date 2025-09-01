using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    public class Thing_ExcaliburBeam : Mote
    {
        public IntVec3 targetCell;
        public Pawn caster;
        public ThingDef weaponDef;
        public float damageAmount;
        public float armorPenetration;
        public float pathWidth;
        public DamageDef damageDef;

        // Burst shot support
        public int burstShotsTotal = 1;
        public int currentBurstShot = 0;

        // Path cells for this burst
        private List<IntVec3> currentBurstCells;

        private int ticksToDetonate = 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetCell, "targetCell");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Values.Look(ref damageAmount, "damageAmount");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref pathWidth, "pathWidth");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref burstShotsTotal, "burstShotsTotal", 1);
            Scribe_Values.Look(ref currentBurstShot, "currentBurstShot", 0);
        }

        public void StartStrike(List<IntVec3> allCells, int burstIndex, int totalBursts)
        {
            if (allCells == null || !allCells.Any())
            {
                Destroy();
                return;
            }

            currentBurstCells = new List<IntVec3>(allCells);
            currentBurstShot = burstIndex;
            burstShotsTotal = totalBursts;

            // Add a small delay before detonation for visual effect
            ticksToDetonate = 10; // 10 ticks delay before detonation
        }

        protected override void TimeInterval(float deltaTime)
        {
            base.TimeInterval(deltaTime);
            if (ticksToDetonate > 0)
            {
                ticksToDetonate--;
                if (ticksToDetonate == 0)
                {
                    Detonate();
                }
            }
        }

        private void Detonate()
        {
            if (currentBurstCells == null || !currentBurstCells.Any() || Map == null)
            {
                Destroy();
                return;
            }

            // Create a copy of the list to avoid modification during iteration
            List<IntVec3> cellsToDetonate = new List<IntVec3>(currentBurstCells);

            // Clear the current burst cells to prevent reuse
            currentBurstCells.Clear();

            foreach (IntVec3 cell in cellsToDetonate)
            {
                if (cell.InBounds(Map))
                {
                    // Apply explosion effect, but ignore the caster
                    List<Thing> ignoredThings = new List<Thing>();
                    if (caster != null)
                    {
                        ignoredThings.Add(caster);
                    }

                    DamageDef explosionDamageType = damageDef ?? DamageDefOf.Bomb;

                    // Create explosion parameters with more precise settings
                    GenExplosion.DoExplosion(
                        center: cell,
                        map: Map,
                        radius: 1.2f, // Slightly larger radius for better visual effect
                        damType: explosionDamageType,
                        instigator: caster,
                        damAmount: (int)damageAmount,
                        armorPenetration: armorPenetration,
                        explosionSound: SRA_DefOf.SRA_star_eater_weapon_hit_01,
                        weapon: null,
                        projectile: null,
                        intendedTarget: null,
                        postExplosionSpawnThingDef: null,
                        postExplosionSpawnChance: 0f,
                        postExplosionSpawnThingCount: 1,
                        postExplosionGasType: null,
                        applyDamageToExplosionCellsNeighbors: true, // Apply damage to neighbor cells
                        preExplosionSpawnThingDef: null,
                        preExplosionSpawnChance: 0f,
                        preExplosionSpawnThingCount: 1,
                        chanceToStartFire: 0.1f, // Small chance to start fire
                        damageFalloff: true, // Add damage falloff
                        direction: null,
                        ignoredThings: ignoredThings,
                        affectedAngle: null,
                        doVisualEffects: true,
                        propagationSpeed: 0.5f, // Add some propagation speed for visual effect
                        screenShakeFactor: 0.3f, // Add screen shake
                        doSoundEffects: true,
                        postExplosionSpawnThingDefWater: null,
                        flammabilityChanceCurve: null,
                        overrideCells: null,
                        postExplosionSpawnSingleThingDef: null,
                        preExplosionSpawnSingleThingDef: null);
                }
            }

            // Check if there are more bursts to come
            if (currentBurstShot < burstShotsTotal - 1)
            {
                // Prepare for next burst
                ticksToDetonate = 15; // Wait 15 ticks before next burst
                currentBurstShot++;
            }
            else
            {
                // All bursts completed, destroy the mote
                Destroy();
            }
        }
    }
}