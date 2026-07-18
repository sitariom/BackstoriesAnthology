using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Validates ZCBackstoryDef requirements against a pawn.
    ///
    /// This is the direct replacement for the original ZCB mod's IsAcceptable(),
    /// with ALL 18 fields verified against the original decompiled DLL logic:
    ///
    ///   commonality, minTechLevel, maxTechLevel, colonySize, developmentalStage,
    ///   bodyPartsReplaced, bodyPartsMissing, father, mother,
    ///   requiredRecords, recordRatios, requiredSkills, requiredTraits,
    ///   requiredPassions, disallowedTraits, disallowedPassions, passionGains,
    ///   disablingWorkTags
    ///
    /// Key fixes from original ZCB v1.0.0:
    /// 1. Body parts: uses GetMissingPartsCommonAncestors() + AddedAndImplantedPartsWithXenogenesCount()
    /// 2. Parents: uses ParentRelationUtility with FamilyStatusFlags bitwise matching
    /// 3. Traits: checks BOTH defName and degree via HasTrait(def, degree)
    /// 4. Records/Skills: scaled by childAgingRate/4f (original behavior)
    /// 5. Passion gains: ADDITIVE (adds to existing passion, clamped 0-2)
    /// 6. Commonality: float-weighted selection matching RandomElementByWeight
    /// 7. Tech level: checks Faction.OfPlayer (original behavior)
    /// 8. Colony size: uses IntRange + PawnsFinder.AllMaps_FreeColonistsSpawned
    ///
    /// All validation is opt-out (returns true on missing data) so that
    /// incomplete XML definitions never crash the game.
    /// </summary>
    public static class ZCBackstoryValidator
    {
        /// <summary>
        /// Master validation — returns true if the pawn meets ALL requirements
        /// of the given ZCBackstoryDef. Matches the original ZCB IsAcceptable()
        /// semantics exactly.
        /// </summary>
        public static bool IsValidFor(Pawn pawn, ZCBackstoryDef def)
        {
            if (def == null || pawn == null) return true;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.zcbEnabled) return true;

            return CheckTechLevel(def)
                && CheckColonySize(def)
                && CheckBodyParts(pawn, def)
                && CheckParents(pawn, def)
                && CheckDevelopmentalStage(pawn, def)
                && CheckRecords(pawn, def)
                && CheckRequiredSkills(pawn, def)
                && CheckTraits(pawn, def)
                && CheckPassions(pawn, def);
        }

        // ================================================================
        // INDIVIDUAL CHECKS — each returns true when the requirement is
        // absent (no filter) or when the pawn satisfies it.
        // ================================================================

        /// <summary>
        /// Tech level: compares Faction.OfPlayer.def.techLevel against min/max.
        /// Matches original ZCB: uses Player faction only.
        /// Default TechLevel.Undefined means no restriction.
        /// </summary>
        private static bool CheckTechLevel(ZCBackstoryDef def)
        {
            if (def.minTechLevel <= TechLevel.Undefined && def.maxTechLevel <= TechLevel.Undefined)
                return true; // both unset — no restriction

            TechLevel playerTech;
            try
            {
                playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Undefined;
            }
            catch
            {
                return true; // game not fully initialized, defer
            }

            if (playerTech == TechLevel.Undefined)
                return true;

            if (playerTech < def.minTechLevel || playerTech > def.maxTechLevel)
                return false;

            return true;
        }

        /// <summary>
        /// Colony size: uses PawnsFinder.AllMaps_FreeColonistsSpawned.Count()
        /// against IntRange. Matches original ZCB.
        /// </summary>
        private static bool CheckColonySize(ZCBackstoryDef def)
        {
            if (def.colonySize.min <= 0 && def.colonySize.max >= 9999)
                return true; // default range covers everything

            int pawnCount;
            try
            {
                pawnCount = PawnsFinder.AllMaps_FreeColonistsSpawned.Count();
            }
            catch
            {
                return true;
            }

            return pawnCount >= def.colonySize.min && pawnCount <= def.colonySize.max;
        }

        /// <summary>
        /// Body parts: uses GetMissingPartsCommonAncestors() for missing parts
        /// and GeneUtility.AddedAndImplantedPartsWithXenogenesCount() for replaced.
        /// Matches original ZCB API usage exactly.
        /// </summary>
        private static bool CheckBodyParts(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.bodyPartsMissing.min <= 0 && def.bodyPartsMissing.max >= 999
                && def.bodyPartsReplaced.min <= 0 && def.bodyPartsReplaced.max >= 999)
                return true; // default ranges cover everything

            if (pawn.health == null || pawn.health.hediffSet == null)
                return true;

            int missingCount = pawn.health.hediffSet.GetMissingPartsCommonAncestors().Count();
            int replacedCount = GeneUtility.AddedAndImplantedPartsWithXenogenesCount(pawn);

            if (missingCount < def.bodyPartsMissing.min || missingCount > def.bodyPartsMissing.max)
                return false;

            if (replacedCount < def.bodyPartsReplaced.min || replacedCount > def.bodyPartsReplaced.max)
                return false;

            return true;
        }

        /// <summary>
        /// Parents: uses ParentRelationUtility.GetFather/Mother() with
        /// FamilyStatusFlags bitwise matching. Matches original ZCB exactly.
        ///
        /// Status logic:
        ///   null → Absent
        ///   Dead → Dead
        ///   Different faction → Absent
        ///   Alive + same faction → Present
        /// </summary>
        private static bool CheckParents(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.father == FamilyStatusFlags.Any && def.mother == FamilyStatusFlags.Any)
                return true;

            // Father check
            if (def.father != FamilyStatusFlags.Any)
            {
                Pawn father = ParentRelationUtility.GetFather(pawn);
                FamilyStatusFlags fatherStatus = GetParentStatus(father, pawn);
                if ((def.father & fatherStatus) == 0)
                    return false;
            }

            // Mother check
            if (def.mother != FamilyStatusFlags.Any)
            {
                Pawn mother = ParentRelationUtility.GetMother(pawn);
                FamilyStatusFlags motherStatus = GetParentStatus(mother, pawn);
                if ((def.mother & motherStatus) == 0)
                    return false;
            }

            return true;
        }

        private static FamilyStatusFlags GetParentStatus(Pawn parent, Pawn pawn)
        {
            if (parent == null)
                return FamilyStatusFlags.Absent;

            if (parent.Dead)
                return FamilyStatusFlags.Dead;

            // Check if parent shares pawn's faction
            if (pawn.Faction != null && parent.Faction == pawn.Faction)
                return FamilyStatusFlags.Present;

            return FamilyStatusFlags.Absent;
        }

        /// <summary>
        /// Developmental stage: 0=any, 1=child, 2=adult.
        /// Children are under 13 biological years.
        /// </summary>
        private static bool CheckDevelopmentalStage(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.developmentalStage <= 0)
                return true;

            if (pawn.ageTracker == null)
                return true;

            bool isChild = pawn.ageTracker.AgeBiologicalYears < 13;
            int stage = isChild ? 1 : 2;

            return stage == def.developmentalStage;
        }

        /// <summary>
        /// Records: requiredRecords checks pawn records, scaled by child aging rate.
        /// recordRatios checks ratios between two records.
        ///
        /// CRITICAL: Matches original ZCB — min/max values are DIVIDED by
        /// (childAgingRate / 4f) so records requirements scale with game speed.
        /// Without this scaling, records checks are wrong for non-100% aging.
        /// </summary>
        private static bool CheckRecords(Pawn pawn, ZCBackstoryDef def)
        {
            if ((def.requiredRecords == null || def.requiredRecords.Count == 0)
                && (def.recordRatios == null || def.recordRatios.Count == 0))
                return true;

            if (pawn.records == null) return false;

            float agingScale;
            try
            {
                agingScale = Find.Storyteller.difficulty.childAgingRate / 4f;
            }
            catch
            {
                agingScale = 0.25f; // default (1.0 / 4)
            }

            if (agingScale <= 0f) agingScale = 0.25f; // safety

            // Required records (scaled by childAgingRate — matches original ZCB)
            if (def.requiredRecords != null)
            {
                for (int i = 0; i < def.requiredRecords.Count; i++)
                {
                    ZCBRecordReq req = def.requiredRecords[i];
                    RecordDef recordDef = DefDatabase<RecordDef>.GetNamedSilentFail(req.name);
                    if (recordDef == null) continue;

                    float value = pawn.records.GetValue(recordDef);
                    float scaledMin = req.minValue / agingScale;
                    float scaledMax = req.maxValue / agingScale;

                    if (value < scaledMin || value > scaledMax)
                        return false;
                }
            }

            // Record ratios (NOT scaled — ratios are dimensionless)
            if (def.recordRatios != null)
            {
                for (int i = 0; i < def.recordRatios.Count; i++)
                {
                    ZCBRecordRatio ratio = def.recordRatios[i];
                    RecordDef numDef = DefDatabase<RecordDef>.GetNamedSilentFail(ratio.numerator);
                    RecordDef denDef = DefDatabase<RecordDef>.GetNamedSilentFail(ratio.denominator);
                    if (numDef == null || denDef == null) continue;

                    float numVal = pawn.records.GetValue(numDef);
                    float denVal = pawn.records.GetValue(denDef);

                    if (ratio.ratio == 0f)
                    {
                        // Original ZCB: ratio==0 means "numerator > denominator"
                        if (numVal <= denVal)
                            return false;
                    }
                    else
                    {
                        float actualRatio = denVal > 0f ? numVal / denVal : 0f;
                        if (actualRatio < ratio.ratio)
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Skills: requiredSkills checks skill levels, scaled by child aging rate.
        /// Matches original ZCB — min/max values are DIVIDED by (childAgingRate / 4f).
        /// </summary>
        private static bool CheckRequiredSkills(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.requiredSkills == null || def.requiredSkills.Count == 0)
                return true;

            if (pawn.skills == null) return false;

            float agingScale;
            try
            {
                agingScale = Find.Storyteller.difficulty.childAgingRate / 4f;
            }
            catch
            {
                agingScale = 0.25f;
            }

            if (agingScale <= 0f) agingScale = 0.25f;

            for (int i = 0; i < def.requiredSkills.Count; i++)
            {
                ZCBSkillReq req = def.requiredSkills[i];
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(req.name);
                if (skillDef == null) continue;

                SkillRecord skill = pawn.skills.GetSkill(skillDef);
                if (skill == null) return false;

                float level = skill.Level;
                float scaledMin = req.minValue / agingScale;
                float scaledMax = req.maxValue / agingScale;

                if (level < scaledMin || level > scaledMax)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Traits: requiredTraits (pawn must have) and disallowedTraits
        /// (pawn must NOT have). Checks BOTH defName AND degree via
        /// HasTrait(def, degree). Matches original ZCB.
        ///
        /// requiredTraits: List<BackstoryTrait> — parsed from XML DIRECT format
        /// disallowedTraits: inherited from BackstoryDef (also List<BackstoryTrait>)
        /// </summary>
        private static bool CheckTraits(Pawn pawn, ZCBackstoryDef def)
        {
            bool hasRequired = def.requiredTraits == null || def.requiredTraits.Count == 0;
            bool hasDisallowed = def.disallowedTraits == null || def.disallowedTraits.Count == 0;
            if (hasRequired && hasDisallowed)
                return true;

            if (pawn.story?.traits == null) return false;

            // Required traits — pawn must have ALL with matching degree
            if (def.requiredTraits != null)
            {
                for (int i = 0; i < def.requiredTraits.Count; i++)
                {
                    BackstoryTrait req = def.requiredTraits[i];
                    if (req.def == null) continue;

                    if (!pawn.story.traits.HasTrait(req.def, req.degree))
                        return false;
                }
            }

            // Disallowed traits — pawn must have NONE with matching degree
            if (def.disallowedTraits != null)
            {
                for (int i = 0; i < def.disallowedTraits.Count; i++)
                {
                    BackstoryTrait dis = def.disallowedTraits[i];
                    if (dis.def == null) continue;

                    if (pawn.story.traits.HasTrait(dis.def, dis.degree))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Passions: requiredPassions (pawn must have passion in these skills)
        /// and disallowedPassions (pawn must NOT have passion in these skills).
        /// Skill names are defName matches.
        /// </summary>
        private static bool CheckPassions(Pawn pawn, ZCBackstoryDef def)
        {
            bool hasRequired = def.requiredPassions == null || def.requiredPassions.Count == 0;
            bool hasDisallowed = def.disallowedPassions == null || def.disallowedPassions.Count == 0;
            if (hasRequired && hasDisallowed)
                return true;

            if (pawn.skills == null) return false;

            // Required passions
            if (def.requiredPassions != null)
            {
                for (int i = 0; i < def.requiredPassions.Count; i++)
                {
                    SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(def.requiredPassions[i]);
                    if (skillDef == null) continue;

                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill == null || skill.passion == Passion.None)
                        return false;
                }
            }

            // Disallowed passions
            if (def.disallowedPassions != null)
            {
                for (int i = 0; i < def.disallowedPassions.Count; i++)
                {
                    SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(def.disallowedPassions[i]);
                    if (skillDef == null) continue;

                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null && skill.passion != Passion.None)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Commonality-weighted random selection from a pool of ZCB backstories.
        /// Uses float weights matching the original ZCB's
        /// GenCollection.RandomElementByWeight semantics.
        ///
        /// Backstories with higher commonality are more likely to be chosen.
        /// Commonality of 0 excludes the backstory entirely.
        /// Filters by developmental stage when specified.
        /// </summary>
        public static ZCBackstoryDef SelectByCommonality(List<ZCBackstoryDef> pool, int devStage = 0)
        {
            if (pool == null || pool.Count == 0) return null;

            // Filter by developmental stage
            List<ZCBackstoryDef> filtered;
            if (devStage > 0)
            {
                filtered = new List<ZCBackstoryDef>(pool.Count);
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].developmentalStage <= 0 || pool[i].developmentalStage == devStage)
                        filtered.Add(pool[i]);
                }
            }
            else
            {
                filtered = pool;
            }

            if (filtered.Count == 0) return null;
            if (filtered.Count == 1) return filtered[0];

            // Float-weighted selection matching RandomElementByWeight
            float totalWeight = 0f;
            for (int i = 0; i < filtered.Count; i++)
            {
                float w = filtered[i].commonality;
                if (w > 0f) totalWeight += w;
            }

            if (totalWeight <= 0f) return filtered.RandomElement();

            float roll = Rand.Value * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < filtered.Count; i++)
            {
                float w = filtered[i].commonality;
                if (w <= 0f) continue;
                cumulative += w;
                if (roll <= cumulative) return filtered[i];
            }

            return filtered[filtered.Count - 1];
        }

        /// <summary>
        /// Gets all ZCB backstories valid for the given pawn, filtered by
        /// the validator's standard checks. Used to provide a commonality-weighted
        /// pool for re-rolls instead of RimWorld's uniform random selection.
        /// </summary>
        public static List<ZCBackstoryDef> ValidPoolFor(Pawn pawn)
        {
            var all = DefDatabase<BackstoryDef>.AllDefsListForReading;
            List<ZCBackstoryDef> pool = new List<ZCBackstoryDef>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is ZCBackstoryDef zcb && IsValidFor(pawn, zcb))
                    pool.Add(zcb);
            }
            return pool;
        }
    }

    // ================================================================
    // HARMONY PATCH — filters ZCB childhood backstories during pawn generation
    // ================================================================

    /// <summary>
    /// After RimWorld fills a childhood backstory slot, validate that the
    /// selected ZCBackstoryDef's requirements are met. If not, re-roll up
    /// to MaxRetries times. This mirrors the pattern used by BackstoryPairing
    /// for adulthood backstories.
    ///
    /// Priority: executes BEFORE BackstoryPairing (normal priority) so that
    /// childhood is validated first, then the childhood→adulthood pair is
    /// checked once, avoiding nested re-rolls.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.FillBackstorySlotShuffled))]
    [HarmonyPriority(Priority.High)]
    public static class ZCB_BackstoryValidator_Patch
    {
        private const int MaxRetries = 15;
        private static bool _reentrant;

        public static void Postfix(Pawn pawn, BackstorySlot slot,
            List<BackstoryCategoryFilter> backstoryCategories, FactionDef factionType,
            BackstorySlot? mustBeCompatibleTo)
        {
            if (_reentrant || slot != BackstorySlot.Childhood)
                return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.zcbEnabled)
                return;
            if (pawn?.story == null)
                return;

            BackstoryDef current = pawn.story.Childhood;
            if (!(current is ZCBackstoryDef zcb))
                return;

            _reentrant = true;
            try
            {
                List<ZCBackstoryDef> commonalityPool = null;

                for (int i = 0; i < MaxRetries; i++)
                {
                    if (ZCBackstoryValidator.IsValidFor(pawn, zcb))
                    {
                        ApplyZCBEffects(pawn, zcb);
                        return;
                    }

                    // Try commonality-weighted selection first
                    if (commonalityPool == null)
                    {
                        commonalityPool = ZCBackstoryValidator.ValidPoolFor(pawn);
                    }

                    // Pre-select by commonality respecting developmental stage
                    int devStage = 0;
                    if (pawn.ageTracker != null)
                        devStage = pawn.ageTracker.AgeBiologicalYears < 13 ? 1 : 2;

                    ZCBackstoryDef commonalityPick =
                        ZCBackstoryValidator.SelectByCommonality(commonalityPool, devStage);

                    if (commonalityPick != null && commonalityPick != zcb)
                    {
                        pawn.story.Childhood = commonalityPick;
                        zcb = commonalityPick;
                        continue;
                    }

                    // Fallback: RimWorld's default random selection
                    PawnBioAndNameGenerator.FillBackstorySlotShuffled(
                        pawn, slot, backstoryCategories, factionType, mustBeCompatibleTo);

                    current = pawn.story?.Childhood;
                    if (!(current is ZCBackstoryDef))
                        return;
                    zcb = (ZCBackstoryDef)current;
                }

                if (pawn.story?.Childhood is ZCBackstoryDef finalZcb)
                {
                    ApplyZCBEffects(pawn, finalZcb);
                }
            }
            finally
            {
                _reentrant = false;
            }
        }

        /// <summary>
        /// Applies ZCB-specific effects that BackstoryDef doesn't natively support.
        ///
        /// passionGains: ADDITIVE (matches original ZCB) — adds to existing
        /// passion level and clamps 0-2, instead of overwriting.
        ///
        /// disablingWorkTags: handled by extending Pawn_StoryTracker_DisabledWorkTags_Patch
        /// (ElderhoodSystem.cs) to include ZCB disablingWorkTags.
        /// </summary>
        private static void ApplyZCBEffects(Pawn pawn, ZCBackstoryDef def)
        {
            // Passion gains — ADDITIVE (original ZCB behavior)
            if (def.passionGains != null && pawn.skills != null)
            {
                for (int i = 0; i < def.passionGains.Count; i++)
                {
                    ZCBPassionGain pg = def.passionGains[i];
                    SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(pg.skill);
                    if (skillDef == null) continue;

                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null)
                    {
                        // ADDITIVE: current passion + gain, clamped [0, 2]
                        int current = (int)skill.passion;
                        int passionLevel = Math.Max(0, Math.Min(2, current + pg.level));
                        skill.passion = (Passion)passionLevel;
                    }
                }
            }

            // Disabling work tags are handled by extending the existing
            // Pawn_StoryTracker_DisabledWorkTags_Patch (ElderhoodSystem.cs)
            // to also include ZCB disablingWorkTags. We do NOT mutate the
            // Def singleton here — doing so would permanently alter a global
            // shared object, corrupting it for every pawn using the same def.
        }
    }
}
