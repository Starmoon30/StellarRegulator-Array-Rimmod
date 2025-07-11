using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Verse;
using Verse.AI.Group;
using static System.Collections.Specialized.BitVector32;

namespace SRA
{
    public static class SRA_SRPawnMaker
    {
        static readonly PredefinedCharacterParmDef parm = SRA_DefOf.SRA_SRParm;


        public static GameComponent_SRA_SRUtilTracker Tracker => SRAMod.Tracker;

        const int maxTries = 100;

        public static Pawn GetPawn()
        {
            if (!SRAMod.AllowDuplicateSRA_SR && Tracker.SRA_SR != null)
            {
                Log.Error("Tried to spawn duplicate SRA_SR while mod setting disallow. This shouldn't happen. A new one is being generated regardless.");
            }

            var raceCache = parm.basePawnKindDef.race;
            parm.basePawnKindDef.race = parm.raceDef;
            Pawn p = PawnGenerator.GeneratePawn(generationRequest());
            if (p.def != parm.raceDef)
            {
                int i = 0;
                while (p.def != parm.raceDef)
                {
                    p = PawnGenerator.GeneratePawn(generationRequest());
                    i++;
                    if (i > maxTries)
                    {
                        Log.Error("Failed to generate SRA_SR with correct race. Returning with incorrect race. You have too many HAR race mods dammit.");
                        break;
                    }
                }
            }
            parm.basePawnKindDef.race = raceCache;

            PawnPostProcess(ref p);
            if (Tracker != null)
            {
                Tracker.SRA_SR = p;
            }
            return p;
        }

        public static void PawnPostProcess(ref Pawn p)
        {
            PawnStylePostProcess(p);
            PawnBioPostProcess(p);
            PawnEquipmentPostProcess(p);
            PawnIdeoPostProcess(p);

            if (SRAMod.NoseScar)
            {
                foreach (var scar in parm.scars)
                {
                    foreach (BodyPartRecord notMissingPart in p.health.hediffSet.GetNotMissingParts())
                    {
                        if (notMissingPart.def == scar.bodyPart && notMissingPart.def.canScarify)
                        {
                            Hediff hediff = HediffMaker.MakeHediff(scar.damage.hediff, p, notMissingPart);
                            hediff.Severity = scar.damageAmount;
                            HediffComp_GetsPermanent hediffComp_GetsPermanent = hediff.TryGetComp<HediffComp_GetsPermanent>();
                            hediffComp_GetsPermanent.IsPermanent = true;
                            hediffComp_GetsPermanent.SetPainCategory(PainCategory.Painless);
                            p.health.AddHediff(hediff);
                            break;
                        }
                    }
                }
            }
            foreach (var hediffInfo in parm.hediffs)
            {
                if (hediffInfo.bodyPart == null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(hediffInfo.hediff, p, null);
                    p.health.AddHediff(hediff);
                }
                else
                {
                    foreach (BodyPartRecord notMissingPart in p.health.hediffSet.GetNotMissingParts())
                    {
                        if ((hediffInfo.partCustomLabel != null && notMissingPart.untranslatedCustomLabel == hediffInfo.partCustomLabel)
                            || notMissingPart.def == hediffInfo.bodyPart)
                        {
                            Hediff hediff = HediffMaker.MakeHediff(hediffInfo.hediff, p, notMissingPart);
                            p.health.AddHediff(hediff);
                            break;
                        }
                    }
                }
            }

            if (parm.overrideFactionLeader)
            {
                p.Faction.leader = p;
            }
        }

        public static void PawnStylePostProcess(Pawn p)
        {
            p.story.headType = parm.headType;
            p.story.SkinColorBase = parm.skinColor;
            p.story.skinColorOverride = parm.skinColor;

            if (p.style != null)
            {
                p.style.FaceTattoo = parm.faceTattoo;
                p.style.BodyTattoo = parm.bodyTattoo;
            }

            p.story.hairDef = parm.forcedHairDef;
            p.story.HairColor = parm.hairColor;
        }

        public static void PawnEquipmentPostProcess(Pawn p)
        {

            if (parm.weaponDef != null)
            {
                if (p.equipment.Primary != null)
                {
                    p.equipment.Primary.Destroy();
                }
                Thing wep = ThingMaker.MakeThing(parm.weaponDef, parm.weaponStuffDef);
                if (wep.TryGetComp<CompQuality>() is CompQuality q)
                {
                    q.SetQuality(QualityCategory.Normal, null);
                }

                p.equipment.AddEquipment((ThingWithComps)wep);
            }

            if (parm.forcedApparels != null)
            {
                p.apparel.DestroyAll();
                p.outfits?.forcedHandler?.Reset();
                foreach (var app in parm.forcedApparels)
                {
                    Apparel apparel = (Apparel)ThingMaker.MakeThing(app, app.defaultStuff);
                    PawnGenerator.PostProcessGeneratedGear(apparel, p);
                    if (ApparelUtility.HasPartsToWear(p, apparel.def))
                    {
                        p.apparel.Wear(apparel, dropReplacedApparel: false);
                    }
                    PawnApparelGenerator.PostProcessApparel(apparel, p);
                }
            }
        }

