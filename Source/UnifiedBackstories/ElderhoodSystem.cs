using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UnifiedBackstories
{
    // ================================================================
    // COMP PROPERTIES & COMP
    // ================================================================

    public class CompProperties_ElderhoodBackstory : CompProperties
    {
        public int elderhoodAge = 60;
        public CompProperties_ElderhoodBackstory()
        {
            compClass = typeof(CompElderhoodBackstory);
        }
    }

    public class CompElderhoodBackstory : ThingComp
    {
        private int elderhoodAge = 60;
        private BackstoryDef elderhoodBackstory;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            if (props is CompProperties_ElderhoodBackstory p)
                elderhoodAge = p.elderhoodAge;
        }

        public int ElderhoodAge => elderhoodAge;
        public BackstoryDef ElderhoodBS => elderhoodBackstory;
        public bool HasElderhood => elderhoodBackstory != null;
        public void SetElderhood(BackstoryDef bs) { elderhoodBackstory = bs; }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref elderhoodAge, "elderhoodAge", 60);
            Scribe_Defs.Look(ref elderhoodBackstory, "elderhoodBackstory");
        }
    }

    // ================================================================
    // HELPER METHODS (mirrors the original ElderhoodBackstory.Utility)
    // ================================================================

    public static class ElderhoodHelper
    {
        private static readonly Regex GenderTokenRegex = new Regex(@"\{PAWN_gender \? (.+?) : (.+?)\}", RegexOptions.Compiled);

        /// <summary>
        /// Processes {PAWN_gender ? male_text : female_text} tokens in backstory descriptions.
        /// This was originally handled by the ElderhoodBackstory.dll's custom formatter.
        /// </summary>
        public static string ProcessGenderTokens(string text, Pawn pawn)
        {
            if (string.IsNullOrEmpty(text) || pawn == null)
                return text;
            return GenderTokenRegex.Replace(text, match =>
            {
                string maleOpt = match.Groups[1].Value.Trim();
                string femaleOpt = match.Groups[2].Value.Trim();
                return (pawn.gender == Gender.Female) ? femaleOpt : maleOpt;
            });
        }

        /// <summary>
        /// Picks elderhood for pawn. Player colonists/prisoners/slaves get special variants.
        /// Others get a random elderhood from the pool.
        /// </summary>
        public static BackstoryDef GetElderhoodFor(Pawn pawn)
        {
            if (pawn?.story == null) return null;
            BackstoryDef bs = null;

            // Player-assigned roles get special elderhoods
            if (pawn.IsColonist)
                bs = DefDatabase<BackstoryDef>.GetNamedSilentFail("UB_Elderhood_PlayerColonist");
            if (bs == null && pawn.IsPrisonerOfColony)
                bs = DefDatabase<BackstoryDef>.GetNamedSilentFail("UB_Elderhood_PlayerPrisoner");
            if (bs == null && pawn.IsSlaveOfColony)
                bs = DefDatabase<BackstoryDef>.GetNamedSilentFail("UB_Elderhood_PlayerSlave");

            // Fallback: random elderhood from the pool
            if (bs == null)
                bs = RandomElderhood();

            return bs;
        }

        private static List<BackstoryDef> _cachedElderhoods;
        private static List<BackstoryDef> _cachedAllElderhoods;

        private static void RebuildElderhoodCache()
        {
            var all = DefDatabase<BackstoryDef>.AllDefsListForReading;
            _cachedElderhoods = all.Where(d => d.spawnCategories != null &&
                d.spawnCategories.Contains("Elderhood")).ToList();
            _cachedAllElderhoods = all.Where(d => d.spawnCategories != null &&
                (d.spawnCategories.Contains("Elderhood") ||
                 d.spawnCategories.Contains("SpecialElderhood"))).ToList();
        }

        private static BackstoryDef RandomElderhood()
        {
            if (_cachedElderhoods == null) RebuildElderhoodCache();
            return _cachedElderhoods.Count > 0 ? _cachedElderhoods.RandomElement() : null;
        }

        public static bool IsElderhood(this BackstoryDef bs)
        {
            if (bs?.spawnCategories == null) return false;
            return bs.spawnCategories.Contains("Elderhood") ||
                   bs.spawnCategories.Contains("SpecialElderhood");
        }

        /// <summary>
        /// Returns all valid elderhood backstories for selection UI.
        /// </summary>
        public static List<BackstoryDef> ListElderhoods()
        {
            if (_cachedAllElderhoods == null) RebuildElderhoodCache();
            return _cachedAllElderhoods;
        }

        public static string TitleFor(BackstoryDef bs, Pawn pawn)
        {
            if (bs == null) return "";
            return (pawn.gender == Gender.Female && !bs.titleFemale.NullOrEmpty())
                ? bs.titleFemale : bs.title;
        }

        public static string TitleShortFor(BackstoryDef bs, Pawn pawn)
        {
            if (bs == null) return "";
            return (pawn.gender == Gender.Female && !bs.titleShortFemale.NullOrEmpty())
                ? bs.titleShortFemale : bs.titleShort;
        }

        public static string TitleCapFor(BackstoryDef bs, Pawn pawn)
        {
            return TitleFor(bs, pawn).CapitalizeFirst();
        }

        private static FieldInfo _pawnFieldCache;
        public static Pawn GetPawnFromTracker(object tracker)
        {
            if (tracker == null) return null;
            Type t = tracker.GetType();
            if (_pawnFieldCache == null || _pawnFieldCache.DeclaringType != t)
            {
                _pawnFieldCache = t.GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?? t.GetField("Pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            return _pawnFieldCache?.GetValue(tracker) as Pawn;
        }

        /// <summary>
        /// Ensures the CompElderhoodBackstory exists on a pawn.
        /// HIGH-005 fix: returns null (not a detached comp) if reflection fails,
        /// so callers don't silently mutate a comp that won't be saved.
        /// HIGH-009 fix: for non-Human human-like races (androids, xenotypes),
        /// the reflection-created comp does not survive save/load. Callers must
        /// be prepared for null returns and treat that as "elderhood unavailable".
        /// </summary>
        public static CompElderhoodBackstory GetOrCreateElderhoodComp(Pawn pawn)
        {
            if (pawn == null) return null;
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp != null) return comp;

            // Manual creation fallback — this should rarely execute since
            // CompProperties_ElderhoodBackstory is added to Human via XML patch.
            // If reflection fails, return null (callers already null-check).
            // Returning a detached comp would cause silent data loss on save/load.
            try
            {
                var props = new CompProperties_ElderhoodBackstory();
                comp = new CompElderhoodBackstory();
                comp.parent = pawn;
                comp.Initialize(props);

                var compsField = typeof(ThingWithComps).GetField("comps",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (compsField != null)
                {
                    var list = compsField.GetValue(pawn);
                    if (list is System.Collections.IList ilist)
                    {
                        ilist.Add(comp);
                        return comp;
                    }
                }
                // compsField null or cast failed — cannot attach comp. Return null.
                Log.Warning("[UB] GetOrCreateElderhoodComp: could not attach comp to pawn " + pawn?.Name?.ToStringFull ?? "(null)");
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning("[UB] GetOrCreateElderhoodComp reflection failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Core method: fills the elderhood slot for the pawn.
        /// Mirrors original Utility.FillBackstorySlotShuffled.
        /// </summary>
        public static void FillBackstorySlotShuffled(Pawn pawn, Faction faction = null)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            if (pawn.ageTracker == null) return;

            CompElderhoodBackstory comp = GetOrCreateElderhoodComp(pawn);
            // HIGH-001 fix: GetOrCreateElderhoodComp returns null when reflection fails
            // (androids, xenotypes, or any pawn whose comps field can't be accessed).
            // Null-check must come before accessing HasElderhood or ElderhoodAge.
            if (comp == null) return;
            if (comp.HasElderhood) return;

            // Only assign elderhood to pawns aged 60+
            if (pawn.ageTracker.AgeBiologicalYears < comp.ElderhoodAge) return;

            BackstoryDef elderhood = GetElderhoodFor(pawn);
            if (elderhood != null)
            {
                comp.SetElderhood(elderhood);
                pawn.skills?.Notify_SkillDisablesChanged();
            }
        }

        /// <summary>
        /// Assigns elderhood from existing patches (birthday, load, etc.)
        /// </summary>
        public static void TryAssignElderhood(Pawn pawn)
        {
            FillBackstorySlotShuffled(pawn, null);
        }

        /// <summary>
        /// Removes all effects of an elderhood backstory from a pawn.
        /// Used when changing or clearing elderhood.
        /// </summary>
        private static void RemoveElderhoodEffects(Pawn pawn, BackstoryDef oldElderhood)
        {
            if (oldElderhood == null) return;

            // Remove skill bonuses (subtract from current level)
            if (oldElderhood.skillGains != null && pawn.skills != null)
            {
                for (int i = 0; i < oldElderhood.skillGains.Count; i++)
                {
                    SkillGain sg = oldElderhood.skillGains[i];
                    if (sg.skill == null) continue;
                    SkillRecord skill = pawn.skills.GetSkill(sg.skill);
                    if (skill != null)
                    {
                        skill.Level = Mathf.Clamp(skill.Level - sg.amount, 0, 20);
                    }
                }
            }

            // Remove forced traits (only if pawn has them with matching degree)
            if (oldElderhood.forcedTraits != null && pawn.story?.traits != null)
            {
                for (int i = 0; i < oldElderhood.forcedTraits.Count; i++)
                {
                    BackstoryTrait te = oldElderhood.forcedTraits[i];
                    if (te?.def == null) continue;
                    if (pawn.story.traits.HasTrait(te.def, te.degree))
                    {
                        Trait t = pawn.story.traits.GetTrait(te.def, te.degree);
                        if (t != null)
                            pawn.story.traits.RemoveTrait(t);
                    }
                }
            }
        }

        /// <summary>
        /// Changes a pawn's elderhood from one backstory to another (or to null).
        /// Properly removes old effects (skills, traits) and applies new ones.
        /// Called from the UI Change button, Clear button, and age regression.
        /// 
        /// BUG FIX: Previously, SetElderhood() was a simple field assignment that
        /// did NOT remove old skill bonuses or forced traits, and did NOT apply
        /// new ones. Changing elderhood from A to B left A's skills/traits in place
        /// and never added B's skills/traits.
        /// </summary>
        public static void ChangeElderhood(Pawn pawn, CompElderhoodBackstory comp, BackstoryDef newElderhood)
        {
            BackstoryDef oldElderhood = comp.ElderhoodBS;

            // Step 1: Remove old elderhood effects
            if (oldElderhood != null)
            {
                RemoveElderhoodEffects(pawn, oldElderhood);
            }

            // Step 2: Set the new elderhood
            comp.SetElderhood(newElderhood);

            // Step 3: Apply new elderhood effects
            if (newElderhood != null)
            {
                // Apply forced traits
                if (newElderhood.forcedTraits != null && pawn.story?.traits != null)
                {
                    for (int i = 0; i < newElderhood.forcedTraits.Count; i++)
                    {
                        BackstoryTrait te = newElderhood.forcedTraits[i];
                        if (te?.def == null) continue;
                        if (pawn.story.traits.HasTrait(te.def)) continue;
                        pawn.story.traits.GainTrait(new Trait(te.def, te.degree, false));
                    }
                }

                // Apply skill bonuses
                if (newElderhood.skillGains != null && pawn.skills != null)
                {
                    for (int i = 0; i < newElderhood.skillGains.Count; i++)
                    {
                        SkillGain sg = newElderhood.skillGains[i];
                        if (sg.skill == null) continue;
                        SkillRecord skill = pawn.skills.GetSkill(sg.skill);
                        if (skill != null)
                        {
                            skill.Level = Mathf.Clamp(skill.Level + sg.amount, 0, 20);
                        }
                    }
                }
            }

            // Step 4: Notify skill system
            pawn.skills?.Notify_SkillDisablesChanged();
        }

        /// <summary>
        /// Applies ALL elderhood effects to a pawn: forced traits, skill bonuses,
        /// and body type. Called when elderhood is newly assigned (birthday path,
        /// save-load path). The GeneratePawn path uses ApplyElderhoodEffects in
        /// the GeneratePawn postfix instead.
        /// 
        /// BUG FIX: Previously, pawns who aged into elderhood via BirthdayBiological
        /// only got the backstory + body type, but NOT skill bonuses or forced traits.
        /// This method ensures all effects are applied regardless of how elderhood
        /// was assigned.
        /// </summary>
        public static void ApplyAllElderhoodEffects(Pawn pawn, CompElderhoodBackstory comp)
        {
            BackstoryDef eb = comp.ElderhoodBS;
            if (eb == null) return;

            // Apply forced traits
            if (eb.forcedTraits != null && pawn.story?.traits != null)
            {
                for (int i = 0; i < eb.forcedTraits.Count; i++)
                {
                    BackstoryTrait te = eb.forcedTraits[i];
                    if (te?.def == null) continue;
                    if (pawn.story.traits.HasTrait(te.def)) continue;
                    pawn.story.traits.GainTrait(new Trait(te.def, te.degree, false));
                }
            }

            // Apply skill bonuses (FinalLevelOfSkill won't fire again for existing pawns)
            if (eb.skillGains != null && pawn.skills != null)
            {
                for (int i = 0; i < eb.skillGains.Count; i++)
                {
                    SkillGain sg = eb.skillGains[i];
                    if (sg.skill == null) continue;
                    SkillRecord skill = pawn.skills.GetSkill(sg.skill);
                    if (skill != null)
                    {
                        skill.Level = Mathf.Clamp(skill.Level + sg.amount, 0, 20);
                    }
                }
            }

            // Apply body type
            BodyTypeDef bt = DefDatabase<BodyTypeDef>.GetNamedSilentFail(
                pawn.gender == Gender.Female ? "FemaleElderly" : "MaleElderly");
            if (bt != null && pawn.story?.bodyType != null)
                pawn.story.bodyType = bt;

            // Notify skill system to re-evaluate
            pawn.skills?.Notify_SkillDisablesChanged();
        }
    }

    // ================================================================
    // HARMONY PATCHES
    // ================================================================

    /// <summary>
    /// Processes {PAWN_gender ? X : Y} tokens in all backstory descriptions.
    /// Handles the custom gender markup from the original ElderhoodBackstory mod.
    /// </summary>
    /// <summary>
    /// NOTE: In RimWorld 1.6, FullDescriptionFor returns TaggedString (not string).
    /// The __result type MUST match the exact return type or Harmony throws:
    /// "Cannot assign method return type TaggedString to __result type String"
    /// TaggedString implicitly converts to/from string, so the logic is unchanged.
    /// </summary>
    [HarmonyPatch(typeof(BackstoryDef), "FullDescriptionFor")]
    public static class BackstoryDef_FullDescriptionFor_Patch
    {
        public static void Postfix(BackstoryDef __instance, Pawn p, ref Verse.TaggedString __result)
        {
            if (__result.NullOrEmpty() || p == null) return;
            __result = ElderhoodHelper.ProcessGenderTokens(__result, p);
        }
    }

    /// <summary>
    /// When TryGiveSolidBioTo succeeds, fill the elderhood slot.
    /// Original: ElderhoodBackstory.Patches.PawnBioAndNameGenerator_TryGiveSolidBioTo_ElderhoodBackstoryPatch
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "TryGiveSolidBioTo")]
    public static class PawnBioAndNameGenerator_TryGiveSolidBioTo_Patch
    {
        public static void Postfix(Pawn pawn, string requiredLastName,
            List<BackstoryCategoryFilter> backstoryCategories, ref bool __result)
        {
            if (__result)
                ElderhoodHelper.FillBackstorySlotShuffled(pawn, null);
        }
    }

    /// <summary>
    /// When GiveShuffledBioTo runs and didn't force no-backstory, fill elderhood slot.
    /// Original: ElderhoodBackstory.Patches.PawnBioAndNameGenerator_GiveShuffledBioTo_ElderhoodBackstoryPatch
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GiveShuffledBioTo")]
    public static class PawnBioAndNameGenerator_GiveShuffledBioTo_Patch
    {
        public static void Postfix(Pawn pawn, FactionDef factionType,
            string requiredLastName, List<BackstoryCategoryFilter> backstoryCategories,
            bool forceNoBackstory, bool forceNoNick, XenotypeDef xenotype,
            bool onlyForcedBackstories)
        {
            if (!forceNoBackstory)
                ElderhoodHelper.FillBackstorySlotShuffled(pawn, null);
        }
    }

    /// <summary>
    /// Apply forced traits from elderhood backstory.
    /// Original: ElderhoodBackstory.Patches.PawnGenerator_GenerateTraits_ElderhoodBackstoryPatch
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GenerateTraits")]
    public static class PawnGenerator_GenerateTraits_ElderhoodPatch
    {
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            if (pawn?.story == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;

            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;
            BackstoryDef elderhood = comp.ElderhoodBS;
            if (elderhood?.forcedTraits == null) return;

            for (int i = 0; i < elderhood.forcedTraits.Count; i++)
            {
                BackstoryTrait te = elderhood.forcedTraits[i];
                // LOW-016 fix: removed redundant te.def null check (already verified above)
                if (te == null || te.def == null) continue;
                if (pawn.story.traits == null) continue;

                // Skip if pawn already has this trait
                if (pawn.story.traits.HasTrait(te.def)) continue;

                // Skip if trait is prohibited
                var prohibited = request.ProhibitedTraits;
                if (prohibited != null && prohibited.Contains(te.def))
                    continue;

                pawn.story.traits.GainTrait(new Trait(te.def, te.degree, false));
            }
        }
    }

    /// <summary>
    /// Birthday patch: assign elderhood when pawn turns old enough.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Pawn_AgeTracker_BirthdayBiological_Patch
    {
        public static void Postfix(Pawn_AgeTracker __instance)
        {
            Pawn pawn = ElderhoodHelper.GetPawnFromTracker(__instance);
            if (pawn == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            bool hadElderhood = pawn.GetComp<CompElderhoodBackstory>()?.HasElderhood == true;
            ElderhoodHelper.TryAssignElderhood(pawn);

            // BUG FIX: Apply ALL elderhood effects (skills, traits, body type) when
            // elderhood is newly assigned via birthday. Previously only body type
            // was applied — skill bonuses and forced traits were missing.
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp != null && comp.HasElderhood && !hadElderhood)
            {
                ElderhoodHelper.ApplyAllElderhoodEffects(pawn, comp);
            }
        }
    }

    /// <summary>
    /// HIGH-010 fix: Age regression (Biotech age reversal serum) must clear
    /// elderhood when biological age drops below ElderhoodAge. Without this,
    /// a de-aged pawn keeps elderhood backstory, work disables, and UI section
    /// indefinitely. Age reversal changes age via AgeBiologicalTicks, not via
    /// BirthdayBiological, so we patch the setter.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker), "set_AgeBiologicalTicks")]
    public static class Pawn_AgeTracker_SetAgeBiologicalTicks_Patch
    {
        public static void Postfix(Pawn_AgeTracker __instance, long value)
        {
            Pawn pawn = ElderhoodHelper.GetPawnFromTracker(__instance);
            if (pawn == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;

            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;

            // RimWorld: 3,600,000 ticks per year.
            int ageYears = Mathf.FloorToInt(value / 3600000f);
            if (ageYears < comp.ElderhoodAge)
            {
                // BUG FIX: Use ChangeElderhood to properly remove skill bonuses
                // and forced traits from the old elderhood before clearing.
                ElderhoodHelper.ChangeElderhood(pawn, comp, null);
            }
        }
    }

    /// <summary>
    /// Post-load: assign elderhood for save game compatibility.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_StoryTracker), "ExposeData")]
    public static class Pawn_StoryTracker_ExposeData_Patch
    {
        public static void Postfix(Pawn_StoryTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            Pawn pawn = ElderhoodHelper.GetPawnFromTracker(__instance);
            if (pawn == null) return;
            ElderhoodHelper.TryAssignElderhood(pawn);
        }
    }

    /// <summary>
    /// GeneratePawn postfix — primary entry point for elderhood assignment.
    /// Runs AFTER all initialization, so age is guaranteed correct.
    /// Also applies effects (traits, skills, body type) since the per-step
    /// patches may have fired before elderhood was set.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_Patch
    {
        public static void Postfix(Pawn __result, PawnGenerationRequest request)
        {
            if (__result == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            if (__result.ageTracker == null) return;
            // MED-022 fix: skip non-human pawns (animals, mechanoids) to avoid
            // polluting their comps list with an empty CompElderhoodBackstory.
            if (!__result.RaceProps.Humanlike) return;

            CompElderhoodBackstory comp = ElderhoodHelper.GetOrCreateElderhoodComp(__result);
            if (comp == null) return;

            // HIGH-001 fix: use comp.ElderhoodAge (XML-configurable) instead of
            // hardcoded 60. Fall back to 60 if comp has default age.
            if (__result.ageTracker.AgeBiologicalYears < comp.ElderhoodAge) return;

            // Assign elderhood if not already set
            if (!comp.HasElderhood)
            {
                BackstoryDef elderhood = ElderhoodHelper.GetElderhoodFor(__result);
                if (elderhood == null) return;
                comp.SetElderhood(elderhood);
                __result.skills?.Notify_SkillDisablesChanged();

                // HIGH-007 fix: Apply skill bonuses here because FinalLevelOfSkill
                // already fired (during GeneratePawn, before this postfix) with
                // HasElderhood=false, so it added no bonus. We must manually apply
                // them now to avoid silent gameplay corruption for forceNoBackstory
                // pawns, raiders, and quest pawns generated at age 60+.
                ApplyElderhoodSkillBonuses(__result, elderhood);
            }

            // Apply effects that the per-step patches may have missed
            // because they fired before elderhood was assigned.
            ApplyElderhoodEffects(__result, comp, request);
        }

        private static void ApplyElderhoodEffects(Pawn pawn, CompElderhoodBackstory comp, PawnGenerationRequest request)
        {
            BackstoryDef eb = comp.ElderhoodBS;
            if (eb == null) return;

            // Apply forced traits
            if (eb.forcedTraits != null && pawn.story?.traits != null)
            {
                for (int i = 0; i < eb.forcedTraits.Count; i++)
                {
                    BackstoryTrait te = eb.forcedTraits[i];
                    if (te?.def == null) continue;
                    if (pawn.story.traits.HasTrait(te.def)) continue;
                    var prohibited = request.ProhibitedTraits;
                    if (prohibited != null && prohibited.Contains(te.def)) continue;
                    pawn.story.traits.GainTrait(new Trait(te.def, te.degree, false));
                }
            }

            // Skill bonuses for the NORMAL path are applied by
            // PawnGenerator_FinalLevelOfSkill_Patch (which fires during bio
            // generation, before this postfix). For the LATE-ASSIGNMENT path
            // (forceNoBackstory, raiders, quest pawns), skill bonuses are applied
            // by ApplyElderhoodSkillBonuses above, in the AssignElderhood block.
            // Do NOT apply them again here — would double-count for normal pawns.

            // Apply body type
            BodyTypeDef bt = DefDatabase<BodyTypeDef>.GetNamedSilentFail(
                pawn.gender == Gender.Female ? "FemaleElderly" : "MaleElderly");
            if (bt != null && pawn.story?.bodyType != null)
                pawn.story.bodyType = bt;
        }

        /// <summary>
        /// HIGH-007 fix: Manually applies elderhood skill bonuses for pawns whose
        /// elderhood was assigned in the GeneratePawn postfix (after FinalLevelOfSkill
        /// already fired). Without this, forceNoBackstory pawns (raiders, quest
        /// pawns) would get elderhood traits + body type but NO skill bonuses —
        /// silent gameplay corruption.
        /// </summary>
        private static void ApplyElderhoodSkillBonuses(Pawn pawn, BackstoryDef eb)
        {
            if (eb?.skillGains == null || pawn.skills == null) return;
            for (int i = 0; i < eb.skillGains.Count; i++)
            {
                SkillGain sg = eb.skillGains[i];
                if (sg.skill == null) continue;
                SkillRecord skill = pawn.skills.GetSkill(sg.skill);
                if (skill != null)
                {
                    // Directly adjust Level (final, permanent — not subject to
                    // further FinalLevelOfSkill calls in this pawn's lifetime).
                    skill.Level = Mathf.Clamp(skill.Level + sg.amount, 0, 20);
                }
            }
        }
    }

    /// <summary>
    /// Character card UI: display elderhood info.
    /// Uses a Transpiler to inject IL into DoLeftSection, matching the approach
    /// of the original ElderhoodBackstory mod. This positions the elderhood
    /// section right after the adulthood section (inline with childhood/adulthood),
    /// using the method's internal 'currentY' variable for proper Y positioning.
    /// 
    /// RimWorld 1.6's DoLeftSection is:
    ///   static void DoLeftSection(Rect rect, Rect leftRect, Pawn pawn)
    /// The 'currentY' is a field on a compiler-generated display class (nested
    /// local class), accessed via a local variable in the method.
    /// 
    /// Fallback: If the Transpiler can't find the currentY field, a Postfix
    /// draws the section at a fixed position near the bottom of the card.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCardUtility), "DoLeftSection")]
    public static class CharacterCardUtility_DoLeftSection_Patch
    {
        private static Texture2D _infoIcon;
        private static Texture2D _editIcon;
        private static Texture2D _clearIcon;
        private static bool _iconsLoaded;
        private static bool _transpilerInjected;

        /// <summary>
        /// Transpiler: injects DrawElderhoodSection call before the last 'ret'
        /// in DoLeftSection, passing the display class's currentY field by ref.
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Step 1: Find the FieldInfo for 'currentY' by searching the display class types.
            // In RimWorld 1.6, DoLeftSection uses compiler-generated display classes like
            // <>c__DisplayClass43_2 which has a float field named 'currentY'.
            FieldInfo curYField = null;
            LocalBuilder curYLocal = null;

            // Search instructions for any Ldfld/Stfld that accesses a field named 'currentY'
            foreach (var code in codes)
            {
                if (code.operand is FieldInfo fi && fi.Name == "currentY" && fi.FieldType == typeof(float))
                {
                    curYField = fi;
                    break;
                }
            }

            if (curYField == null)
            {
                Log.Warning("[UB] Transpiler: could not find 'currentY' field in DoLeftSection — using Postfix fallback");
                return codes;
            }

            // Step 2: Find the LocalBuilder for the display class that contains currentY.
            // The display class is a local variable in the method. We find it by looking
            // for any Ldloc/Stloc instruction whose LocalType has a field named 'currentY'.
            foreach (var code in codes)
            {
                if (code.operand is LocalBuilder lb && lb.LocalType != null)
                {
                    var f = lb.LocalType.GetField("currentY", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType == typeof(float))
                    {
                        curYLocal = lb;
                        break;
                    }
                }
            }

            if (curYLocal == null)
            {
                // Fallback: try finding via ldloca
                foreach (var code in codes)
                {
                    if (code.operand is LocalBuilder lb && lb.LocalType != null)
                    {
                        // Check if this local's type has ANY float field
                        var fields = lb.LocalType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var f in fields)
                        {
                            if (f.Name == "currentY" && f.FieldType == typeof(float))
                            {
                                curYLocal = lb;
                                break;
                            }
                        }
                        if (curYLocal != null) break;
                    }
                }
            }

            if (curYLocal == null)
            {
                Log.Warning("[UB] Transpiler: could not find display class local — using Postfix fallback");
                return codes;
            }

            // Step 3: Find the last 'ret' instruction
            int retIndex = -1;
            for (int i = codes.Count - 1; i >= 0; i--)
            {
                if (codes[i].opcode == OpCodes.Ret)
                {
                    retIndex = i;
                    break;
                }
            }

            if (retIndex == -1)
            {
                Log.Warning("[UB] Transpiler: no 'ret' found in DoLeftSection — using Postfix fallback");
                return codes;
            }

            // Step 4: Inject before the last 'ret':
            //   ldloc curYLocal       (load the display class object)
            //   ldflda curYField      (load address of currentY field)
            //   ldarg.1               (leftRect — parameter index 1)
            //   ldarg.2               (pawn — parameter index 2)
            //   call DrawElderhoodSection(ref float, Rect, Pawn)
            var drawMethod = AccessTools.Method(typeof(CharacterCardUtility_DoLeftSection_Patch),
                nameof(DrawElderhoodSection));

            var injected = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloc, curYLocal),
                new CodeInstruction(OpCodes.Ldflda, curYField),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, drawMethod),
            };

            codes.InsertRange(retIndex, injected);
            _transpilerInjected = true;
            Log.Message("[UB] Transpiler: successfully injected elderhood drawing into DoLeftSection");
            return codes;
        }

        /// <summary>
        /// Fallback Postfix: runs only if the Transpiler failed to inject.
        /// Draws elderhood at a fixed position near the bottom of the card.
        /// </summary>
        public static void Postfix(Rect rect, Rect leftRect, Pawn pawn)
        {
            if (_transpilerInjected) return; // Transpiler handles it

            // Fallback: draw at bottom of card
            float fallbackY = rect.yMax - 120f;
            DrawElderhoodSection(ref fallbackY, leftRect, pawn, rect);
        }

        /// <summary>
        /// Draws the elderhood section at the current Y position in the left column.
        /// Called from injected IL (Transpiler) with currentY passed by ref.
        /// Modifying currentY extends the card height to fit the new section.
        /// </summary>
        public static void DrawElderhoodSection(ref float currentY, Rect leftRect, Pawn pawn)
        {
            DrawElderhoodSection(ref currentY, leftRect, pawn, leftRect);
        }

        /// <summary>
        /// Core drawing method — matches base game DrawBackstorySection layout EXACTLY.
        /// Order: header → title + buttons (info/edit/clear) → titleShort → description → skill gains.
        /// Spacing, fonts, and indentation mirror RimWorld 1.6's CharacterCardUtility.DrawBackstorySection.
        /// Edit/Clear buttons are inline with the title (not in header area) so the section
        /// looks identical to childhood/adulthood sections at first glance.
        /// </summary>
        private static void DrawElderhoodSection(ref float curY, Rect leftRect, Pawn pawn, Rect rectForFallback)
        {
            if (pawn == null || pawn.story == null) return;
            if (!pawn.RaceProps.Humanlike) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;

            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null)
                comp = ElderhoodHelper.GetOrCreateElderhoodComp(pawn);
            if (comp == null) return;

            bool hasElderhood = comp.HasElderhood;
            int elderAgeThreshold = comp.ElderhoodAge > 0 ? comp.ElderhoodAge : 60;
            if (!hasElderhood && (pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYears < elderAgeThreshold))
                return;

            // Lazy-load icons — log a warning when all fallback paths fail
            if (!_iconsLoaded)
            {
                _iconsLoaded = true;
                _infoIcon = ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", false)
                          ?? ContentFinder<Texture2D>.Get("UI/InfoButton", false);
                if (_infoIcon == null)
                    Log.Warning("[UB] Could not load InfoButton texture — info icon will be invisible");

                // RimWorld 1.6 has no standard Edit/Delete textures, so we reuse InfoButton
                // as fallback (visible but functionally distinct via tooltip).
                _editIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Edit", false)
                          ?? ContentFinder<Texture2D>.Get("UI/Icons/Edit", false);
                _clearIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Delete", false)
                           ?? ContentFinder<Texture2D>.Get("UI/Icons/Delete", false);

                // If no custom edit/clear textures exist, create simple colored buttons
                // using a 1x1 pixel texture with color tinting.
                if (_editIcon == null || _clearIcon == null)
                {
                    // Create a simple white pixel texture for tinted buttons
                    Texture2D whiteTex = new Texture2D(1, 1);
                    whiteTex.SetPixel(0, 0, Color.white);
                    whiteTex.Apply();
                    if (_editIcon == null) _editIcon = whiteTex;
                    if (_clearIcon == null) _clearIcon = whiteTex;
                }
            }

            float width = leftRect.width;
            float x = 0f; // Inside GUI group, x starts at 0

            // === Category label (matches base game exactly: Medium font, 28f height) ===
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(x, curY, width, 28f), "UB.ElderhoodHeader".Translate());
            curY += 28f;

            if (!hasElderhood)
            {
                // No elderhood — matches base game "None" style for empty backstory slot
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(x, curY, width, 22f), "UB.NoElderhood".Translate());
                curY += 22f;

                // Clickable hint to add elderhood (small inline link, like RimWorld's
                // clickable "None" labels in other character card sections)
                Text.Font = GameFont.Tiny;
                GUI.color = Color.grey;
                Rect addHintRect = new Rect(x, curY, Text.CalcSize("UB.ClickToAddElderhood".Translate()).x, 18f);
                Widgets.Label(addHintRect, "UB.ClickToAddElderhood".Translate());
                if (Widgets.ButtonInvisible(addHintRect))
                {
                    Find.WindowStack.Add(new Dialog_ChooseElderhood(pawn, comp));
                }
                GUI.color = Color.white;
                curY += 18f;
                curY += 4f;
                return;
            }

            BackstoryDef elderhood = comp.ElderhoodBS;
            if (elderhood == null) return;

            // === Title + buttons (info/edit/clear) on one line ===
            // Matches base game: title at Small font, info button next to it.
            // Edit/clear are additional buttons only visible on elderhood (since elderhood
            // is changeable, unlike childhood/adulthood which are permanent).
            Text.Font = GameFont.Small;
            string title = ElderhoodHelper.TitleFor(elderhood, pawn);
            Vector2 titleSize = Text.CalcSize(title);

            // Reserve width for buttons (info + edit + clear = 3 × 22f + gaps)
            float buttonReserve = (3f * 22f) + (2f * 4f);
            float titleW = Math.Min(titleSize.x, width - buttonReserve - 6f);
            Widgets.Label(new Rect(x, curY, titleW, 24f), title);

            float buttonX = x + titleW + 4f;

            // Info button (standard RimWorld info icon)
            if (_infoIcon != null)
            {
                Rect infoRect = new Rect(buttonX, curY + 1f, 22f, 22f);
                if (Widgets.ButtonImage(infoRect, _infoIcon))
                    Find.WindowStack.Add(new Dialog_InfoCard(elderhood));
                TooltipHandler.TipRegion(infoRect, () => ElderhoodHelper.TitleCapFor(elderhood, pawn), 63321);
                buttonX += 22f + 4f;
            }

            // Change (edit) button — looks like info button but has different tooltip
            Texture2D editTex = _editIcon;
            Rect editRect = new Rect(buttonX, curY + 1f, 22f, 22f);

            // Use colored tint for visual distinction between info/edit/clear
            Color saved = GUI.color;
            GUI.color = hasElderhood ? new Color(0.7f, 0.85f, 1f, 1f) : Color.white; // Light blue tint for edit
            if (Widgets.ButtonImage(editRect, editTex))
            {
                Find.WindowStack.Add(new Dialog_ChooseElderhood(pawn, comp));
            }
            GUI.color = saved;
            TooltipHandler.TipRegion(editRect, "UB.ChangeElderhood.Tip".Translate());
            buttonX += 22f + 4f;

            // Clear button — red tint for danger awareness
            Texture2D clearTex = _clearIcon;
            Rect clearRect = new Rect(buttonX, curY + 1f, 22f, 22f);
            saved = GUI.color;
            GUI.color = hasElderhood ? new Color(1f, 0.6f, 0.6f, 1f) : Color.white; // Light red tint for clear
            if (Widgets.ButtonImage(clearRect, clearTex))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("UB.ClearElderhood".Translate(), delegate
                    {
                        ElderhoodHelper.ChangeElderhood(pawn, comp, null);
                    }),
                    new FloatMenuOption("UB.Cancel".Translate(), null),
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            GUI.color = saved;
            TooltipHandler.TipRegion(clearRect, "UB.ClearElderhood.Tip".Translate());

            curY += 24f;

            // === TitleShort in parentheses (matches base game exactly) ===
            string shortTitle = ElderhoodHelper.TitleShortFor(elderhood, pawn);
            if (!shortTitle.NullOrEmpty() && shortTitle != title)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(x, curY, width, 18f), "(" + shortTitle + ")");
                curY += 17f;
                Text.Font = GameFont.Small;
            }

            // === Full description (NO height cap — matches base game exactly) ===
            // Base game uses Text.CalcHeight with NO Math.Min cap. Descrição longa
            // ocupa toda a altura necessária com word wrap.
            string desc = elderhood.FullDescriptionFor(pawn);
            if (!desc.NullOrEmpty())
            {
                float descH = Text.CalcHeight(desc, width);
                Widgets.Label(new Rect(x, curY, width, descH), desc);
                curY += descH + 4f; // Base game spacing after description
            }

            // === Skill gains inline (matches base game layout) ===
            // Base game draws skill gains inline: "SkillA +3  SkillB -1  SkillC +2"
            // WITHOUT wrapping — just advances X horizontally.
            if (elderhood.skillGains != null)
            {
                float sx = x + 6f; // Slight indent, matching base game
                for (int i = 0; i < elderhood.skillGains.Count; i++)
                {
                    SkillGain sg = elderhood.skillGains[i];
                    if (sg.skill == null) continue;
                    string txt = sg.skill.LabelCap + " " + (sg.amount >= 0 ? "+" : "") + sg.amount;
                    Vector2 sz = Text.CalcSize(txt);
                    Widgets.Label(new Rect(sx, curY, sz.x, 24f), txt);
                    sx += sz.x + 8f;
                }
                curY += 24f;
            }

            curY += 4f; // Final padding (matches base game)
        }
    }

    // ================================================================
    // ELDERHOOD SELECTION DIALOG
    // ================================================================

    /// <summary>
    /// Selection window listing all valid elderhood backstories.
    /// Opens from the character card Edit/Add button.
    /// Compatible with Character Editor — uses the same comp data that CE reads.
    /// </summary>
    public class Dialog_ChooseElderhood : Window
    {
        private Pawn pawn;
        private CompElderhoodBackstory comp;
        private Vector2 scrollPos;
        private List<BackstoryDef> elderhoods;
        private const float RowHeight = 50f;

        public override Vector2 InitialSize => new Vector2(520f, 580f);

        public Dialog_ChooseElderhood(Pawn pawn, CompElderhoodBackstory comp)
        {
            this.pawn = pawn;
            this.comp = comp;
            this.elderhoods = ElderhoodHelper.ListElderhoods() ?? new List<BackstoryDef>();
            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            // absorbInputAroundWindow = false (default) so closeOnClickedOutside works
            this.forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "UB.ChooseElderhood".Translate());

            Text.Font = GameFont.Small;
            Rect listRect = new Rect(0f, 35f, inRect.width, inRect.height - 35f - 40f);
            float contentHeight = elderhoods.Count * RowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(listRect, ref scrollPos, viewRect);

            for (int i = 0; i < elderhoods.Count; i++)
            {
                BackstoryDef bs = elderhoods[i];
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);

                bool isSelected = comp.HasElderhood && comp.ElderhoodBS == bs;

                if (isSelected)
                    Widgets.DrawHighlightSelected(row);

                if (Widgets.ButtonInvisible(row))
                {
                    ElderhoodHelper.ChangeElderhood(pawn, comp, bs);
                    Find.WindowStack.TryRemove(this);
                }

                // Title
                Text.Font = GameFont.Small;
                string title = ElderhoodHelper.TitleFor(bs, pawn);
                Widgets.Label(new Rect(5f, row.y + 2f, viewRect.width - 10f, 22f), title);

                // Description (1 line truncated)
                Text.Font = GameFont.Tiny;
                string desc = bs.FullDescriptionFor(pawn);
                if (desc.Length > 120)
                    desc = desc.Substring(0, 117) + "...";
                Widgets.Label(new Rect(5f, row.y + 22f, viewRect.width - 10f, 20f), desc);

                // Skill tags on the right
                if (bs.skillGains != null)
                {
                    float tagX = viewRect.width - 10f;
                    Text.Font = GameFont.Tiny;
                    for (int j = bs.skillGains.Count - 1; j >= 0; j--)
                    {
                        SkillGain sg = bs.skillGains[j];
                        if (sg.skill == null) continue;
                        string tag = sg.skill.LabelCap + (sg.amount >= 0 ? "+" : "") + sg.amount;
                        Vector2 tagSize = Text.CalcSize(tag);
                        tagX -= tagSize.x + 4f;
                        Rect tagRect = new Rect(tagX, row.y + 1f, tagSize.x + 4f, 18f);
                        Widgets.DrawHighlight(tagRect);
                        Widgets.Label(new Rect(tagX + 2f, row.y + 1f, tagSize.x, 18f), tag);
                    }
                }

                // Divider
                if (i < elderhoods.Count - 1)
                    Widgets.DrawLineHorizontal(5f, row.yMax, viewRect.width - 10f);
            }

            Widgets.EndScrollView();
        }
    }

    /// <summary>
    /// Elderly body type override.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GetBodyTypeFor")]
    public static class PawnGenerator_GetBodyTypeFor_Patch
    {
        public static void Postfix(Pawn pawn, ref BodyTypeDef __result)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;
            BodyTypeDef bt = DefDatabase<BodyTypeDef>.GetNamedSilentFail(
                pawn.gender == Gender.Female ? "FemaleElderly" : "MaleElderly");
            if (bt != null) __result = bt;
        }
    }

    /// <summary>
    /// Skill level bonus from elderhood.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "FinalLevelOfSkill")]
    public static class PawnGenerator_FinalLevelOfSkill_Patch
    {
        public static void Postfix(Pawn pawn, SkillDef sk, ref int __result)
        {
            if (pawn?.story == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;
            BackstoryDef eb = comp.ElderhoodBS;
            if (eb?.skillGains == null) return;
            for (int i = 0; i < eb.skillGains.Count; i++)
            {
                if (eb.skillGains[i].skill == sk)
                    __result += eb.skillGains[i].amount;
            }
        }
    }

    /// <summary>
    /// Work disables from elderhood.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_StoryTracker), "get_DisabledWorkTagsBackstoryAndTraits")]
    public static class Pawn_StoryTracker_DisabledWorkTags_Patch
    {
        public static void Postfix(Pawn_StoryTracker __instance, ref WorkTags __result)
        {
            Pawn pawn = ElderhoodHelper.GetPawnFromTracker(__instance);
            if (pawn == null) return;

            // Elderhood disabling work tags
            if (UB_Mod.Settings == null || UB_Mod.Settings.elderhoodEnabled)
            {
                CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
                if (comp != null && comp.HasElderhood && comp.ElderhoodBS != null)
                {
                    __result |= comp.ElderhoodBS.workDisables;
                }
            }

            // ZCB disablingWorkTags (applied per-pawn, NOT via def mutation)
            // HIGH-004 fix: cache parsed WorkTags on the ZCBackstoryDef instance
            // to avoid Enum.TryParse on every getter call (hot path).
            if (UB_Mod.Settings == null || UB_Mod.Settings.zcbEnabled)
            {
                if (pawn.story?.Childhood is ZCBackstoryDef zcb && zcb.disablingWorkTags != null)
                {
                    __result |= zcb.GetCachedDisabledWorkTags();
                }
            }
        }
    }



    /// <summary>
    /// Suppress old ZCB/ElderhoodBackstory settings screens.
    /// </summary>
    [HarmonyPatch(typeof(Mod), "SettingsCategory")]
    public static class Mod_SettingsCategory_Suppress_Patch
    {
        private static readonly HashSet<string> SuppressedAssemblies = new HashSet<string>
        {
            "ZCB",
            "ElderhoodBackstory",
        };

        public static bool Prefix(Mod __instance, ref string __result)
        {
            if (__instance is UB_Mod) return true;
            string name = __instance.GetType().Assembly.GetName().Name;
            if (SuppressedAssemblies.Contains(name))
            {
                __result = string.Empty;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// SINGLE patch-all entry point. There MUST be only one class with
    /// [StaticConstructorOnStartup] that calls PatchAll() in the assembly.
    /// Having two such classes caused every [HarmonyPatch] to register
    /// twice — resulting in doubled skill bonuses, incorrect need-decay
    /// restoration (43.75% instead of 75%), duplicated CharacterCard UI
    /// elements, and potential item duplication.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UnifiedBackstoriesPatcher
    {
        static UnifiedBackstoriesPatcher()
        {
            new Harmony("UnifiedBackstories.UnifiedBackstories").PatchAll();
            string ver = System.IO.File.GetLastWriteTime(
                System.Reflection.Assembly.GetExecutingAssembly().Location)
                .ToString("yyyy-MM-dd HH:mm");
            Log.Message("[UB] v1.8.0 loaded (build " + ver
                + ") — Elderhood + Gender tokens + Age 60+ + UI edit"
                + " + Mood rebalance + Need grace period + ZCB validator"
                + " + HardshipBonding + BackstoryPairing + TraitAlignment");
        }
    }
}
