using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace UnifiedBackstories
{
    public class UBSettings : ModSettings
    {
        // === XML-driven (changing these requires a game restart) ===

        // Reasonable Moods group
        public bool capableBackstories = true;
        public bool moodRebalance = true;
        public bool griefTaper = true;

        // Elderhood
        public bool elderhoodEnabled = true;

        // ZCB
        public bool zcbEnabled = true;

        // === C#-driven (live toggles) ===

        // Reasonable Moods group
        public bool needsGracePeriod = true;
        public bool hardshipBonding = true;
        public bool betrayalWitnesses = true;
        public bool backstoryPairing = true;
        public bool traitAlignment = true;
        public bool romanceCooldown = true;
        public bool awkwardChat = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref capableBackstories, "capableBackstories", true);
            Scribe_Values.Look(ref moodRebalance, "moodRebalance", true);
            Scribe_Values.Look(ref griefTaper, "griefTaper", true);
            Scribe_Values.Look(ref elderhoodEnabled, "elderhoodEnabled", true);
            Scribe_Values.Look(ref zcbEnabled, "zcbEnabled", true);
            Scribe_Values.Look(ref needsGracePeriod, "needsGracePeriod", true);
            Scribe_Values.Look(ref hardshipBonding, "hardshipBonding", true);
            Scribe_Values.Look(ref betrayalWitnesses, "betrayalWitnesses", true);
            Scribe_Values.Look(ref backstoryPairing, "backstoryPairing", true);
            Scribe_Values.Look(ref traitAlignment, "traitAlignment", true);
            Scribe_Values.Look(ref romanceCooldown, "romanceCooldown", true);
            Scribe_Values.Look(ref awkwardChat, "awkwardChat", true);
        }
    }

    public class UB_Mod : Mod
    {
        public static UBSettings Settings;

        public UB_Mod(ModContentPack content) : base(content)
        {
            // Constructed before XML patches are applied, so PatchOperation_IfSetting
            // can read these values during def loading.
            Settings = GetSettings<UBSettings>();
        }

        public override string SettingsCategory()
        {
            return "Unified Backstories";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);

            Text.Font = GameFont.Medium;
            l.Label("UB.Header.ReasonableMoods".Translate());
            Text.Font = GameFont.Small;

            l.Label("UB.RestartHeader".Translate());
            l.GapLine();
            l.CheckboxLabeled("UB.CapableBackstories".Translate(), ref Settings.capableBackstories,
                "UB.CapableBackstories.Tip".Translate());
            l.CheckboxLabeled("UB.MoodRebalance".Translate(), ref Settings.moodRebalance,
                "UB.MoodRebalance.Tip".Translate());
            l.CheckboxLabeled("UB.GriefTaper".Translate(), ref Settings.griefTaper,
                "UB.GriefTaper.Tip".Translate());

            l.Gap(18f);
            l.Label("UB.LiveHeader".Translate());
            l.GapLine();
            l.CheckboxLabeled("UB.NeedsGracePeriod".Translate(), ref Settings.needsGracePeriod,
                "UB.NeedsGracePeriod.Tip".Translate());
            l.CheckboxLabeled("UB.HardshipBonding".Translate(), ref Settings.hardshipBonding,
                "UB.HardshipBonding.Tip".Translate());
            l.CheckboxLabeled("UB.BetrayalWitnesses".Translate(), ref Settings.betrayalWitnesses,
                "UB.BetrayalWitnesses.Tip".Translate());
            l.CheckboxLabeled("UB.BackstoryPairing".Translate(), ref Settings.backstoryPairing,
                "UB.BackstoryPairing.Tip".Translate());
            l.CheckboxLabeled("UB.TraitAlignment".Translate(), ref Settings.traitAlignment,
                "UB.TraitAlignment.Tip".Translate());
            if (UB_ModCompat.RomanceCooldownSuperseded)
            {
                GUI.color = Color.gray;
                l.Label("UB.RomanceCooldown".Translate() + " — " +
                    "UB.AutoDisabled".Translate(UB_ModCompat.RomanceCooldownSupersededBy),
                    tooltip: "UB.AutoDisabled.Tip".Translate(UB_ModCompat.RomanceCooldownSupersededBy));
                GUI.color = Color.white;
            }
            else
            {
                l.CheckboxLabeled("UB.RomanceCooldown".Translate(), ref Settings.romanceCooldown,
                    "UB.RomanceCooldown.Tip".Translate());
            }
            l.CheckboxLabeled("UB.AwkwardChat".Translate(), ref Settings.awkwardChat,
                "UB.AwkwardChat.Tip".Translate());

            // --- Elderhood section ---
            l.Gap(24f);
            Text.Font = GameFont.Medium;
            l.Label("UB.Header.Elderhood".Translate());
            Text.Font = GameFont.Small;
            l.GapLine();
            l.CheckboxLabeled("UB.ElderhoodEnabled".Translate(), ref Settings.elderhoodEnabled,
                "UB.ElderhoodEnabled.Tip".Translate());

            // --- ZCB section ---
            l.Gap(24f);
            Text.Font = GameFont.Medium;
            l.Label("UB.Header.ZCB".Translate());
            Text.Font = GameFont.Small;
            l.GapLine();
            l.CheckboxLabeled("UB.ZCBEnabled".Translate(), ref Settings.zcbEnabled,
                "UB.ZCBEnabled.Tip".Translate());

            l.End();
        }
    }

    /// <summary>
    /// Applies its child operations only when the named setting is enabled.
    /// Mod classes are constructed before XML patching runs, so the settings are
    /// available here. Disabling a setting turns the whole patch file into a no-op
    /// (takes effect after a restart).
    /// </summary>
    public class PatchOperation_IfSetting : PatchOperation
    {
        public string setting;
        public List<PatchOperation> operations;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            if (!Enabled(setting))
            {
                return true; // feature toggled off: succeed without changing anything
            }
            bool ok = true;
            if (operations != null)
            {
                for (int i = 0; i < operations.Count; i++)
                {
                    ok &= operations[i].Apply(xml);
                }
            }
            return ok;
        }

        private static bool Enabled(string name)
        {
            UBSettings s = UB_Mod.Settings;
            if (s == null)
            {
                return true; // defensive: never silently drop patches
            }
            switch (name)
            {
                case "capableBackstories": return s.capableBackstories;
                case "moodRebalance": return s.moodRebalance;
                case "griefTaper": return s.griefTaper;
                case "elderhoodEnabled": return s.elderhoodEnabled;
                case "zcbEnabled": return s.zcbEnabled;
                default:
                    Log.Warning("[UB] Unknown patch setting '" + name + "', applying anyway.");
                    return true;
            }
        }
    }
}
