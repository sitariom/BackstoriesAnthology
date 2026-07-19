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

        // Restores part of whatever the need lost this interval. Rises are untouched.
        // MED-029 fix: clamp to CurInstantLevel to prevent the need from exceeding
        // what the environment justifies (e.g. entering a beautiful room raises
        // CurInstantLevel, then grace period restores CurLevel — must not exceed new target).
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
            float newLevel = levelBefore - (levelBefore - levelAfter) * factor;
            // MED-029 fix: don't exceed CurInstantLevel when it's below levelBefore
            if (need.CurInstantLevel < newLevel)
            {
                newLevel = Mathf.Max(levelAfter, need.CurInstantLevel);
            }
            need.CurLevel = newLevel;
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

    /// <summary>
    /// HIGH-015 fix: patch Need_Comfort.NeedInterval directly instead of
    /// Need_Seeker.NeedInterval. The previous patch fired for Need_Mood,
    /// Need_Beauty, and Need_RoomSize too (all inherit Need_Seeker) — adding
    /// Harmony overhead to 3 need types that never used it. The fragile
    /// `is Need_Comfort` runtime filter is no longer needed.
    /// </summary>
    [HarmonyPatch(typeof(Need_Comfort), nameof(Need_Comfort.NeedInterval))]
    public static class Need_Comfort_NeedInterval_Patch
    {
        public static void Prefix(Need_Comfort __instance, ref float __state)
        {
            __state = __instance.CurLevel;
        }

        public static void Postfix(Need_Comfort __instance, float __state, Pawn ___pawn)
        {
            EarlyColonyNeeds.ScaleFall(__instance, __state, ___pawn);
        }
    }
}
