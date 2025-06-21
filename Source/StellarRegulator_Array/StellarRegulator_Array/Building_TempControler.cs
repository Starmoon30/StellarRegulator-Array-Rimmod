using System;
using RimWorld;
using Verse;

namespace SRA
{
	// Token: 0x02000004 RID: 4
	public class Building_TempControler : Building_TempControl
	{
		// Token: 0x0600000E RID: 14 RVA: 0x0000236D File Offset: 0x0000056D
		public override void TickRare()
		{
			this.GetRoom(RegionType.Set_Passable).Temperature = this.compTempControl.targetTemperature;
		}
	}
}
