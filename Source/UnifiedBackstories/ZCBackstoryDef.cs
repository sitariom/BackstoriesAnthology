using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Custom BackstoryDef subclass for childhood backstories.
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
        public int colonySize;
        public int developmentalStage;

        public List<ZCBRecordReq> requiredRecords;
        public List<ZCBSkillReq> requiredSkills;
        public List<ZCBTraitReq> requiredTraits;
        public List<ZCBPassionReq> requiredPassions;
        public List<ZCBTraitReq> disallowedTraitsCustom;
        public List<ZCBPassionReq> disallowedPassions;
        public List<ZCBColonyReq> colonySizeReqs;
        public List<ZCBPassionGain> passionGains;
        public List<string> disablingWorkTags;
    }

    // --- Helper types for ZCB requirements ---

    public class ZCBRecordReq
    {
        public string name;
        public int minValue;
        public int maxValue;
    }

    public class ZCBSkillReq
    {
        public string name;
        public int minValue;
    }

    public class ZCBTraitReq
    {
        public string name;
        public int degree;
    }

    public class ZCBPassionReq
    {
        public string name;
        public string level;
    }

    public class ZCBPassionGain
    {
        public string name;
        public string level;
    }

    public class ZCBColonyReq
    {
        public int minSize;
        public int maxSize;
    }

    // ================================================================
}
