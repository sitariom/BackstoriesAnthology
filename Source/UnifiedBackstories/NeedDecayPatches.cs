using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Survivors scraping a camp together don't expect a game of horseshoes.
    /// Recreation and comfort decay slowly during the colony's first days and
    /// ramp back to normal as the colony gets established.
    /// Days 0-5: 25% decay speed. Days 5-20: eases linearly back to 100%.
    /// Only applies to player-faction pawns; existing mid-game saves are unaffected.
    /// </summary>
    public static class EarlyColonyNeeds
    {
        private const float GraceDays = 5f;
        private const float RampEndDays = 20f;
        private const float MinDecayFactor = 0.25f;

        public static float DecayFactor(Pawn pawn)
        {
            if (UB_Mod.Settings != null && !UB_Mod.Settings.needsGracePeriod)
            {
                return 1f;
            }
            if (pawn == null || pawn.Faction == null || !pawn.Faction.IsPlayer)
            {
                return 1f;
            }
            float days = Find.TickManager.TicksGame / (float)GenDate.TicksPerDay;
            if (days >= RampEndDays)
            {
                return 1f;
            }
            if (days <= GraceDays)
            {
                return MinDecayFactor;
            }
            return Mathf.Lerp(MinDecayFactor, 1f, (days - GraceDays) / (RampEndDays - GraceDays));
        }

        // Restores part of whatever the need lost this interval. Rises are untouched,
        // and the vanilla clamp toward CurInstantLevel is never violated because we
        // only move the level back up toward its pre-interval value.
        public static void ScaleFall(Need need, float levelBefore, Pawn pawn)
        {
            float levelAfter = need.CurLevel;
            if (levelAfter >= levelBefore)
            {
                return;
            }
            float factor = DecayFactor(pawn);
            if (factor >= 0.999f)
            {
                return;
            }
            need.CurLevel = levelBefore - (levelBefore - levelAfter) * factor;
        }
    }

    [HarmonyPatch(typeof(Need_Joy), nameof(Need_Joy.NeedInterval))]
    public static class Need_Joy_NeedInterval_Patch
    {
        public static void Prefix(Need_Joy __instance, ref float __state)
        {
            __state = __instance.CurLevel;
        }

        public static void Postfix(Need_Joy __instance, float __state, Pawn ___pawn)
        {
            EarlyColonyNeeds.ScaleFall(__instance, __state, ___pawn);
        }
    }

    [HarmonyPatch(typeof(Need_Seeker), nameof(Need_Seeker.NeedInterval))]
    public static class Need_Seeker_NeedInterval_Patch
    {
        public static void Prefix(Need_Seeker __instance, ref float __state)
        {
            __state = __instance.CurLevel;
        }

        public static void Postfix(Need_Seeker __instance, float __state, Pawn ___pawn)
        {
            if (__instance is Need_Comfort)
            {
                EarlyColonyNeeds.ScaleFall(__instance, __state, ___pawn);
            }
        }
    }
}
