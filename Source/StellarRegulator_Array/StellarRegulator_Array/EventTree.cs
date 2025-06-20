using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace SRA
{
    public class EventTree
    {
        public string title;

        public string dialogue;

        public string graphic = null;

        public float drawSize = 1f;

        public float drawOffset = 0f;

        public List<EventButton> buttons = new List<EventButton>();

        public EventWindow Parent
        {
            get
            {
                return DefDatabase<EventWindow>.AllDefs.First(d => d.eventTree.Contains(this));
            }
        }
    }
}
