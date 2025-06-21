using System;
using System.Collections.Generic;
using Verse;

namespace SRA
{
	public class CompProperties_DamageFactors : CompProperties
    {
		public CompProperties_DamageFactors()
		{
			this.compClass = typeof(CompDamageFactors);
		}

		public List<DamageDef> whitelist = new List<DamageDef>();

        public float damageCap = 100f;

        public bool candamagedbyRanged = true;
        public bool candamagedbynotRanged = true;

		public int popOutCoolDown = 30;

		[MustTranslate]
		public string popOutString = "SRAimmune";
	}
}
