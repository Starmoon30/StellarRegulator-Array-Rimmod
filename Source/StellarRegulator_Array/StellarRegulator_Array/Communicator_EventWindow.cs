using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace SRA
{
	public class Communicator_EventWindow : CompUseEffect
	{
		public Building Station
		{
			get
			{
				return this.parent as Building;
			}
		}

		public override AcceptanceReport CanBeUsedBy(Pawn p)
		{
			AcceptanceReport result;
            result = base.CanBeUsedBy(p);
            return result;
		}

		public override void DoEffect(Pawn user)
		{
			base.DoEffect(user);
			missionInfo = new MissionWindow("SRA.CommunicatorTitle".Translate(), "SRA.CommunicatorDescription".Translate(), this.parent.Map, user);
			Find.WindowStack.Add(missionInfo);
		}

		public MissionWindow missionInfo;

		public GraphicWindow graphicInfo;
	}
}
