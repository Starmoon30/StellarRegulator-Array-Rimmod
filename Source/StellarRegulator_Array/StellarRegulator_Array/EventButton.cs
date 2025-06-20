using System;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace SRA
{
    public class EventButton
    {
        public string text;

        public string action = null;

        public QuestScriptDef quest = null;

        public int jumpTo = -1;

        public string branch = null;

        public bool to = false;
    }
}
