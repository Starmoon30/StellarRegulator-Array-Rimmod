using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SRA
{
	public class CompDamageFactors : ThingComp
    {

        private CompProperties_DamageFactors Props
        {
            get
            {
                return this.props as CompProperties_DamageFactors;
            }
        }
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool SRAimmune)
        {
            SRAimmune = (dinfo.Def.harmsHealth && dinfo.Def.ExternalViolenceFor(this.parent) && ((!GenList.NotNullAndContains<DamageDef>(this.Props.whitelist, dinfo.Def) && ((!this.Props.candamagedbynotRanged && !dinfo.Def.isRanged) || (!this.Props.candamagedbyRanged && dinfo.Def.isRanged))) || dinfo.Amount >= this.Props.damageCap));
			bool flag = SRAimmune && Find.TickManager.TicksGame > this.nextPopOutTick;
            if (flag)
            {
                MoteMaker.ThrowText(this.parent.PositionHeld.ToVector3(), this.parent.MapHeld, this.Props.popOutString, 20f);
                this.nextPopOutTick = Find.TickManager.TicksGame + this.Props.popOutCoolDown;
            }
        }
		private int nextPopOutTick;
	}
}
