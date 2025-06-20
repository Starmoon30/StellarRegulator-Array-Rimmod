using System;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using KTrie;

namespace SRA
{
	public class TradeWindow_Rocivia : Window
	{
		private float windowWidth = 0f;
		private float windowHeight = 0f;

		private string[] options = { Find.World.GetComponent<MissionComponent>().scriptEnded ? "SRA.RociviaEventTreeEnd".Translate() : "SRA.RociviaEventTree".Translate(), "SRA.RociviaEventTree2".Translate() , "SRA.Back".Translate(), "CloseButton".Translate() };
		private int selectedOption = -1;

		public override Vector2 InitialSize => new Vector2(windowWidth, windowHeight);

		public TradeWindow_Rocivia(string title, string description, Map map, Pawn pawn)
		{
			this.title = title;
			this.description = description;
			forcePause = true;
			absorbInputAroundWindow = true;
			closeOnAccept = false;
			closeOnCancel = false;
			soundAppear = SoundDefOf.CommsWindow_Open;
			soundClose = SoundDefOf.CommsWindow_Close;
			//windowWidth = Mathf.Max(300f, Text.CalcSize(title).x + 20f);
			//windowHeight = 150f + Text.CalcHeight(description, windowWidth - 20f) + CloseButSize.y;
			windowWidth = 520f;
			windowHeight = 570f;
			this.map = map;
			this.pawn = pawn;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Rect contentRect = inRect.ContractedBy(10f);
			
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 40f), title);
			
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(contentRect.x, contentRect.y + Text.LineHeight + 5f, contentRect.width, Text.CalcHeight(description, contentRect.width)), description);

			float recty2 = contentRect.y + Text.LineHeight + Text.CalcHeight(description, contentRect.width) + 5f;

			for (int i = 0; i < options.Length; i++)
			{
				Rect optionRect = new Rect(contentRect.x, inRect.height - 25f - (options.Length + 1) * Text.LineHeight + (i + 2) * Text.LineHeight, contentRect.width, Text.LineHeight);

				Widgets.DrawHighlightIfMouseover(optionRect);
				Widgets.Label(optionRect, options[i]);

				if (Widgets.ButtonInvisible(optionRect))
				{
					selectedOption = i;
					if (selectedOption == 0)
						this.Option1();
					else if (selectedOption == 1)
						this.Option2();
					else if (selectedOption == 2)
						this.Close();
				}
			}
		}

		public void Option1()
		{
            this.Close();
        }

		public void Option2()
        {
            EventWindow tree = Find.World.GetComponent<MissionComponent>().Script;
			if (tree == null)
			{
				return;
			}

			if ((tree.questNeedToFinish != null && !Find.QuestManager.QuestsListForReading.Any((Quest q) => q.root == tree.questNeedToFinish)) || Find.QuestManager.QuestsListForReading.Any((Quest q) => q.root == tree.questNeedToFinish && q.TicksSinceCleanup == -1))
			{
				Messages.Message("GD.QuestNotFinish".Translate(), MessageTypeDefOf.NeutralEvent);
				return;
			}

			if (Find.QuestManager.QuestsListForReading.Any((Quest q) => q.root == tree.questNeedToFinish) && !Find.QuestManager.QuestsListForReading.Any((Quest q) => q.root == tree.questNeedToFinish && q.State == QuestState.EndedSuccess))
			{
				this.Close();
				EventTree s0 = tree.failed;
				Find.WindowStack.Add(new EventWindow_Rocivia(s0.title, s0.dialogue, s0.graphic, s0.drawSize, s0.drawOffset, map, pawn, new List<EventButton>() { MakeNewButton("Back", "GD.Back", tree.questNeedToFinish), MakeNewButton("End", "CloseButton", tree.questNeedToFinish) }, null, 0));
				return;
			}


			this.Close();
			List<EventTree> eventTree = tree.eventTree;
			EventTree s;
			int i;
			if (tree.branch != null && Find.World.GetComponent<MissionComponent>().BranchDict.TryGetValue(tree.branch, true))
            {
				s = eventTree[tree.to];
				i = tree.to;
            }
            else
            {
				s = eventTree[0];
				i = 0;
			}
			Find.WindowStack.Add(new EventWindow_Rocivia(s.title, s.dialogue, s.graphic, s.drawSize, s.drawOffset, map, pawn, s.buttons, eventTree, i));
			Find.World.GetComponent<MissionComponent>().script_Finished.Add(tree.ID);
		}


		private EventButton MakeNewButton(string action, string text, QuestScriptDef quest = null)
		{
            EventButton eventButton = new EventButton();
            eventButton.action = action;
            eventButton.text = text;
            eventButton.quest = quest;
			return eventButton;
		}

		private readonly string title;

		private readonly string description;

		private Map map;

		private Pawn pawn;

		//public override Vector2 InitialSize => new Vector2(620f, 700f);
	}
}