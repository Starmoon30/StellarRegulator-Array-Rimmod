using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using UnityEngine;

namespace SRA
{
	public class EventWindow_Rocivia : Window
	{
		private float windowWidth = 500f;
		private float windowHeight = 0f;
		private string graphic = null;
		private float drawSize;
		private float drawOffset;

		private List<EventButton> options;

		private List<EventTree> eventTree;
		private int index;

		public override Vector2 InitialSize => new Vector2(windowWidth, windowHeight);

		public EventWindow_Rocivia(string title, string description, string graphic, float drawSize, float drawOffset, Map map, Pawn pawn, List<EventButton> options, List<EventTree> eventTree, int index)
		{
			this.title = title;
			this.description = description;
			this.graphic = graphic;
			this.drawSize = drawSize;
			this.drawOffset = drawOffset;
			forcePause = true;
			absorbInputAroundWindow = true;
			closeOnAccept = false;
			closeOnCancel = false;
			soundAppear = SoundDefOf.CommsWindow_Open;
			soundClose = SoundDefOf.CommsWindow_Close;
			//windowWidth = Mathf.Max(300f, Text.CalcSize(title).x + 20f);
			//windowHeight = 150f + Text.CalcHeight(description, windowWidth - 20f) + CloseButSize.y;
			windowWidth = 400f;
			windowHeight = 800f;
			this.map = map;
			this.pawn = pawn;
			this.options = options;
			this.eventTree = eventTree;
			this.index = index;
		}

        public override void PreOpen()
        {
            base.PreOpen();
            this.windowRect = new Rect(UI.screenWidth / 2 + windowWidth / 2, UI.screenHeight / 2 - windowHeight / 2, windowWidth, windowHeight);
            if (graphic != null)
            {
				Find.WindowStack.Add(new GraphicWindow(graphic, drawSize, drawOffset, windowWidth, windowHeight));
            }
        }

        public override void DoWindowContents(Rect inRect)
		{
			Rect contentRect = inRect.ContractedBy(10f);
			
			Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 40f), title);
			
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(contentRect.x, contentRect.y + Text.LineHeight + 5f, contentRect.width, Text.CalcHeight(description, contentRect.width)), description.Translate(pawn.NameShortColored));

			for (int i = 0; i < options.Count; i++)
			{
				Rect optionRect = new Rect(contentRect.x, inRect.height - 25f - (options.Count + 1) * Text.LineHeight + (i + 2) * Text.LineHeight, contentRect.width, Text.LineHeight);

				Widgets.DrawHighlightIfMouseover(optionRect);
				Widgets.Label(optionRect, options[i].text.Translate());

				if (Widgets.ButtonInvisible(optionRect))
				{
					if (options[i].action == "End")
                    {
						this.CloseAll();
						if (options[i].quest != null)
                        {
							this.GenerateQuest(map, options[i].quest);
                        }
					}
					else if (options[i].action == "ContinueWithBranch")
					{
						this.CloseAll();
						if (options[i].quest != null)
						{
							this.GenerateQuest(map, options[i].quest);
						}
						if (eventTree == null)
						{
							return;
						}
						if (options[i].jumpTo != -1)
						{
							EventTree s = eventTree[options[i].jumpTo];
							if (s != null)
							{
								Find.WindowStack.Add(new EventWindow_Rocivia(s.title, s.dialogue, s.graphic, s.drawSize, s.drawOffset, map, pawn, s.buttons, eventTree, options[i].jumpTo));
							}
						}
						else
						{
							EventTree s = eventTree[index + 1];
							if (s != null)
							{
								Find.WindowStack.Add(new EventWindow_Rocivia(s.title, s.dialogue, s.graphic, s.drawSize, s.drawOffset, map, pawn, s.buttons, eventTree, index + 1));
							}
						}
					}
					else if (options[i].action == "Back")
					{
						this.CloseAll();
						Find.WindowStack.Add(new MissionWindow("SRA.CommunicatorTitle".Translate(), "SRA.CommunicatorDescription".Translate(Find.World.GetComponent<MissionComponent>().intelligencePrimary, Find.World.GetComponent<MissionComponent>().intelligenceAdvanced), map, pawn));
						if (options[i].quest != null)
						{
							this.GenerateQuest(map, options[i].quest);
						}
					}
					else if (options[i].action == "Continue")
					{
						this.CloseAll();
						if (options[i].quest != null)
						{
							this.GenerateQuest(map, options[i].quest);
						}
						if (eventTree == null)
                        {
							return;
                        }
						if (options[i].jumpTo != -1)
                        {
							EventTree s = eventTree[options[i].jumpTo];
							if (s != null)
                            {
								Find.WindowStack.Add(new EventWindow_Rocivia(s.title, s.dialogue, s.graphic, s.drawSize, s.drawOffset, map, pawn, s.buttons, eventTree, options[i].jumpTo));
							}
						}
                        else
                        {
							EventTree s = eventTree[index + 1];
							if (s != null)
							{
								Find.WindowStack.Add(new EventWindow_Rocivia(s.title, s.dialogue, s.graphic, s.drawSize, s.drawOffset, map, pawn, s.buttons, eventTree, index + 1));
							}
						}
					}
				}
			}
		}

		public void CloseAll()
        {
			this.Close();
			IList<Window> l = Find.WindowStack.Windows;
			for (int i = 0; i < l.Count; i++)
            {
				Window w = l[i];
				if (w is GraphicWindow)
				{
					w.Close();
				}
			}
        }

		public void GenerateQuest(Map map, QuestScriptDef quest)
        {
			QuestUtility.SendLetterQuestAvailable(QuestUtility.GenerateQuestAndMakeAvailable(quest, new IncidentParms
			{
				target = map,
				points = StorytellerUtility.DefaultThreatPointsNow(map)
			}.points));
		}

		private readonly string title;

		private readonly string description;

		private Map map;

		private Pawn pawn;

		//public override Vector2 InitialSize => new Vector2(620f, 700f);
	}
}