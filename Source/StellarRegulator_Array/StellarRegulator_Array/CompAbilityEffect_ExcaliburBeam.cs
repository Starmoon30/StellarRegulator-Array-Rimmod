using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace SRA
{
    public class CompProperties_AbilityExcaliburBeam : CompProperties_AbilityEffect
    {
        public string beamDefName;
        public float damageAmount;
        public float armorPenetration;
        public float pathWidth;
        public DamageDef damageDef;

        public CompProperties_AbilityExcaliburBeam()
        {
            this.compClass = typeof(CompAbilityEffect_ExcaliburBeam);
        }
    }

    public class CompAbilityEffect_ExcaliburBeam : CompAbilityEffect
    {
        public new CompProperties_AbilityExcaliburBeam Props
        {
            get
            {
                return (CompProperties_AbilityExcaliburBeam)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 计算影响的所有单元格
            List<IntVec3> allAffectedCells = CalculateAffectedCells(target.Cell);

            // 创建光束效果
            Thing_ExcaliburBeam beam = (Thing_ExcaliburBeam)GenSpawn.Spawn(
                DefDatabase<ThingDef>.GetNamed(Props.beamDefName),
                this.parent.pawn.Position,
                this.parent.pawn.Map
            );

            // 设置光束属性
            beam.caster = this.parent.pawn;
            beam.targetCell = target.Cell;
            beam.damageAmount = Props.damageAmount;
            beam.armorPenetration = Props.armorPenetration;
            beam.pathWidth = Props.pathWidth;
            beam.damageDef = Props.damageDef;

            // 启动打击
            beam.StartStrike(allAffectedCells, 0, 1); // 单次爆发
        }

        // 重写 DrawEffectPreview 方法以显示作用范围
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);

            if (target.IsValid)
            {
                // 计算并绘制受影响区域
                List<IntVec3> affectedCells = CalculateAffectedCells(target.Cell);
                GenDraw.DrawFieldEdges(affectedCells, Valid(target) ? Color.green : Color.red);

                // 绘制从施法者到目标的连线
                GenDraw.DrawLineBetween(this.parent.pawn.Position.ToVector3Shifted(),
                                       target.CenterVector3, SimpleColor.White);
            }
        }

        // 计算受影响的单元格
        private List<IntVec3> CalculateAffectedCells(IntVec3 targetCell)
        {
            List<IntVec3> result = new List<IntVec3>();
            Map map = this.parent.pawn.Map;

            // 使用 ShootLine 计算从施法者到目标的直线
            ShootLine shootLine = new ShootLine(this.parent.pawn.Position, targetCell);
            foreach (IntVec3 cell in shootLine.Points())
            {
                // 添加单元格及其周围单元格（根据路径宽度）
                for (int i = -Mathf.FloorToInt(Props.pathWidth / 2f); i <= Mathf.CeilToInt(Props.pathWidth / 2f); i++)
                {
                    for (int j = -Mathf.FloorToInt(Props.pathWidth / 2f); j <= Mathf.CeilToInt(Props.pathWidth / 2f); j++)
                    {
                        IntVec3 offsetCell = new IntVec3(cell.x + i, cell.y, cell.z + j);
                        if (offsetCell.InBounds(map) && !result.Contains(offsetCell))
                        {
                            result.Add(offsetCell);
                        }
                    }
                }
            }

            return result;
        }
    }
}