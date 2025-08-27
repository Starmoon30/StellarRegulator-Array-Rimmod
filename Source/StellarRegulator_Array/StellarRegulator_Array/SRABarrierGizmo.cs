using System;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using static HarmonyLib.Code;

namespace SRA
{

    public class SRABarrierGizmo : Gizmo
    {
        private readonly HediffComp_SRABarrier barrier;

        public SRABarrierGizmo(HediffComp_SRABarrier barrier)
        {
            this.barrier = barrier;
            base.Order = -100f; // 使用base.order
        }

        public override float GetWidth(float maxWidth) => 120f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);

            // 标题
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Text.Anchor = TextAnchor.UpperCenter;
            string hediffName = barrier.parent.LabelCap;
            Widgets.Label(titleRect, hediffName + "SRABarrierTitle".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            // 屏障条
            Rect barRect = new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, 20f);
            float fillPercent = barrier.CurrentBarrier / barrier.Props.maxBarrier;
            if (fillPercent > 1){
                fillPercent = 1;
            }

            Widgets.FillableBar(
                barRect,
                fillPercent,
                SolidColorMaterials.NewSolidColorTexture(Color.black),
                BaseContent.BlackTex,
                false
            );

            // 数值显示
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, $"{barrier.CurrentBarrier:F0}/{barrier.Props.maxBarrier:F0}");
            Text.Anchor = TextAnchor.UpperLeft;

            // 状态信息
            Rect statusRect = new Rect(rect.x, rect.y + 55f, rect.width, 20f);
            if (barrier.InCooldown)
            {
                string cooldownText = "SRA_BarrierRecharging".Translate(
                    barrier.GetCooldownSeconds().ToString("N" + 2)
                );
                Widgets.Label(statusRect, cooldownText);
            }
            else
            {
                string regenText = "SRA_BarrierRegen".Translate(
                    barrier.Props.regenRate.ToString()
                );
                Widgets.Label(statusRect, regenText);
            }
            string TooltipText = "SRABarrierTooltip".Translate(
                    barrier.Props.regenDelay.ToString(),
                    barrier.Props.rechargeCooldown.ToString(),
                    barrier.Props.DamageTakenMult.ToString(),
                    barrier.Props.DamageTakenMax.ToString(),
                    barrier.Props.DamageTakenReduce.ToString()
                );
            TooltipHandler.TipRegion(rect, TooltipText);
            return new GizmoResult(GizmoState.Clear);
        }
    }

}
