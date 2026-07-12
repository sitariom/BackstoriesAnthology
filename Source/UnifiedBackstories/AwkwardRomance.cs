using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Detects loaded mods whose features fully supersede one of ours. The
    /// overlapping RMCB feature is auto-disabled ONLY while that mod is active;
    /// without it, everything stays on by default.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UB_ModCompat
    {
        // Less Stupid Romance Attempt already stops romance attempts after a
        // single rebuff - strictly stronger than our graduated cooldown, so
        // running both is redundant.
        public static readonly bool RomanceCooldownSuperseded;
        public static readonly string RomanceCooldownSupersededBy;

        static UB_ModCompat()
        {
            if (ModsConfig.IsActive("Mlie.LessStupidRomanceAttempt"))
            {
                RomanceCooldownSuperseded = true;
                RomanceCooldownSupersededBy = "Less Stupid Romance Attempt";
                Log.Message("[UB] Less Stupid Romance Attempt detected - romance cooldown auto-disabled (LSRA already handles it).");
            }
        }
    }

    /// <summary>
    /// Shared helpers for the "failed romance" mechanics.
    /// </summary>
    public static class RomanceAwkwardness
    {
        // Two pawns are awkward with each other while either one still carries a
        // recent failed-romance memory referencing the other. Those memories
        // decay over ~10 days, after which the pair "gets over it".
        public static bool AwkwardBetween(Pawn a, Pawn b)
        {
            return HasFailedRomanceMemoryToward(a, b) || HasFailedRomanceMemoryToward(b, a);
        }

        private static bool HasFailedRomanceMemoryToward(Pawn p, Pawn other)
        {
            List<Thought_Memory> mems = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null)
            {
                return false;
            }
            for (int i = 0; i < mems.Count; i++)
            {
                Thought_Memory m = mems[i];
                if ((m.def == ThoughtDefOf.RebuffedMyRomanceAttempt || m.def == ThoughtDefOf.FailedRomanceAttemptOnMe)
                    && m is Thought_MemorySocial ms && ms.otherPawn == other)
                {
                    return true;
                }
            }
            return false;
        }

        // How many times the initiator has been rebuffed recently, in total and
        // toward this specific recipient.
        public static void CountRebuffs(Pawn initiator, Pawn recipient, out int total, out int towardRecipient)
        {
            total = 0;
            towardRecipient = 0;
            List<Thought_Memory> mems = initiator?.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null)
            {
                return;
            }
            for (int i = 0; i < mems.Count; i++)
            {
                Thought_Memory m = mems[i];
                if (m.def == ThoughtDefOf.RebuffedMyRomanceAttempt)
                {
                    total++;
                    if (m is Thought_MemorySocial ms && ms.otherPawn == recipient)
                    {
                        towardRecipient++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pawns who keep failing romance attempts back off for a while. Give up on a
    /// specific person after two rebuffs; get generally discouraged after several.
    /// Composes with Less Stupid Romance Attempt (which is stricter) - both only
    /// ever push the weight down.
    /// </summary>
    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight))]
    public static class RomanceAttempt_Pause_Patch
    {
        public static void Postfix(ref float __result, Pawn initiator, Pawn recipient)
        {
            if (__result <= 0f)
            {
                return;
            }
            if (UB_ModCompat.RomanceCooldownSuperseded)
            {
                return; // another mod already handles rejection cooldowns
            }
            if (UB_Mod.Settings != null && !UB_Mod.Settings.romanceCooldown)
            {
                return;
            }
            RomanceAwkwardness.CountRebuffs(initiator, recipient, out int total, out int towardRecipient);
            if (towardRecipient >= 2)
            {
                __result = 0f; // stop chasing this particular person
                return;
            }
            if (total >= 3)
            {
                __result *= 0.15f; // discouraged with romance in general
            }
            else if (total == 2)
            {
                __result *= 0.5f;
            }
        }
    }

    /// <summary>
    /// Suppress normal chitchat between an awkward pair so they do Awkward Chat instead.
    /// </summary>
    [HarmonyPatch(typeof(InteractionWorker_Chitchat), nameof(InteractionWorker_Chitchat.RandomSelectionWeight))]
    public static class Chitchat_Suppress_Patch
    {
        public static void Postfix(ref float __result, Pawn initiator, Pawn recipient)
        {
            if (UB_Mod.Settings != null && !UB_Mod.Settings.awkwardChat)
            {
                return;
            }
            if (__result > 0f && RomanceAwkwardness.AwkwardBetween(initiator, recipient))
            {
                __result = 0f;
            }
        }
    }

    /// <summary>
    /// Awkward Chat only fires between a pair with a recent failed romance. Weight
    /// matches chitchat's base so it happens about as often as chitchat would have.
    /// </summary>
    public class InteractionWorker_UB_AwkwardChat : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (UB_Mod.Settings != null && !UB_Mod.Settings.awkwardChat)
            {
                return 0f;
            }
            if (initiator.Inhumanized() || recipient.Inhumanized())
            {
                return 0f;
            }
            return RomanceAwkwardness.AwkwardBetween(initiator, recipient) ? 1f : 0f;
        }
    }
}
