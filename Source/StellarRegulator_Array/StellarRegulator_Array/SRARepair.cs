using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
namespace RimWorld
{
    public class SRA_SRA_RepairTower : ThingComp
    {
        private int SRArepairTick;
        public override void CompTick()
        {
            base.CompTick();
            if (this.SRArepairTick <= 0)
            {
                this.SRArepairTick = 300;
                this.SRARepairAllBuildings();
                this.SRARepairAllThings();
                this.SRARepairAllMapApparelsInPawn();
            }
            else
            {
                this.SRArepairTick--;
            }
        }

        private bool SRACanRepair(Thing thing)
        {
            return thing.def.useHitPoints && (float)thing.HitPoints < (float)thing.MaxHitPoints;
        }

        public void SRARepairAllBuildings()
        {
            foreach (IntVec3 intVec in this.parent.MapHeld.areaManager.Home.ActiveCells)
            {
                Building firstBuilding = GridsUtility.GetFirstBuilding(intVec, this.parent.MapHeld);
                if (firstBuilding != null && firstBuilding.def.useHitPoints && (float)firstBuilding.HitPoints < 2f * (float)firstBuilding.MaxHitPoints + 800 && firstBuilding.Faction == Faction.OfPlayer)
                {
                    firstBuilding.HitPoints += Mathf.CeilToInt((float)firstBuilding.MaxHitPoints * 0.1f);
                }
            }
        }

        public void SRARepairAllThings()
        {
            foreach (IntVec3 intVec in this.parent.MapHeld.areaManager.Home.ActiveCells)
            {
                foreach (Thing thing in GridsUtility.GetThingList(intVec, this.parent.MapHeld))
                {
                    if (this.SRACanRepair(thing))
                    {
                        thing.HitPoints += Mathf.CeilToInt((float)thing.MaxHitPoints * 0.1f);
                    }
                }
            }
        }

        public void SRARepairAllApparelsInOnePawn(Pawn p)
        {
            if (p.apparel != null)
            {
                foreach (Apparel apparel in p.apparel.WornApparel)
                {
                    if (apparel.def.useHitPoints && (float)apparel.HitPoints < (float)apparel.MaxHitPoints)
                    {
                        apparel.HitPoints = (int)((float)apparel.MaxHitPoints);
                    }
                }
            }
            if (p.equipment != null)
            {
                foreach (ThingWithComps thingWithComps in p.equipment.AllEquipmentListForReading)
                {
                    if (thingWithComps.def.useHitPoints && (float)thingWithComps.HitPoints < (float)thingWithComps.MaxHitPoints)
                    {
                        thingWithComps.HitPoints = (int)((float)thingWithComps.MaxHitPoints);
                    }
                }
            }
            if (p.inventory != null)
            {
                foreach (Thing thing in p.inventory.GetDirectlyHeldThings())
                {
                    if (thing.def.useHitPoints && (float)thing.HitPoints < (float)thing.MaxHitPoints)
                    {
                        thing.HitPoints = (int)((float)thing.MaxHitPoints);
                    }
                }
            }
        }

        public void SRARepairAllApparelsInPawn(Map map = null)
        {
            Map map2 = map ?? this.parent.MapHeld;
            foreach (Pawn p in map2.mapPawns.PawnsInFaction(Find.FactionManager.OfPlayer))
            {
                this.SRARepairAllApparelsInOnePawn(p);
            }
        }

        public void SRARepairAllMapApparelsInPawn()
        {
            foreach (Map map in Find.Maps)
            {
                this.SRARepairAllApparelsInPawn(map);
            }
        }

    }
}
