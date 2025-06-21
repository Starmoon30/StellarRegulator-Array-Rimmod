using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace SRA
{
	public class GraphicWindow : Window
    {
		private float windowWidth = 800f;
        private float windowHeight = 800f;
        public override Vector2 InitialSize => new Vector2(windowWidth, windowHeight);
		public GraphicWindow(string texPath, float drawSize, float drawOffset, float adjustx, float adjusty)
		{
			this.texPath = texPath;
			closeOnAccept = false;
			closeOnCancel = false;
			this.drawSize = drawSize;
			soundAppear = SoundDefOf.CommsWindow_Open;
			soundClose = SoundDefOf.CommsWindow_Close;
			this.adjustx = adjustx;
			this.adjusty = adjusty;
			this.drawOffset = drawOffset;
		}
		//UI.screenWidth / 2 - adjustx / 2 - imageSize, UI.screenHeight / 2 - adjusty / 2,

		public void SetTexture(string texPath)
		{
			if (PreviewTexture == null)
			{
				PreviewTexture = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
			}
		}

		public override void PreOpen()
		{
			base.PreOpen();
			SetTexture(texPath);
		}

		public override void PostOpen()
		{
			base.PostOpen();
			this.windowRect = new Rect(UI.screenWidth / 2 - adjustx / 2 - windowWidth / 2, UI.screenHeight / 2 - adjusty / 2, windowWidth, windowHeight);
		}


		public override void DoWindowContents(Rect inRect)
		{
			Rect imageRect = new Rect((inRect.width - windowWidth) / 2f, (inRect.height - windowHeight) / 2f, windowWidth, windowHeight);
			Rect imageRectFix = new Rect((inRect.width - windowWidth) / 2f, (inRect.height - windowHeight - drawOffset) / 2f, windowWidth, windowHeight);
			Widgets.DrawTextureFitted(imageRect, PreviewTexture, drawSize);
		}

		private readonly string texPath;

		private Vector2 scrollPosition = Vector2.zero;

		private Texture2D PreviewTexture;


		private float adjustx;

		private float adjusty;

		private float drawSize;

		private float drawOffset;

		//public override Vector2 InitialSize => new Vector2(620f, 700f);
	}
}