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
    /// Supports additional XML fields: baseDesc, commonality, techLevel,
    /// requiredRecords, requiredSkills, requiredTraits, disallowedTraits, etc.
    /// skillGains must use standard RimWorld format (List of li/skill/amount).
    /// </summary>
    public class ZCBackstoryDef : BackstoryDef
    {
        public int commonality = 1;
        public string minTechLevel;
        public string maxTechLevel;
        public string colonySize;       // range format: "1~99"
        public int developmentalStage;

        public string bodyPartsReplaced; // range format: "1~999"
        public string bodyPartsMissing;  // range format: "1~999"
        public string father;            // "Dead", "Present", "Absent, Dead"
        public string mother;            // "Dead", "Present", "Absent, Dead"

        public List<ZCBRecordReq> requiredRecords;
        public List<ZCBRecordRatio> recordRatios;
        public List<ZCBSkillReq> requiredSkills;
        public List<string> requiredTraits;       // trait names (pawn must have)
        public List<string> requiredPassions;     // skill names (pawn must have passion)
        public new List<string> disallowedTraits;  // trait names (pawn must NOT have)
        public List<ZCBPassionGain> passionGains;
        public List<string> disallowedPassions;
        public List<string> disablingWorkTags;
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

    // ================================================================
}
