using RimWorld;
using SRA;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace SRA
{
    public class GameComponent_SRA_SRUtilTracker : GameComponent
    {
        public GameComponent_SRA_SRUtilTracker(Game game)
        {
        }

        Dictionary<Pawn, int> invincibility = new Dictionary<Pawn, int>();
        Dictionary<Pawn, int> resurrectability = new Dictionary<Pawn, int>();

        public Dictionary<Pawn, int> Invincibility => invincibility;
        public Dictionary<Pawn, int> Resurrectability => resurrectability;

        public List<Pawn> PawnsList;
        public List<int> intList;

        public List<Pawn> PawnsList2;
        public List<int> intList2;

        public Ideo ideo;

        public Pawn SRA_SR;

        public int FailedRescueCount;
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref invincibility, "Invisibility", LookMode.Reference, LookMode.Value, ref PawnsList, ref intList);
            Scribe_Collections.Look(ref resurrectability, "Resurrectability", LookMode.Reference, LookMode.Value, ref PawnsList2, ref intList2);

            Scribe_References.Look(ref ideo, "ideo");
            Scribe_References.Look(ref SRA_SR, "SRA_SR");

            Scribe_Values.Look(ref FailedRescueCount, "FailedRescueCount");
        }

        public bool SRA_SRInPeril => SRA_SR != null && !SRA_SR.Faction.IsPlayer && (SRA_SR.IsSlave || SRA_SR.IsPrisoner);

        public bool SRA_SRInPlayerHands => SRA_SR != null && SRA_SR.IsColonist;

        public bool SRA_SRAtHome => SRA_SR == null || (Find.WorldPawns.Contains(SRA_SR) && !(SRA_SR.IsColonist || SRA_SR.IsSlave || SRA_SR.IsPrisoner || SRA_SR.Dead));

        public bool SRA_SRInDestroyableContainer => SRA_SR != null && SRA_SR.ParentHolder is Thing thing && thing.def.useHitPoints;

        //Duplicates won't trigger rescue raids or others, but they do consider themselves as SRA_SR, moods and mental breaks still applies to them.
        public bool IsDuplicatedOrRealSRA_SR(Pawn p)
        {
            if (p == SRA_SR) return true;
            if (p.duplicate == null) return false;
            if (SRA_SR != null && p.duplicate.duplicateOf > int.MinValue)
            {
                return p.duplicate.duplicateOf == SRA_SR.thingIDNumber;
            }
            return false;
        }

        #region generic functions
        bool Validate(Pawn pawn, Dictionary<Pawn, int> dict, Action<Pawn, bool> action)
        {
            if (dict.NullOrEmpty())
            {
                return false;
            }

            if (dict.ContainsKey(pawn))
            {
                if (Find.TickManager.TicksGame > dict[pawn])
                {
                    action(pawn, true);
                    return false;
                }
                return true;
            }
            return false;
        }

        void Register(Pawn pawn, Dictionary<Pawn, int> dict, int amount, HediffDef hediffDef = null)
        {
            if (dict.ContainsKey(pawn))
            {
                dict[pawn] = Find.TickManager.TicksGame + amount;
            }
            else
            {
                dict.Add(pawn, Find.TickManager.TicksGame + amount);
            }
            AddOrRefreshHediff(pawn, amount, hediffDef);
        }

        void Deregister(Pawn pawn, Dictionary<Pawn, int> dict, bool skipCheck = false, Type compType = null)
        {
            if (skipCheck || dict.ContainsKey(pawn))
            {
                dict.Remove(pawn);
                if (compType != null)
                {
                    foreach (var c in pawn.health.hediffSet.GetHediffComps<HediffComp_StatusFlag>())
                    {
                        if (c.GetType() == compType) c.Remove();
                    }
                }
            }
        }

        void AddOrRefreshHediff(Pawn p, int amount, HediffDef hediffDef = null)
        {
            if (hediffDef == null) return;
            var hediff = p.health.hediffSet.GetFirstHediffOfDef(hediffDef) ?? newHediff();
            HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (hediffComp_Disappears != null)
            {
                hediffComp_Disappears.ticksToDisappear = amount;
            }

            Hediff newHediff()
            {
                hediff = HediffMaker.MakeHediff(hediffDef, p, null);
                hediff.TryGetComp<HediffComp_StatusFlag>()?.SetHandleStatus(false);
                p.health.AddHediff(hediff);
                return hediff;
            }
        }
        #endregion

        #region invincibility
        public bool ValidateInvincibility(Pawn pawn)
        {
            return Validate(pawn, invincibility, DeregisterInvincibility);
        }

        public void DeregisterInvincibility(Pawn pawn, bool skipCheck = false)
        {
            Deregister(pawn, invincibility, skipCheck, typeof(HediffComp_InvincibilityFlag));
        }

        public void RegisterInvincibility(Pawn pawn, int amount, HediffDef hediffDef = null)
        {
            Register(pawn, invincibility, amount, hediffDef);
        }
        #endregion

        #region resurrectability
        public bool ValidateResurrectability(Pawn pawn)
        {
            return Validate(pawn, resurrectability, DeregisterResurrectability);
        }

        public void RegisterResurrectability(Pawn pawn, int amount, HediffDef hediffDef = null)
        {
            Register(pawn, resurrectability, amount, hediffDef);
        }

        public void DeregisterResurrectability(Pawn pawn, bool skipCheck = false)
        {
            Deregister(pawn, resurrectability, skipCheck, typeof(HediffComp_ResurrectabilityFlag));
        }
        #endregion

        public override void LoadedGame()
        {
            SRA_SRPawnMaker.DebugGenFaction();
        }
    }
}
