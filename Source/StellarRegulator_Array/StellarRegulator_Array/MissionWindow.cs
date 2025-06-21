using System;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SRA;
using KTrie;

namespace SRA
{
	public class MissionWindow : Window
	{
		private float windowWidth = 0f;
		private float windowHeight = 0f;
        private string[] options = { "SRA.WaitForDiscover".Translate(), "SRA.CommunicateWithRocivia".Translate(), "CloseButton".Translate() };

		private int selectedOption = -1;

		public override Vector2 InitialSize => new Vector2(windowWidth, windowHeight);

		public MissionWindow(string title, string description, Map map, Pawn pawn)
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
			// 绘制标题
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 40f), title);
			// 绘制描述
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(contentRect.x, contentRect.y + Text.LineHeight + 5f, contentRect.width, Text.CalcHeight(description, contentRect.width)), description);

			float recty2 = contentRect.y + Text.LineHeight + Text.CalcHeight(description, contentRect.width) + 5f;
			Widgets.DrawLineHorizontal(contentRect.x, recty2 + 5f, contentRect.width);


			for (int i = 0; i < options.Length; i++)
			{
				Rect optionRect = new Rect(contentRect.x, inRect.height - 25f - (options.Length + 1) * Text.LineHeight + (i + 2) * Text.LineHeight, contentRect.width, Text.LineHeight);

				Widgets.DrawHighlightIfMouseover(optionRect);
				Widgets.Label(optionRect, options[i]);

				if (Widgets.ButtonInvisible(optionRect))
				{
					selectedOption = i;
					if (selectedOption == 0)
						DoOption1();
					else if (selectedOption == 1)
						DoOption2();
					else if (selectedOption == 2)
						this.Close();
				}
			}
		}

        private void DoOption1()
        {
            this.Close();
        }
        private void DoOption2()
        {
            this.Close();
            Find.WindowStack.Add(new TradeWindow_Rocivia("SRA.CommunicateWithRocivia".Translate(), Find.World.GetComponent<MissionComponent>().script_Finished.Contains(300) ? "SRA.CommunicateWithRocivia".Translate() : "SRA.CommunicateWithRocivia".Translate(), map, pawn));
        }

		private readonly string title;

		private readonly string description;

		private Map map;

		private Pawn pawn;

		private Vector2 scrollPosition = Vector2.zero;

		//public override Vector2 InitialSize => new Vector2(620f, 700f);
	}
}