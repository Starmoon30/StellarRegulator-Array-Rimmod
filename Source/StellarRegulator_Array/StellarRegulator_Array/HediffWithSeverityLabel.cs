using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SRA
{
    public class HediffWithSeverityLabel : HediffWithComps
    {
        public override string LabelBase
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(base.LabelBase);
                stringBuilder.Append(": ");
                stringBuilder.Append((base.Severity / this.def.maxSeverity).ToStringPercent());
                return stringBuilder.ToString();
            }
        }
    }
    public class Hediff_ImplantWithSeverityLabel : HediffWithSeverityLabel
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            bool flag = base.Part == null;
            if (flag)
            {
                Log.Error(this.def.defName + " has null Part. It should be set before PostAdd.");
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            bool flag = Scribe.mode == LoadSaveMode.PostLoadInit && base.Part == null;
            if (flag)
            {
                Log.Error(base.GetType().Name + " has null part after loading.");
                this.pawn.health.hediffSet.hediffs.Remove(this);
            }
        }
    }
}
