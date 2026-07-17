using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Trench camaraderie. Tracks three kinds of hardship on each map - hostile
    /// threats (raids, ambushes, infestations, mech clusters), large fires, and
    /// disease outbreaks. Colonists who actively participated get a mutual
    /// "survived hardship together" opinion memory when the hardship ends.
    /// Hiding in bed earns nothing.
    /// </summary>
    public class MapComponent_RMCBHardship : MapComponent
    {
        private const int CheckInterval = 250;
        private const int RecentCombatWindow = 2500;     // ~1 in-game hour
        private const int MinThreatDurationTicks = 2500; // ignore trivial blips
        private const int MinFireDurationTicks = 1250;

        private bool threatActive;
        private int threatStartTick;
        private List<Pawn> combatParticipants = new List<Pawn>();

        private bool fireActive;
        private int fireStartTick;
        private List<Pawn> fireParticipants = new List<Pawn>();

        private bool outbreakActive;
        private List<Pawn> outbreakParticipants = new List<Pawn>();

        public MapComponent_RMCBHardship(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckInterval != 17)
            {
                return;
            }
            if (UB_Mod.Settings != null && !UB_Mod.Settings.hardshipBonding)
            {
                return;
            }
            TickThreat();
            TickFire();
            TickDisease();
        }

        private void TickThreat()
        {
            int tick = Find.TickManager.TicksGame;
            if (GenHostility.AnyHostileActiveThreatToPlayer(map))
            {
                if (!threatActive)
                {
                    threatActive = true;
                    threatStartTick = tick;
                    combatParticipants.Clear();
                }
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (IsFighting(p, tick))
                    {
                        combatParticipants.Add(p);
                    }
                }
            }
            else if (threatActive)
            {
                threatActive = false;
                if (tick - threatStartTick >= MinThreatDurationTicks)
                {
                    GrantBonds(combatParticipants, "UB.SurvivedBattle");
                }
                combatParticipants.Clear();
            }
        }

        private static bool IsFighting(Pawn p, int tick)
        {
            if (p.Drafted)
            {
                return true;
            }
            Verse.AI.Pawn_MindState mind = p.mindState;
            if (mind == null)
            {
                return false;
            }
            return tick - mind.lastCombatantTick <= RecentCombatWindow
                || tick - mind.lastHarmTick <= RecentCombatWindow
                || tick - mind.lastEngageTargetTick <= RecentCombatWindow;
        }

        private void TickFire()
        {
            int tick = Find.TickManager.TicksGame;
            if (map.fireWatcher.LargeFireDangerPresent)
            {
                if (!fireActive)
                {
                    fireActive = true;
                    fireStartTick = tick;
                    fireParticipants.Clear();
                }
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (p.CurJobDef == JobDefOf.BeatFire)
                    {
                        fireParticipants.Add(p);
                    }
                }
            }
            else if (fireActive)
            {
                fireActive = false;
                if (tick - fireStartTick >= MinFireDurationTicks)
                {
                    GrantBonds(fireParticipants, "UB.SurvivedFire");
                }
                fireParticipants.Clear();
            }
        }

        private void TickDisease()
        {
            int sickNow = 0;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!HasSeriousDisease(p))
                {
                    continue;
                }
                sickNow++;
                if (outbreakActive)
                {
                    outbreakParticipants.Add(p);
                }
            }
            if (!outbreakActive && sickNow >= 2)
            {
                outbreakActive = true;
                outbreakParticipants.Clear();
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (HasSeriousDisease(p))
                    {
                        outbreakParticipants.Add(p);
                    }
                }
            }
            else if (outbreakActive && sickNow == 0)
            {
                outbreakActive = false;
                GrantBonds(outbreakParticipants, "UB.SurvivedOutbreak");
                outbreakParticipants.Clear();
            }
        }

        private static bool HasSeriousDisease(Pawn p)
        {
            List<Hediff> hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return false;
            }
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h.def.lethalSeverity > 0f && h is HediffWithComps hwc
                    && hwc.TryGetComp<HediffComp_Immunizable>() != null && !h.FullyImmune())
                {
                    return true;
                }
            }
            return false;
        }

        private void GrantBonds(List<Pawn> pawns, string messageKey)
        {
            List<Pawn> survivors = pawns
                .Where(p => p != null && !p.Dead && !p.Destroyed && p.needs?.mood != null && p.IsFreeColonist)
                .ToList();
            if (survivors.Count < 2)
            {
                return;
            }
            for (int i = 0; i < survivors.Count; i++)
            {
                for (int j = 0; j < survivors.Count; j++)
                {
                    if (i != j)
                    {
                        survivors[i].needs.mood.thoughts.memories.TryGainMemory(
                            UB_DefOf.RMCB_SurvivedHardshipTogether, survivors[j]);
                    }
                }
            }
            Messages.Message(
                messageKey.Translate(survivors.Count),
                new LookTargets(survivors),
                MessageTypeDefOf.PositiveEvent,
                historical: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref threatActive, "RMCB_threatActive");
            Scribe_Values.Look(ref threatStartTick, "RMCB_threatStartTick");
            Scribe_Values.Look(ref fireActive, "RMCB_fireActive");
            Scribe_Values.Look(ref fireStartTick, "RMCB_fireStartTick");
            Scribe_Values.Look(ref outbreakActive, "RMCB_outbreakActive");
            Scribe_Collections.Look(ref combatParticipants, "RMCB_combatParticipants", LookMode.Reference);
            Scribe_Collections.Look(ref fireParticipants, "RMCB_fireParticipants", LookMode.Reference);
            Scribe_Collections.Look(ref outbreakParticipants, "RMCB_outbreakParticipants", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                combatParticipants = combatParticipants ?? new List<Pawn>();
                fireParticipants = fireParticipants ?? new List<Pawn>();
                outbreakParticipants = outbreakParticipants ?? new List<Pawn>();
                combatParticipants.RemoveWhere(p => p == null);
                fireParticipants.RemoveWhere(p => p == null);
                outbreakParticipants.RemoveWhere(p => p == null);
            }
        }
    }
}
