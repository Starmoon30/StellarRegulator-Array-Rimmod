using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using System.Collections.Generic;
using System.Linq;
using Verse.AI.Group;

namespace SRA
{
	public class MissionComponent : WorldComponent
	{
		public MissionComponent(World world) : base(world)
		{
		}


		public EventWindow Script
        {
            get
            {
				List<EventWindow> trees = DefDatabase<EventWindow>.AllDefs.ToList().FindAll((EventWindow m) => !m.Ended && m.ID != -1);
				if (trees.Count == 0)
                {
					return null;
                }
                else
                {
					trees.SortBy((EventWindow m) => m.ID);
					return trees[0];
				}
			}
        }

		public override void ExposeData()
		{
			Scribe_Values.Look<bool>(ref blackMechDiscoverd, "blackMechDiscoverd", false, false);
			Scribe_Values.Look<bool>(ref factionRelationLock, "factionRelationLock", false, false);
			Scribe_Values.Look<bool>(ref keyGained, "keyGained", false, false);
			Scribe_Values.Look<bool>(ref militorSpawned, "militorSpawned", false, false);
			Scribe_Values.Look<bool>(ref apocritonDead, "apocritonDead", false, false);
			Scribe_Values.Look<bool>(ref scriptEnded, "scriptEnded", false, false);
			Scribe_Values.Look<int>(ref relation, "relation", 0, false);
			Scribe_Values.Look<int>(ref lastChangeTick, "lastChangeTick", 0, false);
			Scribe_Values.Look<int>(ref intelligencePrimary, "intelligencePrimary", 0, false);
			Scribe_Values.Look<int>(ref intelligenceAdvanced, "intelligenceAdvanced", 0, false);
			Scribe_Collections.Look<string, bool>(ref BranchDict, "BranchDict", LookMode.Value, LookMode.Value, ref tmpStrings, ref tmpBool, false);
			Scribe_Collections.Look<int>(ref script_Finished, "script_Finished", LookMode.Value, Array.Empty<object>());
			Scribe_Collections.Look<int>(ref script_Allowed, "script_Allowed", LookMode.Value, Array.Empty<object>());
		}

		public List<int> script_Finished = new List<int>();

		public List<int> script_Allowed = new List<int>();

		public bool blackMechDiscoverd = false;

		public bool factionRelationLock = false;

		public bool keyGained = false;

		public bool militorSpawned = false;

		public bool apocritonDead = false;

		public bool scriptEnded = false;

		public int relation = 0;

		public int intelligencePrimary;

		public int intelligenceAdvanced;

		public int lastChangeTick;

		private IntRange interval = new IntRange(-10000, 10000);

		public Dictionary<string, bool> BranchDict = new Dictionary<string, bool>();

		private List<string> tmpStrings = new List<string>();

		private List<bool> tmpBool = new List<bool>();


	}
}
