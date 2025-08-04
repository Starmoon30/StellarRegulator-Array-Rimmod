using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;
using static UnityEngine.GraphicsBuffer;
namespace SRA
{
    public class SRABuilding_ResurrectionBeacon : Building
    {
        // 存储绑定的殖民者ID
        private HashSet<int> boundPawnIds = new HashSet<int>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref boundPawnIds, "boundPawnIds", LookMode.Value);
        }

        public bool CanBindPawn(Pawn pawn)
        {
            if (pawn == null) return false;
            // 只处理己方单位
            return pawn.CanTakeOrder;
        }
        
        // 绑定殖民者
        public void BindPawn(Pawn pawn)
        {
            if (pawn == null || boundPawnIds.Contains(pawn.thingIDNumber) || !CanBindPawn(pawn)) return;

            boundPawnIds.Add(pawn.thingIDNumber);
            ApplyBoundHediff(pawn);
        }


        // 解绑殖民者
        public void UnbindPawn(Pawn pawn)
        {
            if (pawn == null || !boundPawnIds.Contains(pawn.thingIDNumber)) return;

            boundPawnIds.Remove(pawn.thingIDNumber);
            RemoveBoundHediff(pawn);
        }

        // 检查是否绑定
        public bool IsBound(Pawn pawn) =>
            pawn != null && boundPawnIds.Contains(pawn.thingIDNumber) && CanBindPawn(pawn);

        // 应用绑定状态
        private void ApplyBoundHediff(Pawn pawn)
        {
            if (pawn.health?.hediffSet == null) return;
            if (!pawn.health.hediffSet.HasHediff(SRA_DefOf.SRAResurrectionBound))
            {
                pawn.health.AddHediff(SRA_DefOf.SRAResurrectionBound);
            }
        }

        // 移除绑定状态
        private void RemoveBoundHediff(Pawn pawn)
        {
            Hediff hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(SRA_DefOf.SRAResurrectionBound);
            if (hediff != null) pawn.health.RemoveHediff(hediff);
        }

        // 建筑被摧毁时解绑所有殖民者
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            List<Pawn> allPawns = PawnsFinder.All_AliveOrDead;
            foreach (int pawnId in new List<int>(boundPawnIds))
            {
                Pawn pawn = allPawns.Find(p => p.thingIDNumber == pawnId);
                if (pawn != null) RemoveBoundHediff(pawn);
            }
            boundPawnIds.Clear();
            base.Destroy(mode);
        }

        // 复活处理
        public void TriggerResurrection(Pawn pawn)
        {
            if (!Spawned || pawn == null) return;

            // 传送殖民者
            ResurrectionUtility.TeleportToBeacon(pawn, this);

            // 治愈致死伤
            // ResurrectionUtility.CureLethalConditions(pawn);

            Hediff newHediff = HediffMaker.MakeHediff(SRA_DefOf.SRAResurrectionAlready, pawn);
            newHediff.Severity = 1f;
            pawn.health.AddHediff(newHediff);

        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;

            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DropCarriedPawn"),
                defaultLabel = "SRABuilding_ResurrectionBeaconActionLabel".Translate(),
                defaultDesc = "SRABuilding_ResurrectionBeaconActionDesc".Translate(),
                action = () => Find.WindowStack.Add(new Dialog_BindPawns(this))
            };
        }
    }
    public static class ResurrectionUtility
    {
        public static void TeleportToBeacon(Pawn pawn, SRABuilding_ResurrectionBeacon beacon)
        {
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            // 确保位置有效
            IntVec3 targetPos = beacon.Position;
            Map targetMap = beacon.Map;

            if (!targetPos.IsValid || targetMap == null) return;

            // 放置殖民者
            GenPlace.TryPlaceThing(pawn, targetPos, targetMap, ThingPlaceMode.Direct);

            // 创建传送特效
            FleckMaker.ThrowLightningGlow(targetPos.ToVector3Shifted(), targetMap, 2f);
            FleckMaker.ThrowDustPuffThick(targetPos.ToVector3Shifted(), targetMap, 4f, new Color(0.8f, 0.2f, 1f));
        }
    }
    public class Dialog_BindPawns : Window
    {
        private SRABuilding_ResurrectionBeacon beacon;
        private Vector2 scrollPosition;
        private List<Pawn> bindablePawns;
        private Rect scrollRect;
        private Rect scrollContent;

        // 拖选状态变量
        private bool isDragging = false;
        private bool dragStartState = false;
        private Pawn dragStartPawn;
        private HashSet<Pawn> processedPawns = new HashSet<Pawn>();

        // 存储每个条目的矩形位置
        private Dictionary<Pawn, Rect> pawnRects = new Dictionary<Pawn, Rect>();

        public Dialog_BindPawns(SRABuilding_ResurrectionBeacon beacon)
        {
            this.beacon = beacon;
            this.forcePause = true;
            this.doCloseButton = true;
            this.absorbInputAroundWindow = false;

            // 预先获取可绑定的殖民者列表
            bindablePawns = beacon.Map.mapPawns.AllPawns
                .Where(p => p.CanTakeOrder)
                .ToList();
        }

        public override Vector2 InitialSize => new Vector2(500f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            HandleDragSelection();
            // 绘制标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "SRABuilding_ResurrectionBeaconTitle".Translate());
            Text.Font = GameFont.Small; // 恢复默认字体

            // 计算内容区域
            Rect contentRect = new Rect(inRect);
            contentRect.yMin += 35f;
            contentRect.yMax -= 45f;
            contentRect = contentRect.ContractedBy(5f);

            // 计算滚动区域
            const float rowHeight = 40f;
            scrollContent = new Rect(0f, 0f, contentRect.width - 16f, bindablePawns.Count * rowHeight);
            scrollRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height);

            // 开始滚动视图
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, scrollContent);
            {
                float curY = 0f;
                pawnRects.Clear(); // 清除旧的位置记录

                foreach (Pawn pawn in bindablePawns)
                {
                    Rect rowRect = new Rect(0f, curY, scrollContent.width, rowHeight);
                    pawnRects[pawn] = rowRect; // 记录每个殖民者的矩形位置

                    bool isBound = beacon.IsBound(pawn);

                    // 绘制行背景
                    if (isBound)
                    {
                        GUI.color = new Color(0.2f, 0.8f, 0.2f, 0.1f);
                        Widgets.DrawHighlight(rowRect);
                        GUI.color = Color.white;
                    }

                    // 悬停效果
                    if (Mouse.IsOver(rowRect))
                    {
                        Widgets.DrawHighlight(rowRect);

                        // 拖选过程中的特殊高亮
                        if (isDragging)
                        {
                            Widgets.DrawBox(rowRect, 2);
                        }
                    }

                    // 1. 绘制殖民者肖像
                    const float portraitSize = 35f;
                    Rect portraitRect = new Rect(
                        rowRect.x + 5f,
                        rowRect.y + (rowHeight - portraitSize) / 2f,
                        portraitSize,
                        portraitSize
                    );

                    RenderTexture portrait = PortraitsCache.Get(
                        pawn,
                        new Vector2(portraitSize, portraitSize),
                        Rot4.South,
                        new Vector3(0f, 0f, 0.1f),
                        1.3f,
                        true,
                        true,
                        false,
                        true,
                        null,
                        null,
                        false,
                        null
                    );
                    GUI.DrawTexture(portraitRect, portrait);

                    // 2. 绘制殖民者信息
                    Rect infoRect = new Rect(
                        portraitRect.xMax + 10f,
                        rowRect.y,
                        rowRect.width - portraitRect.width - 30f,
                        rowHeight
                    );

                    // 保存原始文本设置
                    GameFont originalFont = Text.Font;
                    TextAnchor originalAnchor = Text.Anchor;

                    // 名字
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(infoRect.x, infoRect.y, infoRect.width, rowHeight), pawn.LabelShortCap);

                    // 恢复文本设置
                    Text.Font = originalFont;
                    Text.Anchor = originalAnchor;

                    // 3. 绘制绑定状态指示器
                    Rect checkRect = new Rect(
                        rowRect.xMax - 30f,
                        rowRect.y + (rowHeight - 20f) / 2f,
                        20f,
                        20f
                    );
                    Widgets.Checkbox(checkRect.position,ref isBound, disabled: true);

                    // 4. 在 MouseDown 时立即处理绑定/解绑
                    if (Event.current.type == EventType.MouseDown &&
                        Mouse.IsOver(rowRect) &&
                        Event.current.button == 0)
                    {
                        // 记录当前状态前切换
                        bool wasBound = beacon.IsBound(pawn);

                        // 立即切换状态
                        TogglePawnBinding(pawn);

                        // 开始拖选
                        isDragging = true;
                        dragStartPawn = pawn;
                        dragStartState = !wasBound; // 记录起始操作类型
                        processedPawns.Clear();
                        processedPawns.Add(pawn);

                        // 标记事件已处理
                        Event.current.Use();
                    }

                    curY += rowHeight;
                }
            }
            Widgets.EndScrollView();

            // 处理拖选逻辑
            HandleDragSelection();

        }
        private void HandleDragSelection()
        {
            if (!isDragging) return;

            // 处理拖选逻辑
            if (Event.current.type == EventType.MouseDrag)
            {
                // 获取当前鼠标位置下的殖民者
                Pawn currentPawn = GetPawnAtPosition(Event.current.mousePosition);

                if (currentPawn != null && !processedPawns.Contains(currentPawn))
                {
                    // 只有当当前状态与起始操作不一致时才切换
                    if (beacon.IsBound(currentPawn) != dragStartState)
                    {
                        // 立即切换状态
                        TogglePawnBinding(currentPawn);
                    }

                    processedPawns.Add(currentPawn);

                    // 标记事件已处理
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                // 结束拖选
                isDragging = false;
                processedPawns.Clear();

                // 标记事件已处理
                Event.current.Use();
            }
        }
        // 确保有正确的 GetPawnAtPosition 实现
        private Pawn GetPawnAtPosition(Vector2 mousePos)
        {
            // 计算鼠标在滚动内容区域中的位置
            Vector2 scrollViewPosition = mousePos - scrollRect.position + scrollPosition;

            // 遍历所有殖民者的矩形位置
            foreach (var pair in pawnRects)
            {
                if (pair.Value.Contains(scrollViewPosition))
                {
                    return pair.Key;
                }
            }
            return null;
        }
        private enum PawnInteraction
        {
            None,
            Clicked,
            Dragging
        }
        private void TogglePawnBinding(Pawn pawn)
        {
            if (beacon.IsBound(pawn))
            {
                beacon.UnbindPawn(pawn);
            }
            else
            {
                beacon.BindPawn(pawn);
            }
        }
    }
}
