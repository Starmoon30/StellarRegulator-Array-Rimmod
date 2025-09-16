using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    public class Comp_SRAWarUnitSpawner : ThingComp
    {
        public bool AutoMode = true;
        public float Powerint = 0f;
        public int SRAWarUnitMax;
        public float SRAperCyclePowerint;
        public PawnKindDef currPawn;

        public CompProperties_SRAWarUnitSpawner Props => 
            (CompProperties_SRAWarUnitSpawner)props;

        public override void PostPostMake()
        {
            base.PostPostMake();
            // 从XML属性初始化最大值
            SRAWarUnitMax = Props.maxWarUnits;
            SRAperCyclePowerint = Props.perCyclePowerint;

            // 确保当前pawn种类有效
            if (currPawn == null || !Props.pawnKindDef.Contains(currPawn))
            {
                currPawn = Props.pawnKindDef?.FirstOrDefault();
            }
            
            // 如果还是没有有效的pawn种类，使用默认值
            if (currPawn == null)
            {
                currPawn = DefDatabase<PawnKindDef>.GetNamedSilentFail("SRA_Mech_WarUnit_S");
                if (currPawn == null && Props.pawnKindDef != null && Props.pawnKindDef.Count > 0)
                {
                    currPawn = Props.pawnKindDef[0];
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref AutoMode, "AutoMode", true);
            Scribe_Values.Look(ref Powerint, "Powerint", 0f);
            Scribe_Values.Look(ref SRAWarUnitMax, "SRAWarUnitMax", Props.maxWarUnits);
            Scribe_Values.Look(ref SRAperCyclePowerint, "SRAperCyclePowerint", Props.perCyclePowerint);
            Scribe_Defs.Look(ref currPawn, "currPawn");
        }

        public void MakeSRAWarUnit()
        {
            // 添加详细的日志输出以便调试
            if (Powerint < 1f)
            {
                return;
            }
            
            if (currPawn == null)
            {
                Log.Error("SRA: currPawn is null! Cannot spawn war unit.");
                return;
            }
            
            try
            {
                // 简化pawn生成请求
                PawnGenerationRequest request = new PawnGenerationRequest(
                    currPawn,
                    parent.Faction,
                    PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: true
                );
                
                Pawn newThing = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(newThing, parent.Position, parent.Map, WipeMode.Vanish);
                Powerint -= 1f;

                Hediff newHediff = HediffMaker.MakeHediff(SRA_DefOf.SRA_30000_CountdownDeath, newThing);
                newHediff.Severity = 1f;
                newThing.health.AddHediff(newHediff);

            }
            catch (Exception ex)
            {
                Log.Error("SRA: Failed to spawn war unit: " + ex.ToString());
            }
        }

        private int SRAWarUnitSpawnerTick;
        public override void CompTick()
        {
            base.CompTick();
            if (this.SRAWarUnitSpawnerTick <= 0)
            {
                this.SRAWarUnitSpawnerTick = 300;
                CompPowerTrader compPowerTrader = parent.TryGetComp<CompPowerTrader>();
                bool hasPower = compPowerTrader == null || compPowerTrader.PowerOn;

                if (!hasPower)
                {
                    return;
                }

                // 自动模式检测威胁
                if (AutoMode)
                {
                    bool hasThreat = GenHostility.AnyHostileActiveThreatTo(parent.Map, parent.Faction, false, false);
                    if (hasThreat)
                    {
                        MakeSRAWarUnit();
                    }
                }

                // 充能逻辑
                if (Powerint < SRAWarUnitMax)
                {
                    Powerint += SRAperCyclePowerint;
                }
            }
            else
            {
                this.SRAWarUnitSpawnerTick--;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 首先返回基类的Gizmo
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            // 自动模式切换按钮
            yield return new Command_Toggle
            {
                defaultLabel = "SRAWarUnitSpawnerAutoMode".Translate(),
                defaultDesc = "SRAWarUnitSpawnerAutoModeDesc".Translate(),
                icon = TexCommand.Attack,
                isActive = () => AutoMode,
                toggleAction = () => {
                    AutoMode = !AutoMode;
                }
            };
            
            // 手动生成按钮（仅在非自动模式显示）
            if (currPawn != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "SRAWarUnitSpawnerNoAuto".Translate(),
                    defaultDesc = "SRAWarUnitSpawnerNoAutoDesc".Translate(),
                    icon = currPawn.race?.uiIcon ?? BaseContent.BadTex,
                    action = () => {
                        MakeSRAWarUnit();
                    }
                };
            }
            
            // 单位选择按钮
            if (currPawn != null && Props?.pawnKindDef != null && Props.pawnKindDef.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ChangeSRAWarUnit".Translate(),
                    defaultDesc = currPawn.race?.description ?? "No description",
                    icon = currPawn.race?.uiIcon ?? BaseContent.BadTex,
                    action = () => {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (PawnKindDef pkd in Props.pawnKindDef)
                        {
                            options.Add(new FloatMenuOption(
                                pkd.LabelCap, 
                                () => {
                                    currPawn = pkd;
                                },
                                pkd.race?.uiIcon,Color.white
                            ));
                        }
                        
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                };
            }
            else
            {
                Log.Warning("SRA: Cannot create ChangeSRAWarUnit button - currPawn or Props.pawnKindDef is invalid");
            }
        }

        public override string CompInspectStringExtra()
        {
            string baseStr = base.CompInspectStringExtra();
            string status = "SRAWarUnitSpawnerHaveSRAWarUnit".Translate(
                Powerint.ToString("F1"), 
                SRAWarUnitMax.ToString()
            );
            
            return string.IsNullOrEmpty(baseStr) ? status : $"{baseStr}\n{status}";
        }
    }
}