using System;
using Verse;
using UnityEngine;

namespace SRA
{
	public class SRASettings : ModSettings
	{
		public override void ExposeData()
		{
			base.ExposeData();
		}
		public static void DoWindowContents(Rect rect)
		{
			Listing_Standard listing_Standard = new Listing_Standard();
			listing_Standard.Begin(rect);
			listing_Standard.End();
		}

	}
}
