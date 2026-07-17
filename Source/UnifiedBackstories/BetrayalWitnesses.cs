using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Reputation is social. When a pawn cheats on a colonist or sells their
    /// loved one, every other colonist on the map hears about it and loses a
    /// smaller amount of opinion of the offender - not just the victim.
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
        typeof(Thought_Memory), typeof(Pawn))]
    public static class BetrayalWitnesses_Patch
    {
        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            if (UB_Mod.Settings != null && !UB_Mod.Settings.betrayalWitnesses)
            {
                return;
            }
            if (otherPawn == null || newThought == null || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }
            // SoldMyLovedOne is from Ideology DLC — resolve dynamically to avoid crash without it
            ThoughtDef soldMyLovedOne = UB_DefOf.SoldMyLovedOneResolved();
            if (newThought.def != ThoughtDefOf.CheatedOnMe && (soldMyLovedOne == null || newThought.def != soldMyLovedOne))
            {
                return;
            }
            Pawn victim = __instance.pawn;
            if (victim == null || (victim.Faction != Faction.OfPlayer && otherPawn.Faction != Faction.OfPlayer))
            {
                return; // outsiders betraying outsiders is not the colony's business
            }
            Map map = victim.MapHeld ?? otherPawn.MapHeld;
            if (map == null)
            {
                return;
            }
            foreach (Pawn witness in map.mapPawns.FreeColonistsSpawned)
            {
                if (witness == victim || witness == otherPawn || witness.needs?.mood == null)
                {
                    continue;
                }
                witness.needs.mood.thoughts.memories.TryGainMemory(UB_DefOf.RMCB_HeardAboutBetrayal, otherPawn);
            }
        }
    }
}
