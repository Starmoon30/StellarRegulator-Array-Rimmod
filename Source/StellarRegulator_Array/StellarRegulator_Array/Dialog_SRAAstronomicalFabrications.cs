using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    public sealed class Dialog_SRASelectScanItems : Window
    {
        private readonly CompOrbitalScannerTransporter scanner;
        private readonly List<TransferableOneWay> transferables = new List<TransferableOneWay>();
        private TransferableOneWayWidget transferWidget;

        public override Vector2 InitialSize => new Vector2(1020f, 720f);

        public Dialog_SRASelectScanItems(CompOrbitalScannerTransporter scanner)
        {
            this.scanner = scanner;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            BuildTransferables();
            transferWidget = new TransferableOneWayWidget(
                transferables,
                "SRA_OrbitalColonyInventory".Translate(),
                "SRA_OrbitalUploadManifest".Translate(),
                "SRA_OrbitalAvailableCount".Translate(),
                drawMass: false,
                drawMarketValue: true);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Rect widgetRect = new Rect(inRect.x, inRect.y + 2f, inRect.width, inRect.height - 60f);
            transferWidget?.OnGUI(widgetRect);

            Rect cancelRect = new Rect(inRect.center.x - 170f, inRect.yMax - 44f, 160f, 40f);
            Rect acceptRect = new Rect(inRect.center.x + 10f, inRect.yMax - 44f, 160f, 40f);
            if (Widgets.ButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            bool hasSelection = transferables.Any(transferable => transferable.CountToTransfer > 0);
            bool canApply = hasSelection || scanner.HasLoadedItems || scanner.AnythingLeftToLoad;
            if (Widgets.ButtonText(acceptRect, "AcceptButton".Translate(), active: canApply) && canApply)
            {
                ApplyManifest();
            }
        }

        private void BuildTransferables()
        {
            transferables.Clear();
            Map map = scanner.parent.Map;
            List<CompTransporter> transporters = new List<CompTransporter> { scanner };
            foreach (Thing thing in TransporterUtility.AllSendableItems(transporters, map))
            {
                AddTransferableThing(thing);
            }

            // AllSendableItems can omit existing assignments, so re-add them before restoring their requested counts.
            if (scanner.leftToLoad != null)
            {
                for (int i = 0; i < scanner.leftToLoad.Count; i++)
                {
                    TransferableOneWay assigned = scanner.leftToLoad[i];
                    if (assigned?.things == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < assigned.things.Count; j++)
                    {
                        AddTransferableThing(assigned.things[j]);
                    }
                }

                for (int i = 0; i < scanner.leftToLoad.Count; i++)
                {
                    TransferableOneWay assigned = scanner.leftToLoad[i];
                    Thing source = assigned?.AnyThing;
                    if (source == null)
                    {
                        continue;
                    }

                    TransferableOneWay matching = TransferableUtility.TransferableMatching(
                        source,
                        transferables,
                        TransferAsOneMode.PodsOrCaravanPacking);
                    if (matching != null)
                    {
                        matching.AdjustTo(Math.Min(matching.MaxCount, Math.Max(matching.CountToTransfer, assigned.CountToTransfer)));
                    }
                }
            }
        }

        private void AddTransferableThing(Thing thing)
        {
            if (thing == null || !OrbitalFabricationUtility.CanScan(thing, out _))
            {
                return;
            }

            TransferableOneWay transferable = TransferableUtility.TransferableMatching(
                thing,
                transferables,
                TransferAsOneMode.PodsOrCaravanPacking);
            if (transferable == null)
            {
                transferable = new TransferableOneWay();
                transferables.Add(transferable);
            }

            if (!transferable.things.Contains(thing))
            {
                transferable.things.Add(thing);
            }
        }

        private void ApplyManifest()
        {
            CompGenerator_SRA_Core comp = scanner.parent.TryGetComp<CompGenerator_SRA_Core>();
            comp?.ReplaceUploadManifest(transferables);
            Close();
        }
    }

    public sealed class Dialog_SRAAstronomicalFabrications : Window
    {
        private enum ConsolePage
        {
            ScanAndOrders,
            OrbitalIndustry
        }

        private static readonly Color PanelFill = new Color(0.018f, 0.085f, 0.11f, 0.96f);
        private static readonly Color PanelLine = new Color(0.19f, 0.82f, 0.9f, 0.7f);
        private static readonly Color Accent = new Color(0.42f, 1f, 0.9f);

        private readonly CompGenerator_SRA_Core comp;
        private readonly ThingFilter blueprintFilter = new ThingFilter();
        private readonly ThingFilterUI.UIState blueprintFilterUiState = new ThingFilterUI.UIState();
        private ConsolePage page;
        private Vector2 blueprintScroll;
        private Vector2 orderScroll;
        private Vector2 loadedContentsScroll;
        private Vector2 outputContentsScroll;
        private string blueprintSearch = string.Empty;
        private string reserveBuffer;
        private string targetBuffer;
        private string linkIntervalBuffer;
        private int linkIntervalSeconds;

        public override Vector2 InitialSize => new Vector2(1140f, 790f);

        public Dialog_SRAAstronomicalFabrications(CompGenerator_SRA_Core comp)
        {
            this.comp = comp;
            blueprintFilter.SetAllowAll(null, true);
            reserveBuffer = comp.AutoLaunchReserve.ToString();
            targetBuffer = comp.AutoLaunchTargetUnits.ToString();
            linkIntervalSeconds = comp.LinkIntervalSeconds;
            linkIntervalBuffer = linkIntervalSeconds.ToString();
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            soundAppear = SoundDefOf.CommsWindow_Open;
            soundClose = SoundDefOf.CommsWindow_Close;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (comp.parent == null || comp.parent.Destroyed || !comp.parent.Spawned)
            {
                Close();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawHeader(inRect);
            Rect tabsRect = new Rect(inRect.x, inRect.y + 92f, inRect.width, 38f);
            DrawTabs(tabsRect);
            Rect body = new Rect(inRect.x, tabsRect.yMax + 8f, inRect.width, inRect.yMax - tabsRect.yMax - 8f);
            if (page == ConsolePage.ScanAndOrders)
            {
                DrawScanAndOrders(body);
            }
            else
            {
                DrawOrbitalIndustry(body);
            }
        }

        private void DrawHeader(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            GUI.color = Accent;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "SRA_OrbitalConsoleTitle".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect strip = new Rect(inRect.x, inRect.y + 38f, inRect.width, 48f);
            DrawCyanPanel(strip);
            OrbitalIndustryNetworkState network = comp.Network;
            string orbit = "SRA_OrbitalHeaderOrbitEnergy".Translate(
                OrbitalFabricationUtility.FormatMassEnergy(comp.OrbitalMassEnergy),
                OrbitalFabricationUtility.FormatMassEnergy(comp.OrbitalMassEnergyCapacity));
            string local = "SRA_OrbitalHeaderUploadEnergy".Translate(
                OrbitalFabricationUtility.FormatMassEnergy(comp.MassEnergy),
                OrbitalFabricationUtility.FormatMassEnergy(comp.StorageCapacity));
            string units = "SRA_OrbitalHeaderUnits".Translate(network?.industrialUnits ?? 0);
            string state = comp.TransferActive
                ? "SRA_OrbitalHeaderDownload".Translate(comp.TransferProgress.ToStringPercent())
                : comp.Operational ? "SRA_OrbitalHeaderOnline".Translate() : "SRA_OrbitalHeaderOffline".Translate();

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Accent;
            Widgets.Label(new Rect(strip.x + 14f, strip.y, strip.width * 0.30f, strip.height), orbit);
            GUI.color = new Color(0.47f, 0.83f, 1f);
            Widgets.Label(new Rect(strip.x + strip.width * 0.31f, strip.y, strip.width * 0.30f, strip.height), local);
            GUI.color = new Color(0.72f, 1f, 0.89f);
            Widgets.Label(new Rect(strip.x + strip.width * 0.62f, strip.y, strip.width * 0.17f, strip.height), units);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(strip.x + strip.width * 0.79f, strip.y, strip.width * 0.20f - 10f, strip.height), state);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawTabs(Rect rect)
        {
            const float tabWidth = 250f;
            DrawTabButton(new Rect(rect.x, rect.y, tabWidth, rect.height), "SRA_OrbitalTabScan".Translate(), ConsolePage.ScanAndOrders);
            DrawTabButton(new Rect(rect.x + tabWidth + 6f, rect.y, tabWidth, rect.height), "SRA_OrbitalTabIndustry".Translate(), ConsolePage.OrbitalIndustry);
        }

        private void DrawTabButton(Rect rect, string label, ConsolePage targetPage)
        {
            bool selected = page == targetPage;
            Color old = GUI.color;
            GUI.color = Color.white;
            if (Widgets.ButtonText(rect, label))
            {
                page = targetPage;
            }

            if (selected)
            {
                Widgets.DrawBox(rect, 1);
            }

            GUI.color = old;
        }

        private void DrawScanAndOrders(Rect body)
        {
            const float uploadHeight = 188f;
            DrawUploadBay(new Rect(body.x, body.y, body.width, uploadHeight));

            float lowerY = body.y + uploadHeight + 10f;
            float libraryWidth = body.width * 0.36f;
            DrawOrderQueue(new Rect(body.x, lowerY, body.width - libraryWidth - 10f, body.yMax - lowerY));
            DrawBlueprints(new Rect(body.xMax - libraryWidth, lowerY, libraryWidth, body.yMax - lowerY));
        }

        private void DrawUploadBay(Rect rect)
        {
            DrawCyanPanel(rect);
            Rect inner = rect.ContractedBy(11f);
            Text.Font = GameFont.Medium;
            GUI.color = Accent;
            Widgets.Label(new Rect(inner.x, inner.y, 320f, 28f), "SRA_OrbitalUploadBay".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            CompOrbitalScannerTransporter scanner = comp.ScannerComp;
            string loadState;
            if (scanner == null)
            {
                loadState = "SRA_OrbitalScannerMissing".Translate();
            }
            else if (scanner.AnythingLeftToLoad)
            {
                loadState = "SRA_OrbitalScannerManifestRemaining".Translate(comp.PendingLoadCount);
            }
            else if (scanner.HasLoadedItems)
            {
                loadState = "SRA_OrbitalScannerReady".Translate(scanner.innerContainer.Count);
            }
            else
            {
                loadState = "SRA_OrbitalScannerEmptyStatus".Translate();
            }

            long projectedYield = comp.GetLoadedScanYield();
            long freeStorage = Math.Max(0L, comp.StorageCapacity - comp.MassEnergy);
            GUI.color = new Color(0.66f, 0.93f, 0.95f);
            Widgets.Label(new Rect(inner.x, inner.y + 36f, 300f, 24f), loadState);
            Widgets.Label(new Rect(inner.x, inner.y + 64f, 300f, 24f),
                "SRA_OrbitalProjectedYield".Translate(OrbitalFabricationUtility.FormatMassEnergy(projectedYield)));
            Widgets.Label(new Rect(inner.x, inner.y + 92f, 300f, 24f),
                "SRA_OrbitalUploadBufferFree".Translate(OrbitalFabricationUtility.FormatMassEnergy(freeStorage)));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, inner.y + 120f, 300f, 18f),
                "SRA_OrbitalScanEfficiency".Translate(comp.Props.scanEfficiency.ToStringPercent()));
            Widgets.Label(new Rect(inner.x, inner.y + 140f, 300f, 18f),
                "SRA_OrbitalFabricationCostFactor".Translate(comp.Props.fabricationCostFactor.ToStringPercent()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect contentsRect = new Rect(inner.x + 314f, inner.y, 310f, inner.height);
            Widgets.DrawBoxSolid(contentsRect, new Color(0.025f, 0.14f, 0.17f, 0.8f));
            DrawLoadedContents(contentsRect.ContractedBy(7f), scanner);

            Rect buttons = new Rect(inner.xMax - 236f, inner.y, 236f, inner.height);
            bool canSelect = scanner != null;
            DrawActionButton(new Rect(buttons.x, buttons.y, buttons.width, 34f), "SRA_OrbitalManageManifest".Translate(), canSelect, delegate
            {
                Find.WindowStack.Add(new Dialog_SRASelectScanItems(scanner));
            });

            bool canUpload = scanner != null && scanner.HasLoadedItems && projectedYield <= freeStorage;
            DrawActionButton(new Rect(buttons.x, buttons.y + 40f, buttons.width, 34f), "SRA_OrbitalUploadNow".Translate(), canUpload, delegate
            {
                TryUploadNow();
            });
        }

        private void TryUploadNow()
        {
            if (!comp.TryStopLoadingAndUpload(false, out string reason))
            {
                Messages.Message(reason, comp.parent, MessageTypeDefOf.RejectInput, false);
            }
        }

        private void DrawLoadedContents(Rect rect, CompOrbitalScannerTransporter scanner)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = Accent;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), "SRA_OrbitalLoadedContents".Translate());
            GUI.color = Color.white;
            Rect outRect = new Rect(rect.x, rect.y + 20f, rect.width, rect.height - 20f);
            int count = scanner?.innerContainer?.Count ?? 0;
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Math.Max(outRect.height, count * 21f));
            Widgets.BeginScrollView(outRect, ref loadedContentsScroll, viewRect);
            if (scanner?.innerContainer != null)
            {
                float y = 0f;
                foreach (Thing thing in scanner.innerContainer)
                {
                    Widgets.Label(new Rect(0f, y, viewRect.width, 20f), thing.LabelCap);
                    y += 21f;
                }
            }

            Widgets.EndScrollView();
            Text.Font = GameFont.Small;
        }

        private void DrawOrderQueue(Rect rect)
        {
            DrawCyanPanel(rect);
            Rect inner = rect.ContractedBy(10f);
            const float outputWidth = 174f;
            const float rowHeight = 116f;
            Rect queueRect = new Rect(inner.x, inner.y, inner.width - outputWidth - 8f, inner.height);
            Rect outputRect = new Rect(queueRect.xMax + 8f, inner.y, outputWidth, inner.height);

            Text.Font = GameFont.Medium;
            GUI.color = Accent;
            Widgets.Label(new Rect(queueRect.x, queueRect.y, queueRect.width, 27f), "SRA_OrbitalOrderQueue".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            List<OrbitalFabricationOrder> orders = comp.OrbitalOrders.Where(order => order != null).ToList();
            Rect outRect = new Rect(queueRect.x, queueRect.y + 32f, queueRect.width, queueRect.height - 32f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Math.Max(outRect.height, orders.Count * rowHeight));
            Widgets.BeginScrollView(outRect, ref orderScroll, viewRect);
            if (orders.Count == 0)
            {
                GUI.color = new Color(0.55f, 0.8f, 0.85f);
                Widgets.Label(new Rect(8f, 8f, viewRect.width - 16f, 38f), "SRA_OrbitalOrderQueueEmpty".Translate());
                GUI.color = Color.white;
            }
            else
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    DrawOrderRow(new Rect(0f, i * rowHeight, viewRect.width, rowHeight - 5f), orders[i], i);
                }
            }

            Widgets.EndScrollView();
            DrawOutputBuffer(outputRect);
        }

        private void DrawOrderRow(Rect rect, OrbitalFabricationOrder order, int index)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Widgets.DrawBoxSolid(rect, new Color(0.05f, 0.2f, 0.23f, index % 2 == 0 ? 0.46f : 0.23f));
            Rect icon = new Rect(rect.x + 5f, rect.y + 7f, 50f, 50f);
            Widgets.ThingIcon(icon, order.productDef, order.stuffDef, null, 1f, null, null, 1f);

            const float commandWidth = 112f;
            Rect textRect = new Rect(icon.xMax + 8f, rect.y + 5f, rect.width - icon.width - commandWidth - 18f, rect.height - 10f);
            long unitCost = OrbitalFabricationUtility.FabricationUnitCost(order.productDef, order.stuffDef, comp.Props);
            long batchCost = comp.GetOrderBatchCost(order);
            string stock = order.lastStockCount >= 0
                ? order.lastStockCount.ToString()
                : "SRA_OrbitalOrderUnchecked".Translate().ToString();

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(textRect.x, textRect.y, textRect.width, 20f), order.Label);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.54f, 0.93f, 0.94f);
            Widgets.Label(new Rect(textRect.x, textRect.y + 20f, textRect.width, 18f),
                "SRA_OrbitalOrderBatchDetails".Translate(
                    order.batchSize,
                    OrbitalFabricationUtility.FormatMassEnergy(unitCost),
                    OrbitalFabricationUtility.FormatMassEnergy(batchCost)));
            Widgets.Label(new Rect(textRect.x, textRect.y + 38f, textRect.width, 18f), GetOrderFulfillmentSummary(order));
            GUI.color = new Color(0.73f, 0.94f, 0.96f);
            Widgets.Label(new Rect(textRect.x, textRect.y + 56f, textRect.width, 18f),
                "SRA_OrbitalOrderInventoryDetails".Translate(stock, GetOrderQualitySummary(order)));
            GUI.color = Color.white;
            Widgets.Label(new Rect(textRect.x, textRect.y + 74f, textRect.width, 18f),
                "SRA_OrbitalOrderStatusDetails".Translate(comp.GetOrderStatus(order), GetOrderEvaluationSummary(order)));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float x = rect.xMax - commandWidth;
            if (Widgets.ButtonText(new Rect(x, rect.y + 7f, 53f, 25f), "SRA_OrbitalEdit".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SRAOrbitalOrderDraft(comp, order));
            }

            string suspendLabel = order.suspended ? "SRA_OrbitalResumeOrder".Translate() : "SRA_OrbitalSuspendOrder".Translate();
            if (Widgets.ButtonText(new Rect(x + 57f, rect.y + 7f, 53f, 25f), suspendLabel))
            {
                order.suspended = !order.suspended;
            }

            if (Widgets.ButtonText(new Rect(x, rect.y + 39f, 35f, 24f), "SRA_OrbitalMoveUp".Translate()))
            {
                comp.MoveOrder(order, -1);
            }

            if (Widgets.ButtonText(new Rect(x + 38f, rect.y + 39f, 35f, 24f), "SRA_OrbitalMoveDown".Translate()))
            {
                comp.MoveOrder(order, 1);
            }

            if (Widgets.ButtonText(new Rect(x + 76f, rect.y + 39f, 35f, 24f), "SRA_OrbitalDeleteShort".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "SRA_OrbitalRemoveOrderConfirm".Translate(order.Label),
                    delegate { comp.RemoveOrder(order); },
                    destructive: true));
            }
        }

        private static string GetOrderFulfillmentSummary(OrbitalFabricationOrder order)
        {
            switch (order.mode)
            {
                case OrbitalOrderMode.RepeatCount:
                    return "SRA_OrbitalOrderRepeatSummary".Translate(order.remainingBatches).ToString();
                case OrbitalOrderMode.TargetCount:
                    return "SRA_OrbitalOrderTargetSummary".Translate(order.targetCount).ToString();
                default:
                    return "SRA_OrbitalOrderForeverSummary".Translate().ToString();
            }
        }

        private static string GetOrderQualitySummary(OrbitalFabricationOrder order)
        {
            if (!order.ProductSupportsQuality)
            {
                return "SRA_OrbitalOrderNoQuality".Translate().ToString();
            }

            QualityRange range = order.QualityRange;
            return "SRA_OrbitalOrderQualityRange".Translate(
                QualityUtility.GetLabel(range.min),
                QualityUtility.GetLabel(range.max)).ToString();
        }

        private static string GetOrderEvaluationSummary(OrbitalFabricationOrder order)
        {
            if (order.lastEvaluationTick < 0)
            {
                return "SRA_OrbitalOrderNotChecked".Translate().ToString();
            }

            int now = Find.TickManager?.TicksGame ?? order.lastEvaluationTick;
            return "SRA_OrbitalOrderLastChecked".Translate(
                Math.Max(0, now - order.lastEvaluationTick).ToStringTicksToPeriod()).ToString();
        }

        private void DrawOutputBuffer(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.02f, 0.15f, 0.18f, 0.82f));
            Text.Font = GameFont.Small;
            GUI.color = Accent;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 5f, rect.width - 12f, 20f), "SRA_OrbitalOutputBuffer".Translate());
            GUI.color = new Color(0.73f, 1f, 0.93f);
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 25f, rect.width - 12f, 20f),
                "SRA_OrbitalOutputStored".Translate(comp.OutputItemCount, comp.OutputItemCapacity));
            GUI.color = Color.white;

            List<Thing> contents = comp.OutputItems.ToList();
            Rect outRect = new Rect(rect.x + 6f, rect.y + 48f, rect.width - 12f, rect.height - 54f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Math.Max(outRect.height, contents.Count * 20f));
            Text.Font = GameFont.Tiny;
            Widgets.BeginScrollView(outRect, ref outputContentsScroll, viewRect);
            if (contents.Count == 0)
            {
                GUI.color = new Color(0.48f, 0.75f, 0.78f);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 34f), "SRA_OrbitalOutputEmpty".Translate());
                GUI.color = Color.white;
            }
            else
            {
                float y = 0f;
                for (int i = 0; i < contents.Count; i++)
                {
                    Widgets.Label(new Rect(0f, y, viewRect.width, 19f), contents[i].LabelCap);
                    y += 20f;
                }
            }

            Widgets.EndScrollView();
            Text.Font = GameFont.Small;
        }

        private void DrawBlueprints(Rect rect)
        {
            DrawCyanPanel(rect);
            Rect inner = rect.ContractedBy(10f);
            Text.Font = GameFont.Medium;
            GUI.color = Accent;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width - 128f, 29f), "SRA_OrbitalBlueprintLibrary".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inner.xMax - 122f, inner.y - 2f, 122f, 31f), "SRA_OrbitalBlueprintFilter".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SRAOrbitalBlueprintFilter(blueprintFilter, blueprintFilterUiState, comp.Map));
            }

            blueprintSearch = Widgets.TextField(new Rect(inner.x, inner.y + 36f, inner.width, 28f), blueprintSearch);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.52f, 0.92f, 0.88f);
            Widgets.Label(new Rect(inner.x, inner.y + 66f, inner.width, 19f),
                "SRA_OrbitalBlueprintFilterSummary".Translate(blueprintFilter.Summary));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            List<OrbitalFabricationBlueprint> blueprints = (comp.Network?.blueprints ?? new List<OrbitalFabricationBlueprint>())
                .Where(blueprint => blueprint != null && blueprint.thingDef != null &&
                                    BlueprintMatchesFilter(blueprint) &&
                                    (blueprintSearch.NullOrEmpty() || blueprint.Label.IndexOf(blueprintSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(blueprint => blueprint.Label)
                .ToList();

            Rect outRect = new Rect(inner.x, inner.y + 88f, inner.width, inner.height - 88f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Math.Max(outRect.height, blueprints.Count * 50f));
            Widgets.BeginScrollView(outRect, ref blueprintScroll, viewRect);
            for (int i = 0; i < blueprints.Count; i++)
            {
                DrawBlueprintRow(new Rect(0f, i * 50f, viewRect.width, 46f), blueprints[i]);
            }
            Widgets.EndScrollView();
        }

        private bool BlueprintMatchesFilter(OrbitalFabricationBlueprint blueprint)
        {
            return blueprintFilter.Allows(blueprint.thingDef) ||
                   blueprint.stuffDef != null && blueprintFilter.Allows(blueprint.stuffDef);
        }

        private void DrawBlueprintRow(Rect rect, OrbitalFabricationBlueprint blueprint)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Rect icon = new Rect(rect.x + 4f, rect.y + 4f, 38f, 38f);
            Widgets.ThingIcon(icon, blueprint.thingDef, blueprint.stuffDef, null, 1f, null, null, 1f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(icon.xMax + 7f, rect.y, rect.width - 128f, rect.height), blueprint.Label);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(new Rect(rect.xMax - 118f, rect.y + 5f, 56f, 34f), "SRA_OrbitalAddOrder".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SRAOrbitalOrderDraft(comp, blueprint));
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - 56f, rect.y + 5f, 56f, 34f), "SRA_OrbitalDeleteBlueprint".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "SRA_OrbitalRemoveBlueprintConfirm".Translate(blueprint.Label),
                    delegate { comp.RemoveBlueprint(blueprint); },
                    destructive: true));
            }
        }

        private void DrawOrbitalIndustry(Rect body)
        {
            float visualWidth = body.width * 0.59f;
            Rect visualRect = new Rect(body.x, body.y, visualWidth, body.height);
            Rect controlRect = new Rect(visualRect.xMax + 10f, body.y, body.xMax - visualRect.xMax - 10f, body.height);
            OrbitalIndustryNetworkState network = comp.Network;
            AstronomicalFabricationVisuals.DrawHolographicSystem(
                visualRect,
                network?.industrialUnits ?? 0,
                comp.OrbitalMassEnergy,
                comp.OrbitalMassEnergyCapacity);
            DrawOrbitalControls(controlRect, network);
        }

        private void DrawOrbitalControls(Rect rect, OrbitalIndustryNetworkState network)
        {
            DrawCyanPanel(rect);
            Rect inner = rect.ContractedBy(12f);
            Text.Font = GameFont.Medium;
            GUI.color = Accent;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 29f), "SRA_OrbitalIndustryControl".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            float y = inner.y + 37f;

            int units = network?.industrialUnits ?? 0;
            long generation = OrbitalFabricationUtility.SaturatingMultiply(units, comp.Props.unitYieldPerInterval);
            DrawMetric(ref y, inner, "SRA_OrbitalMetricUnits".Translate(), units.ToString());
            DrawMetric(ref y, inner, "SRA_OrbitalMetricGeneration".Translate(comp.GenerationPeriodLabel), OrbitalFabricationUtility.FormatMassEnergy(generation));
            DrawMetric(ref y, inner, "SRA_OrbitalMetricUnitGeneration".Translate(comp.GenerationPeriodLabel),
                OrbitalFabricationUtility.FormatMassEnergy(Math.Max(0L, comp.Props.unitYieldPerInterval)));
            DrawMetric(ref y, inner, "SRA_OrbitalMetricOrbitalEnergy".Translate(),
                OrbitalFabricationUtility.FormatMassEnergy(comp.OrbitalMassEnergy) + " / " +
                OrbitalFabricationUtility.FormatMassEnergy(comp.OrbitalMassEnergyCapacity));
            DrawMetric(ref y, inner, "SRA_OrbitalMetricUnitCapacity".Translate(),
                OrbitalFabricationUtility.FormatMassEnergy(Math.Max(0L, comp.Props.orbitalMassEnergyCapacityPerUnit)));
            DrawMetric(ref y, inner, "SRA_OrbitalMetricUploadBuffer".Translate(),
                OrbitalFabricationUtility.FormatMassEnergy(comp.MassEnergy) + " / " + OrbitalFabricationUtility.FormatMassEnergy(comp.StorageCapacity));
            DrawMetric(ref y, inner, "SRA_OrbitalMetricBandwidth".Translate(), OrbitalFabricationUtility.FormatMassEnergy(comp.LinkBandwidthPerEvent));
            DrawMetric(ref y, inner, "SRA_OrbitalMetricNextLink".Translate(), comp.NextLinkTicks.ToStringTicksToPeriod());

            y += 4f;
            Widgets.Label(new Rect(inner.x, y + 4f, inner.width - 120f, 25f), "SRA_OrbitalLinkInterval".Translate());
            int previousInterval = linkIntervalSeconds;
            Widgets.TextFieldNumeric(new Rect(inner.xMax - 112f, y, 112f, 30f), ref linkIntervalSeconds, ref linkIntervalBuffer, 1,
                Math.Max(1, Mathf.CeilToInt(comp.Props.maximumLinkIntervalTicks / 60f)));
            if (linkIntervalSeconds != previousInterval)
            {
                comp.SetLinkIntervalSeconds(linkIntervalSeconds);
            }

            y += 36f;
            bool animate = comp.DownloadAnimationEnabled;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, inner.width, 25f), "SRA_OrbitalDownloadAnimation".Translate(), ref animate);
            comp.DownloadAnimationEnabled = animate;
            y += 27f;
            bool sound = comp.DownloadSoundEnabled;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, inner.width, 25f), "SRA_OrbitalDownloadSound".Translate(), ref sound);
            comp.DownloadSoundEnabled = sound;
            y += 33f;

            Widgets.Label(new Rect(inner.x, y, inner.width, 21f), "SRA_OrbitalTransferProgress".Translate());
            y += 23f;
            Widgets.FillableBar(new Rect(inner.x, y, inner.width, 20f), comp.TransferActive ? comp.TransferProgress : 0f);
            Text.Anchor = TextAnchor.MiddleCenter;
            string transferState = comp.TransferActive
                ? comp.TransferProgress.ToStringPercent()
                : "SRA_OrbitalIdle".Translate().ToString();
            Widgets.Label(new Rect(inner.x, y, inner.width, 20f), transferState);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 30f;

            string launchLabel = "SRA_OrbitalLaunchUnit".Translate(OrbitalFabricationUtility.FormatMassEnergy(comp.Props.industrialUnitLaunchCost));
            bool canLaunch = comp.Operational && comp.LaunchCooldownTicks <= 0 && network != null &&
                             network.industrialUnits < OrbitalFabricationUtility.MaximumIndustrialUnits &&
                             network.OrbitalMassEnergy >= comp.Props.industrialUnitLaunchCost;
            DrawActionButton(new Rect(inner.x, y, inner.width, 35f), launchLabel, canLaunch, delegate { comp.TryBeginIndustrialUnitLaunch(); });
            y += 42f;

            if (comp.LaunchCooldownTicks > 0)
            {
                Widgets.Label(new Rect(inner.x, y, inner.width, 21f), "SRA_OrbitalLaunchCooldown".Translate(comp.LaunchCooldownTicks.ToStringTicksToPeriod()));
                y += 25f;
            }

            bool auto = comp.AutoLaunch;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, inner.width, 25f), "SRA_OrbitalAutoLaunch".Translate(), ref auto);
            comp.AutoLaunch = auto;
            y += 28f;

            Widgets.Label(new Rect(inner.x, y + 4f, inner.width - 120f, 24f), "SRA_OrbitalAutoLaunchTarget".Translate());
            targetBuffer = Widgets.TextField(new Rect(inner.xMax - 112f, y, 112f, 28f), targetBuffer);
            if (int.TryParse(targetBuffer, out int target))
            {
                comp.AutoLaunchTargetUnits = target;
            }

            y += 33f;
            Widgets.Label(new Rect(inner.x, y + 4f, inner.width - 120f, 24f), "SRA_OrbitalReserveThreshold".Translate());
            reserveBuffer = Widgets.TextField(new Rect(inner.xMax - 112f, y, 112f, 28f), reserveBuffer);
            if (long.TryParse(reserveBuffer, out long reserve))
            {
                comp.AutoLaunchReserve = reserve;
            }
        }

        private static void DrawMetric(ref float y, Rect inner, string label, string value)
        {
            Rect row = new Rect(inner.x, y, inner.width, 24f);
            Widgets.DrawBoxSolid(row, new Color(0.06f, 0.23f, 0.27f, ((int)(y - inner.y) / 24 & 1) == 0 ? 0.34f : 0.16f));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 5f, row.y, row.width * 0.59f, row.height), label);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Accent;
            Widgets.Label(new Rect(row.x + row.width * 0.59f, row.y, row.width * 0.39f - 5f, row.height), value);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            y += 26f;
        }

        private static void DrawActionButton(Rect rect, string label, bool active, Action action)
        {
            if (Widgets.ButtonText(rect, label, active: active) && active)
            {
                action();
            }
        }

        private static void DrawCyanPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, PanelFill);
            Color old = GUI.color;
            GUI.color = PanelLine;
            Widgets.DrawBox(rect, 1);
            GUI.color = old;
        }
    }

    public sealed class Dialog_SRAOrbitalBlueprintFilter : Window
    {
        private readonly ThingFilter filter;
        private readonly ThingFilterUI.UIState uiState;
        private readonly Map map;

        public override Vector2 InitialSize => new Vector2(920f, 760f);

        public Dialog_SRAOrbitalBlueprintFilter(ThingFilter filter, ThingFilterUI.UIState uiState, Map map)
        {
            this.filter = filter;
            this.uiState = uiState;
            this.map = map;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 164f, 32f), "SRA_OrbitalBlueprintFilterTitle".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 156f, inRect.y - 2f, 156f, 32f), "SRA_OrbitalBlueprintFilterReset".Translate()))
            {
                filter.SetAllowAll(null, true);
            }

            ThingFilterUI.DoThingFilterConfigWindow(
                new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f),
                uiState,
                filter,
                null,
                0,
                null,
                null,
                true,
                true,
                false,
                null,
                map);
        }
    }

    public sealed class Dialog_SRAOrbitalOrderDraft : Window
    {
        private readonly CompGenerator_SRA_Core comp;
        private readonly OrbitalFabricationBlueprint blueprint;
        private readonly OrbitalFabricationOrder editingOrder;
        private int batchSize;
        private string batchBuffer;
        private OrbitalOrderMode mode;
        private int repeatCount;
        private string repeatBuffer;
        private int targetCount;
        private string targetBuffer;
        private bool includeEquipped;
        private QualityRange qualityRange;

        public override Vector2 InitialSize => new Vector2(650f, SupportsQuality ? 545f : 475f);

        private bool SupportsQuality => OrbitalFabricationUtility.ProductSupportsQuality(blueprint?.thingDef);

        public Dialog_SRAOrbitalOrderDraft(CompGenerator_SRA_Core comp, OrbitalFabricationBlueprint blueprint)
            : this(comp, blueprint, null)
        {
        }

        public Dialog_SRAOrbitalOrderDraft(CompGenerator_SRA_Core comp, OrbitalFabricationOrder order)
            : this(comp, new OrbitalFabricationBlueprint(order?.productDef, order?.stuffDef), order)
        {
        }

        private Dialog_SRAOrbitalOrderDraft(CompGenerator_SRA_Core comp, OrbitalFabricationBlueprint blueprint, OrbitalFabricationOrder editingOrder)
        {
            this.comp = comp;
            this.blueprint = blueprint;
            this.editingOrder = editingOrder;
            batchSize = editingOrder?.batchSize ?? 1;
            batchBuffer = batchSize.ToString();
            mode = editingOrder?.mode ?? OrbitalOrderMode.Forever;
            repeatCount = editingOrder?.remainingBatches ?? 1;
            repeatBuffer = repeatCount.ToString();
            targetCount = editingOrder?.targetCount ?? 1;
            targetBuffer = targetCount.ToString();
            includeEquipped = editingOrder?.includeEquipped ?? false;
            qualityRange = editingOrder?.QualityRange ?? new QualityRange(QualityCategory.Awful, QualityCategory.Legendary);
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                editingOrder == null ? "SRA_OrbitalOrderDraftTitle".Translate() : "SRA_OrbitalEditOrderTitle".Translate());
            Text.Font = GameFont.Small;

            Rect iconRect = new Rect(inRect.x, inRect.y + 43f, 64f, 64f);
            Widgets.DrawMenuSection(iconRect.ExpandedBy(4f));
            Widgets.ThingIcon(iconRect, blueprint.thingDef, blueprint.stuffDef, null, 1f, null, null, 1f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(iconRect.xMax + 15f, iconRect.y, inRect.width - iconRect.width - 15f, iconRect.height), blueprint.Label);
            Text.Anchor = TextAnchor.UpperLeft;

            float y = iconRect.yMax + 14f;
            DrawNumericRow(new Rect(inRect.x, y, inRect.width, 30f), "SRA_OrbitalBatchSize".Translate(), ref batchSize, ref batchBuffer, 1,
                OrbitalFabricationUtility.MaximumBatchSize);
            y += 38f;

            if (SupportsQuality)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "SRA_OrbitalQualityRange".Translate());
                y += 22f;
                Widgets.QualityRange(new Rect(inRect.x, y, inRect.width, 40f), 882011 + (editingOrder?.id ?? 0), ref qualityRange);
                y += 48f;
            }

            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "SRA_OrbitalRepeatMode".Translate());
            y += 27f;
            float modeWidth = (inRect.width - 12f) / 3f;
            DrawModeButton(new Rect(inRect.x, y, modeWidth, 30f), "SRA_OrbitalRepeatForever".Translate(), OrbitalOrderMode.Forever);
            DrawModeButton(new Rect(inRect.x + modeWidth + 6f, y, modeWidth, 30f), "SRA_OrbitalRepeatCount".Translate(), OrbitalOrderMode.RepeatCount);
            DrawModeButton(new Rect(inRect.x + (modeWidth + 6f) * 2f, y, modeWidth, 30f), "SRA_OrbitalRepeatTarget".Translate(), OrbitalOrderMode.TargetCount);
            y += 38f;

            if (mode == OrbitalOrderMode.RepeatCount)
            {
                DrawNumericRow(new Rect(inRect.x, y, inRect.width, 30f), "SRA_OrbitalRepeatTimes".Translate(), ref repeatCount, ref repeatBuffer, 1,
                    OrbitalFabricationUtility.MaximumBatchSize);
                y += 38f;
            }
            else if (mode == OrbitalOrderMode.TargetCount)
            {
                DrawNumericRow(new Rect(inRect.x, y, inRect.width, 30f), "SRA_OrbitalTargetCount".Translate(), ref targetCount, ref targetBuffer, 1, int.MaxValue);
                y += 38f;
            }

            Widgets.CheckboxLabeled(new Rect(inRect.x, y, inRect.width, 26f), "SRA_OrbitalCountEquipped".Translate(), ref includeEquipped);
            y += 31f;

            long unitCost = OrbitalFabricationUtility.FabricationUnitCost(blueprint.thingDef, blueprint.stuffDef, comp.Props);
            long totalCost = OrbitalFabricationUtility.SaturatingMultiply(unitCost, Math.Max(1, Math.Min(batchSize, OrbitalFabricationUtility.MaximumBatchSize)));
            GUI.color = new Color(0.48f, 1f, 0.9f);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 21f), "SRA_OrbitalOrderUnitPrice".Translate(OrbitalFabricationUtility.FormatMassEnergy(unitCost)));
            y += 23f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 21f), "SRA_OrbitalOrderTotalPrice".Translate(OrbitalFabricationUtility.FormatMassEnergy(totalCost)));
            y += 23f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 21f),
                "SRA_OrbitalFabricationCostFactor".Translate(comp.Props.fabricationCostFactor.ToStringPercent()));
            GUI.color = Color.white;

            Rect cancelRect = new Rect(inRect.center.x - 165f, inRect.yMax - 42f, 150f, 38f);
            Rect acceptRect = new Rect(inRect.center.x + 15f, inRect.yMax - 42f, 150f, 38f);
            if (Widgets.ButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            if (Widgets.ButtonText(acceptRect, editingOrder == null ? "SRA_OrbitalCreateOrder".Translate() : "SRA_OrbitalSaveOrder".Translate()))
            {
                bool saved = editingOrder == null
                    ? comp.AddOrder(blueprint, batchSize, qualityRange, mode, repeatCount, targetCount, includeEquipped)
                    : comp.UpdateOrder(editingOrder, batchSize, qualityRange, mode, repeatCount, targetCount, includeEquipped);
                if (saved)
                {
                    Close();
                }
            }
        }

        private void DrawModeButton(Rect rect, string label, OrbitalOrderMode targetMode)
        {
            Color old = GUI.color;
            GUI.color = Color.white;
            if (Widgets.ButtonText(rect, label))
            {
                mode = targetMode;
            }

            if (mode == targetMode)
            {
                Widgets.DrawBox(rect, 1);
            }

            GUI.color = old;
        }

        private static void DrawNumericRow(Rect rect, string label, ref int value, ref string buffer, int min, int max)
        {
            Widgets.Label(new Rect(rect.x, rect.y + 4f, rect.width - 150f, rect.height), label);
            Widgets.TextFieldNumeric(new Rect(rect.xMax - 140f, rect.y, 140f, rect.height), ref value, ref buffer, min, max);
        }
    }
}
