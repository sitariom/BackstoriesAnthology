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
    /// These 24+ fields were declared in ZCBackstoryDef but never enforced.
    /// This class implements the full filtering system so ZCB childhood
    /// backstories only appear for pawns that meet their requirements.
    ///
    /// All validation is opt-out (returns true on missing data) so that
    /// incomplete XML definitions never crash the game.
    /// </summary>
    public static class ZCBackstoryValidator
    {
        /// <summary>
        /// Master validation — returns true if the pawn meets ALL requirements
        /// of the given ZCBackstoryDef.
        /// </summary>
        public static bool IsValidFor(Pawn pawn, ZCBackstoryDef def)
        {
            if (def == null || pawn == null) return true;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.zcbEnabled) return true;

            return CheckTechLevel(pawn, def)
                && CheckColonySize(def)
                && CheckBodyParts(pawn, def)
                && CheckParents(pawn, def)
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
        /// Tech level: minTechLevel / maxTechLevel from XML.
        /// Compared against the faction's or pawn's tech level.
        /// </summary>
        private static bool CheckTechLevel(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.minTechLevel.NullOrEmpty() && def.maxTechLevel.NullOrEmpty())
                return true;

            TechLevel pawnTech = TechLevel.Undefined;
            if (pawn.Faction != null)
                pawnTech = pawn.Faction.def.techLevel;
            else if (Faction.OfPlayer?.def != null)
                pawnTech = Faction.OfPlayer.def.techLevel;

            if (pawnTech == TechLevel.Undefined)
                return true; // can't determine — allow

            if (!def.minTechLevel.NullOrEmpty())
            {
                if (!Enum.TryParse(def.minTechLevel, true, out TechLevel minTech))
                    return true; // invalid XML — allow
                if (pawnTech < minTech)
                    return false;
            }

            if (!def.maxTechLevel.NullOrEmpty())
            {
                if (!Enum.TryParse(def.maxTechLevel, true, out TechLevel maxTech))
                    return true;
                if (pawnTech > maxTech)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Colony size: format "1~99" — min and max pawn count on the map.
        /// Counts free colonists across all loaded maps.
        /// </summary>
        private static bool CheckColonySize(ZCBackstoryDef def)
        {
            if (def.colonySize.NullOrEmpty())
                return true;

            var parts = def.colonySize.Split('~');
            if (parts.Length != 2) return true;

            if (!int.TryParse(parts[0].Trim(), out int minSize)
                || !int.TryParse(parts[1].Trim(), out int maxSize))
                return true;

            int pawnCount = 0;
            try
            {
                if (Find.Maps != null)
                {
                    for (int i = 0; i < Find.Maps.Count; i++)
                    {
                        pawnCount += Find.Maps[i].mapPawns.FreeColonistsSpawnedCount;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[UB] ZCBackstoryValidator.CheckColonySize: " + ex.Message);
            }

            return pawnCount >= minSize && pawnCount <= maxSize;
        }

        /// <summary>
        /// Body parts: bodyPartsReplaced / bodyPartsMissing — range "1~999".
        /// Checks the pawn's hediffs for replaced or missing body parts.
        /// </summary>
        private static bool CheckBodyParts(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.bodyPartsReplaced.NullOrEmpty() && def.bodyPartsMissing.NullOrEmpty())
                return true;

            if (pawn.health == null || pawn.health.hediffSet == null)
                return true; // no health data — can't enforce

            int replaced = 0, missing = 0;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_MissingPart) missing++;
                if (hediff is Hediff_AddedPart) replaced++;
            }

            if (!def.bodyPartsReplaced.NullOrEmpty())
            {
                var parts = def.bodyPartsReplaced.Split('~');
                if (parts.Length == 2
                    && int.TryParse(parts[0].Trim(), out int minRep)
                    && int.TryParse(parts[1].Trim(), out int maxRep)
                    && (replaced < minRep || replaced > maxRep))
                    return false;
            }

            if (!def.bodyPartsMissing.NullOrEmpty())
            {
                var parts = def.bodyPartsMissing.Split('~');
                if (parts.Length == 2
                    && int.TryParse(parts[0].Trim(), out int minMiss)
                    && int.TryParse(parts[1].Trim(), out int maxMiss)
                    && (missing < minMiss || missing > maxMiss))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Parents: father / mother strings like "Dead", "Present", "Absent, Dead".
        /// Checks parent status from the pawn's parent relations.
        /// </summary>
        private static bool CheckParents(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.father.NullOrEmpty() && def.mother.NullOrEmpty())
                return true;

            // Use DefDatabase lookup — safe regardless of DLC load order
            PawnRelationDef fatherDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail("Father");
            PawnRelationDef motherDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail("Mother");
            if (fatherDef == null && motherDef == null)
                return true; // parent system not available (no Biotech/base defs)

            var father = fatherDef != null ? GetParentPawn(pawn, fatherDef) : null;
            var mother = motherDef != null ? GetParentPawn(pawn, motherDef) : null;

            if (!def.father.NullOrEmpty() && !ParentMatches(father, def.father))
                return false;

            if (!def.mother.NullOrEmpty() && !ParentMatches(mother, def.mother))
                return false;

            return true;
        }

        private static Pawn GetParentPawn(Pawn pawn, PawnRelationDef relation)
        {
            if (pawn == null || pawn.relations == null) return null;
            List<DirectPawnRelation> relations = pawn.relations.DirectRelations;
            for (int i = 0; i < relations.Count; i++)
            {
                if (relations[i].def == relation)
                    return relations[i].otherPawn;
            }
            return null;
        }

        private static bool ParentMatches(Pawn parent, string requirement)
        {
            if (requirement.NullOrEmpty()) return true;

            var tokens = requirement.Split(',');
            foreach (string token in tokens)
            {
                string t = token.Trim().ToLowerInvariant();
                switch (t)
                {
                    case "dead":
                        if (parent == null || !parent.Dead) return false;
                        break;
                    case "present":
                        if (parent == null || parent.Dead || parent.Destroyed) return false;
                        break;
                    case "absent":
                        if (parent != null) return false;
                        break;
                    // "Absent, Dead" splits into ["Absent", "Dead"] — handled sequentially
                }
            }
            return true;
        }

        /// <summary>
        /// Records: requiredRecords checks pawn records (e.g. time spent feral).
        /// recordRatios checks ratios between two records.
        /// Handles the ZCB_* records defined in ZCB_Records.xml.
        /// </summary>
        private static bool CheckRecords(Pawn pawn, ZCBackstoryDef def)
        {
            if ((def.requiredRecords == null || def.requiredRecords.Count == 0)
                && (def.recordRatios == null || def.recordRatios.Count == 0))
                return true;

            if (pawn.records == null) return false;

            // Required records
            if (def.requiredRecords != null)
            {
                for (int i = 0; i < def.requiredRecords.Count; i++)
                {
                    ZCBRecordReq req = def.requiredRecords[i];
                    RecordDef recordDef = DefDatabase<RecordDef>.GetNamedSilentFail(req.name);
                    if (recordDef == null) continue;

                    float value = pawn.records.GetValue(recordDef);
                    if (value < req.minValue || value > req.maxValue)
                        return false;
                }
            }

            // Record ratios
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
                    float actualRatio = denVal > 0f ? numVal / denVal : 0f;
                    if (actualRatio < ratio.ratio)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Skills: requiredSkills sets min/max skill levels.
        /// The pawn must have each skill within the specified range.
        /// </summary>
        private static bool CheckRequiredSkills(Pawn pawn, ZCBackstoryDef def)
        {
            if (def.requiredSkills == null || def.requiredSkills.Count == 0)
                return true;

            if (pawn.skills == null) return false;

            for (int i = 0; i < def.requiredSkills.Count; i++)
            {
                ZCBSkillReq req = def.requiredSkills[i];
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(req.name);
                if (skillDef == null) continue;

                SkillRecord skill = pawn.skills.GetSkill(skillDef);
                if (skill == null) return false;

                int level = skill.Level;
                if (level < req.minValue || level > req.maxValue)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Traits: requiredTraits (pawn must have) and disallowedTraits
        /// (pawn must NOT have). Trait names are defName matches.
        /// </summary>
        private static bool CheckTraits(Pawn pawn, ZCBackstoryDef def)
        {
            bool hasRequired = def.requiredTraits == null || def.requiredTraits.Count == 0;
            bool hasDisallowed = def.disallowedTraits == null || def.disallowedTraits.Count == 0;
            if (hasRequired && hasDisallowed)
                return true;

            if (pawn.story?.traits == null) return false;

            List<Trait> allTraits = pawn.story.traits.allTraits;

            // Required traits — pawn must have ALL of these
            if (def.requiredTraits != null)
            {
                for (int i = 0; i < def.requiredTraits.Count; i++)
                {
                    string traitName = def.requiredTraits[i];
                    bool found = false;
                    for (int j = 0; j < allTraits.Count; j++)
                    {
                        if (allTraits[j].def.defName == traitName)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) return false;
                }
            }

            // Disallowed traits — pawn must have NONE of these
            if (def.disallowedTraits != null)
            {
                for (int i = 0; i < def.disallowedTraits.Count; i++)
                {
                    string traitName = def.disallowedTraits[i];
                    for (int j = 0; j < allTraits.Count; j++)
                    {
                        if (allTraits[j].def.defName == traitName)
                            return false;
                    }
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
        /// Backstories with higher commonality are more likely to be chosen.
        /// Commonality of 0 excludes the backstory entirely.
        /// </summary>
        public static ZCBackstoryDef SelectByCommonality(List<ZCBackstoryDef> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0];

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                int weight = pool[i].commonality;
                if (weight > 0) totalWeight += weight;
            }

            if (totalWeight <= 0) return pool.RandomElement();

            int roll = Rand.RangeInclusive(1, totalWeight);
            int cumulative = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                int weight = pool[i].commonality;
                if (weight <= 0) continue;
                cumulative += weight;
                if (roll <= cumulative) return pool[i];
            }

            return pool[pool.Count - 1];
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

            // Only validate if the selected backstory is a ZCBackstoryDef
            BackstoryDef current = pawn.story.Childhood;
            if (!(current is ZCBackstoryDef zcb))
                return;

            _reentrant = true;
            try
            {
                for (int i = 0; i < MaxRetries; i++)
                {
                    if (ZCBackstoryValidator.IsValidFor(pawn, zcb))
                    {
                        // Apply ZCB-specific effects (passion gains, work tags)
                        ApplyZCBEffects(pawn, zcb);
                        return;
                    }

                    // Re-roll the childhood backstory
                    PawnBioAndNameGenerator.FillBackstorySlotShuffled(
                        pawn, slot, backstoryCategories, factionType, mustBeCompatibleTo);

                    current = pawn.story?.Childhood;
                    if (!(current is ZCBackstoryDef))
                        return; // rolled a non-ZCB backstory — RimWorld's normal logic applies
                    zcb = (ZCBackstoryDef)current;
                }

                // Exhausted retries — accept whatever we ended up with
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
        /// Applies ZCB-specific effects that BackstoryDef doesn't natively support:
        /// - passionGains: sets passion level for specific skills
        /// - disablingWorkTags: work tags to disable
        /// </summary>
        private static void ApplyZCBEffects(Pawn pawn, ZCBackstoryDef def)
        {
            // Passion gains
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
                        // level 0 = none, 1 = minor, 2 = major
                        int passionLevel = Math.Max(0, Math.Min(2, pg.level));
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
