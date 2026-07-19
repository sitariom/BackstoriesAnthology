using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Custom BackstoryDef subclass for ZCB childhood backstories.
    /// Replaces ZCB.ZCBackstoryDef from the original Childhood Backstories mod.
    ///
    /// Supports additional XML fields: commonality, minTechLevel, maxTechLevel,
    /// colonySize, bodyPartsReplaced, bodyPartsMissing, father, mother,
    /// requiredRecords, recordRatios, requiredSkills, requiredTraits,
    /// requiredPassions, disallowedPassions, passionGains,
    /// disablingWorkTags, and developmentalStage.
    ///
    /// All fields are enforced by ZCBackstoryValidator during pawn generation.
    /// Matches the original ZCB field semantics (float commonality, IntRange for
    /// colonySize/bodyParts, FamilyStatusFlags for parents, additive passion gains,
    /// child aging rate scaling for records/skills).
    ///
    /// XML parsing: standard RimWire types (IntRange, TechLevel, FamilyStatusFlags)
    /// are parsed natively. Complex lists use LoadDataFromXmlCustom.
    ///
    /// skillGains use standard RimWorld format (List of li/skill/amount).
    /// requiredTraits/disallowedTraits use DIRECT format: &lt;TraitDefName&gt;degree&lt;/TraitDefName&gt;
    /// passionGains use DIRECT format: &lt;SkillDefName&gt;degree&lt;/SkillDefName&gt;
    /// </summary>
    public class ZCBackstoryDef : BackstoryDef
    {
        public float commonality = 1f;
        public TechLevel minTechLevel;
        // HIGH-006 fix: default maxTechLevel to Archotech (max enum value)
        // so that setting only minTechLevel doesn't reject everything above Undefined.
        // The CheckTechLevel validator also treats Undefined as "no upper bound"
        // for defense-in-depth.
        public TechLevel maxTechLevel = TechLevel.Archotech;
        public IntRange colonySize = new IntRange(0, 9999);
        public int developmentalStage;
        public IntRange bodyPartsMissing = new IntRange(0, 999);
        public IntRange bodyPartsReplaced = new IntRange(0, 999);
        public FamilyStatusFlags father = FamilyStatusFlags.Any;
        public FamilyStatusFlags mother = FamilyStatusFlags.Any;

        public List<ZCBRecordReq> requiredRecords;
        public List<ZCBRecordRatio> recordRatios;
        public List<ZCBSkillReq> requiredSkills;
        public List<BackstoryTrait> requiredTraits;       // trait def+degree (pawn must have)
        // NOTE: disallowedTraits is inherited from BackstoryDef (List<BackstoryTrait>)
        public List<string> requiredPassions;              // skill defNames (pawn must have passion)
        public List<string> disallowedPassions;            // skill defNames (pawn must NOT have passion)
        public List<ZCBPassionGain> passionGains;          // ADDITIVE passion changes (matched to original ZCB)
        public List<string> disablingWorkTags;

        // HIGH-004 fix: cache parsed WorkTags to avoid Enum.TryParse on every
        // DisabledWorkTagsBackstoryAndTraits getter call (hot path — called
        // dozens of times per tick per pawn for work assignment + UI rendering).
        private WorkTags? _cachedDisabledWorkTags;
        private bool _disabledWorkTagsCached;

        public WorkTags GetCachedDisabledWorkTags()
        {
            if (_disabledWorkTagsCached)
                return _cachedDisabledWorkTags ?? WorkTags.None;

            WorkTags result = WorkTags.None;
            if (disablingWorkTags != null)
            {
                for (int i = 0; i < disablingWorkTags.Count; i++)
                {
                    if (Enum.TryParse(disablingWorkTags[i], true, out WorkTags tag))
                        result |= tag;
                }
            }
            _cachedDisabledWorkTags = result;
            _disabledWorkTagsCached = true;
            return result;
        }
    }

    // --- Helper types for ZCB requirements ---

    public class ZCBRecordReq
    {
        public string name;
        public int minValue;
        public int maxValue;
    }

    public class ZCBRecordRatio
    {
        public string numerator;
        public string denominator;
        public float ratio;
    }

    public class ZCBSkillReq
    {
        public string name;
        public int minValue;
        public int maxValue;
    }

    public class ZCBPassionGain
    {
        public string skill;
        public int level;
    }

    /// <summary>
    /// Matches the original ZCB FamilyStatusFlags for parent requirements.
    /// [Flags] enum for bitwise combination.
    /// </summary>
    [Flags]
    public enum FamilyStatusFlags
    {
        Any = 1,
        Present = 2,
        Absent = 4,
        Dead = 8
    }

    // ================================================================
}