        public static void PawnIdeoPostProcess(Pawn p)
        {
            if (ModLister.IdeologyInstalled && ModsConfig.IdeologyActive)
            {
                //If use faction ideo, set tracker's ideo to this just in case
                if (parm.useFactionIdeo)
                {
                    p.ideo.SetIdeo(p.Faction?.ideos?.PrimaryIdeo);
                    Tracker.ideo = p.ideo.Ideo;
                }
                else
                {
                    p.ideo.SetIdeo(GetIdeo());
                }
            }
        }

        static void PawnBioPostProcess(Pawn p)
        {
            p.kindDef = parm.basePawnKindDef;
            if (p.Name is NameTriple nameTriple)
            {
                p.Name = new NameTriple(parm.firstName, parm.nickname, parm.lastname);
                p.story.birthLastName = nameTriple.Last;
            }

            if (parm.abilities != null)
            {
                foreach (var ability in parm.abilities)
                {
                    p.abilities.GainAbility(ability);
                }
            }

            p.story.Childhood = parm.fixedChildBackStories.RandomElement();
            p.story.Adulthood = parm.fixedAdultBackStories.RandomElement();
            p.story.title = parm.title;
            foreach (BackstoryDef item in p.story.AllBackstories.Where((BackstoryDef bs) => bs != null))
            {
                foreach (SkillGain skillGain in item.skillGains)
                {
                    p.skills.GetSkill(skillGain.skill).Level += skillGain.amount;
                }
            }
            if (parm.traits != null)
            {
                p.story.traits.allTraits.Clear();
                foreach (TraitRequirement forcedTrait in parm.traits)
                {
                    p.story.traits.GainTrait(new Trait(forcedTrait.def, forcedTrait.degree ?? 0, forced: true));
                }
            }

            if (ModLister.BiotechInstalled)
            {
                foreach (var generemove in parm.removeGenes)
                {
                    p.genes.RemoveGene(p.genes.GetGene(generemove));
                }
            }

            if (parm.eyeColor != null && ModLister.GetActiveModWithIdentifier("nals.facialanimation") != null)
            {
                try
                {
                    Assembly FA = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FacialAnimation");
                    if (FA != null)
                    {
                        Type EyeCon = FA.GetType("FacialAnimation.EyeballControllerComp");
                        if (EyeCon != null)
                        {
                            var CompEyeControl = p.AllComps.Where(c => c.GetType() == EyeCon).FirstOrDefault();
                            if (CompEyeControl != null)
                            {
                                FieldInfo EyeColor = EyeCon.GetField("color", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (EyeColor != null)
                                {
                                    EyeColor.SetValue(CompEyeControl, parm.eyeColor);
                                    Log.Message("FA q");
                                }
                                FieldInfo EyeIIColor = EyeCon.GetField("secondColor", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (EyeIIColor != null)
                                {
                                    EyeIIColor.SetValue(CompEyeControl, parm.eyeColor);
                                }
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                }
            }
        }

        public static PawnGenerationRequest generationRequest()
        {
            PawnGenerationRequest req = new PawnGenerationRequest(parm.basePawnKindDef);
            //Not a cosplayer, must be a new pawn
            req.ForceGenerateNewPawn = true;
            req.ForceBodyType = parm.forcedBodyTypeDef;
            req.CanGeneratePawnRelations = false;
            req.MustBeCapableOfViolence = true;
            req.ForceAddFreeWarmLayerIfNeeded = true;
            req.AllowGay = false;
            req.AllowAddictions = false;
            req.FixedBiologicalAge = parm.fixedAge;
            req.FixedChronologicalAge = parm.fixedAge;
            req.FixedGender = parm.gender;
            req.FixedLastName = parm.lastname;
            req.ForcedXenotype = parm.baseXenotype;
            req.ForcedEndogenes = parm.endoGenes;
            req.ForcedXenogenes = parm.xenoGenes;
            req.ForceNoBackstory = true;
            req.Faction = FactionUtility.DefaultFactionFrom(parm.fixedFaction) ?? null;
            req.FixedIdeo = GetIdeo();
            return req;
        }

        public static Ideo GetIdeo()
        {
            if (parm.useFactionIdeo)
            {
                return null;
            }
            if (ModLister.IdeologyInstalled && ModsConfig.IdeologyActive)
            {
                //Just to make sure if multiple instances of SRA_SR exists, they'll have the same ideo.
                if (Tracker?.ideo != null)
                {
                    return Tracker.ideo;
                }
                IdeoGenerationParms parms = new IdeoGenerationParms(FactionDefOf.AncientsHostile, false, styles: parm.styles, disallowedPrecepts: parm.disallowedPreceptDefs, disallowedMemes: parm.disallowedMemes, forcedMemes: parm.forcedMemes, forceNoWeaponPreference: true, hidden: false, fixedIdeo: true);
                Faction f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(FactionDefOf.Beggars, parms, true));
                if (Tracker != null)
                {
                    Tracker.ideo = f.ideos.PrimaryIdeo;
                }
                return f.ideos.PrimaryIdeo;
            }
            return null;
        }

        [DebugAction("SRAMod", "Spawn SRA_SR", false, false, false, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
        public static void DebugSpawn()
        {
            Pawn pawn = GetPawn();
            GenSpawn.Spawn(pawn, UI.MouseCell(), Find.CurrentMap);
            PostDebugPawnSpawn(pawn);
        }

        [DebugAction("SRAMod", "Fetch Existing SRA_SR", false, false, false, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
        public static void DebugFetch()
        {
            Pawn pawn = Tracker.SRA_SR;
            if (pawn != null)
            {
                Thing thingToSpawn = pawn;
                if (pawn.Dead)
                {
                    thingToSpawn = pawn.Corpse ?? pawn.MakeCorpse(null, null);
                }
                if (thingToSpawn.Spawned)
                {
                    thingToSpawn.DeSpawn();
                }
                GenSpawn.Spawn(thingToSpawn, UI.MouseCell(), Find.CurrentMap);
                PostDebugPawnSpawn(pawn);
            }
            else
            {
                Log.Warning("Tried to fetch SRA_SR but doesn't exist, could be GCed or not generated yet.");
            }
        }

        private static void PostDebugPawnSpawn(Pawn pawn)
        {
            if (pawn.Spawned && pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
            {
                Lord lord = null;
                if (pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction).Any((Pawn p) => p != pawn))
                {
                    lord = ((Pawn)GenClosest.ClosestThing_Global(pawn.Position, pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction), 99999f, (Thing p) => p != pawn && ((Pawn)p).GetLord() != null)).GetLord();
                }
                if (lord == null || !lord.CanAddPawn(pawn))
                {
                    lord = LordMaker.MakeNewLord(pawn.Faction, new LordJob_DefendPoint(pawn.Position), Find.CurrentMap);
                }
                if (lord != null && lord.LordJob.CanAutoAddPawns && !lord.ownedPawns.Contains(pawn))
                {
                    lord.AddPawn(pawn);
                }
            }
            pawn.Rotation = Rot4.South;
        }

        [DebugAction("SRAMod", "Redesignate SRA_SR", false, false, false, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
        public static void DebugRedesignate(Pawn p)
        {
            Tracker.SRA_SR = p;
        }

        [DebugAction("SRAMod", "Clear SRA_SR Tracker", false, false, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
        public static void DebugClearTracker()
        {
            Tracker.SRA_SR = null;
        }

        [DebugAction("SRAMod", "Log SRA_SR Info", false, false, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
        public static void DebugInfo()
        {
            if (Tracker.SRA_SR == null)
            {
                Log.Message("SRA_SR not generated");
                return;
            }
            Pawn p = Tracker.SRA_SR;
            StringBuilder stb = new StringBuilder();
            stb.AppendLine("spawned: " + p.Spawned.ToString());
            stb.AppendLine("dead: " + p.Dead.ToString());
            stb.AppendLine("downed: " + p.Downed.ToString());
            stb.AppendLine("faction: " + p.Faction.ToString());
            stb.AppendLine("slave faction: " + (p.SlaveFaction == null ? "null" : p.SlaveFaction.ToString()));
            stb.AppendLine("host faction: " + (p.HostFaction == null ? "null" : p.HostFaction.ToString()));
            stb.AppendLine("worldpawn: " + Find.WorldPawns.Contains(p).ToString());
            stb.AppendLine("can reach map edge: " + p.CanReachMapEdge().ToString());
            stb.AppendLine("in peril: " + Tracker.SRA_SRInPeril.ToString());
            Log.Message(stb.ToString());
        }

    }
}
