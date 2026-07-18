using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// </summary>
        public static CompElderhoodBackstory GetOrCreateElderhoodComp(Pawn pawn)
        {
            if (pawn == null) return null;
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp != null) return comp;

            // Manual creation fallback — this should rarely execute since
            // CompProperties_ElderhoodBackstory is added to Human via XML patch.
            var props = new CompProperties_ElderhoodBackstory();
            comp = new CompElderhoodBackstory();
            comp.parent = pawn;
            comp.Initialize(props);

            try
            {
                var compsField = typeof(ThingWithComps).GetField("comps",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (compsField != null)
                {
                    var list = compsField.GetValue(pawn);
                    if (list is System.Collections.IList ilist)
                        ilist.Add(comp);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[UB] GetOrCreateElderhoodComp reflection failed: " + ex.Message);
            }
            return comp;
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
                if (te == null || te.def == null) continue;
                if (pawn.story.traits == null) continue;

                // Skip if pawn already has this trait
                if (pawn.story.traits.HasTrait(te.def)) continue;

                // Skip if trait is prohibited
                var prohibited = request.ProhibitedTraits;
                if (prohibited != null && prohibited.Contains(te.def))
                    continue;

                if (te.def != null)
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
            ElderhoodHelper.TryAssignElderhood(pawn);
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
            if (__result.ageTracker.AgeBiologicalYears < 60) return;

            CompElderhoodBackstory comp = ElderhoodHelper.GetOrCreateElderhoodComp(__result);
            if (comp == null) return;

            // Assign elderhood if not already set
            if (!comp.HasElderhood)
            {
                BackstoryDef elderhood = ElderhoodHelper.GetElderhoodFor(__result);
                if (elderhood == null) return;
                comp.SetElderhood(elderhood);
                __result.skills?.Notify_SkillDisablesChanged();
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

            // Skill bonuses are applied by PawnGenerator_FinalLevelOfSkill_Patch.
            // Do NOT apply them here — doing so would double-count for pawns whose
            // elderhood was already set during bio generation (the normal path).

            // Apply body type
            BodyTypeDef bt = DefDatabase<BodyTypeDef>.GetNamedSilentFail(
                pawn.gender == Gender.Female ? "FemaleElderly" : "MaleElderly");
            if (bt != null && pawn.story?.bodyType != null)
                pawn.story.bodyType = bt;
        }
    }

    /// <summary>
    /// Character card UI: display elderhood info.
    /// Uses the original approach with a Postfix on DoLeftSection.
    /// </summary>
    [StaticConstructorOnStartup]
    [HarmonyPatch]
    public static class CharacterCardUtility_DoLeftSection_Patch
    {
        private static Type _ccuType;
        private static FieldInfo fiCurY;
        private static FieldInfo fiPawn;
        private static FieldInfo fiLeft;
        private static Texture2D _infoIcon;
        private static Texture2D _editIcon;
        private static Texture2D _clearIcon;
        private static bool _iconsLoaded;

        public static MethodBase TargetMethod()
        {
            _ccuType = AccessTools.TypeByName("RimWorld.CharacterCardUtility");
            if (_ccuType == null) return null;
            fiCurY = _ccuType.GetField("curY", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                  ?? _ccuType.GetField("currentY", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            fiPawn = _ccuType.GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            fiLeft = _ccuType.GetField("leftRect", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return AccessTools.Method(_ccuType, "DoLeftSection");
        }

        public static void Postfix(object __instance)
        {
            if (__instance == null || fiCurY == null || fiPawn == null) return;

            Pawn pawn = fiPawn.GetValue(__instance) as Pawn;
            if (pawn == null || !pawn.RaceProps.Humanlike || pawn.story == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;

            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null)
                comp = ElderhoodHelper.GetOrCreateElderhoodComp(pawn);
            bool hasElderhood = comp != null && comp.HasElderhood;

            // Only show section for pawns 60+ or with existing elderhood
            if (!hasElderhood && (pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYears < 60))
                return;
            if (comp == null) return;

            // Lazy-load icons
            if (!_iconsLoaded)
            {
                _iconsLoaded = true;
                _infoIcon = ContentFinder<Texture2D>.Get("UI/InfoButton", false)
                           ?? ContentFinder<Texture2D>.Get("UI/Icons/InfoButton", false);
                _editIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Edit", false)
                           ?? ContentFinder<Texture2D>.Get("UI/Icons/Edit", false);
                _clearIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Delete", false)
                            ?? ContentFinder<Texture2D>.Get("UI/Icons/Delete", false);
            }

            float curY = (float)fiCurY.GetValue(__instance);
            float width = 196f;
            if (fiLeft?.GetValue(__instance) is Rect lr) width = lr.width;

            // === Category label ===
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 30f), "UB.ElderhoodHeader".Translate());

            // === Edit / Clear buttons (top-right of header) ===
            Text.Font = GameFont.Tiny;
            float btnSize = 22f;
            float btnX = width - btnSize;

            if (hasElderhood)
            {
                // Clear button
                Texture2D clearTex = _clearIcon ?? _infoIcon;
                Rect clearRect = new Rect(btnX, curY + 2f, btnSize, btnSize);
                if (Widgets.ButtonImage(clearRect, clearTex, Color.white, Color.grey * 1.5f))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("UB.ClearElderhood".Translate(), delegate
                        {
                            comp.SetElderhood(null);
                            pawn.skills?.Notify_SkillDisablesChanged();
                        }),
                        new FloatMenuOption("UB.Cancel".Translate(), null),
                    };
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                TooltipHandler.TipRegion(clearRect, "UB.ClearElderhood.Tip".Translate());
                btnX -= btnSize + 2f;

                // Edit (change) button
                Texture2D editTex = _editIcon ?? _infoIcon;
                Rect editRect = new Rect(btnX, curY + 2f, btnSize, btnSize);
                if (Widgets.ButtonImage(editRect, editTex))
                {
                    Find.WindowStack.Add(new Dialog_ChooseElderhood(pawn, comp));
                }
                TooltipHandler.TipRegion(editRect, "UB.ChangeElderhood.Tip".Translate());
            }
            else
            {
                // No elderhood yet — show "Add" button
                Rect addRect = new Rect(btnX, curY + 2f, btnSize, btnSize);
                Texture2D addTex = _editIcon ?? _infoIcon;
                if (Widgets.ButtonImage(addRect, addTex))
                {
                    Find.WindowStack.Add(new Dialog_ChooseElderhood(pawn, comp));
                }
                TooltipHandler.TipRegion(addRect, "UB.AddElderhood.Tip".Translate());
            }

            curY += 28f;

            if (!hasElderhood)
            {
                // No elderhood assigned — show hint
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, curY, width, 20f), "UB.NoElderhood".Translate());
                curY += 22f;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, curY, width, 16f), "UB.ClickToAddElderhood".Translate());
                curY += 18f;

                curY += 4f;
                fiCurY.SetValue(__instance, curY);
                return;
            }

            BackstoryDef elderhood = comp.ElderhoodBS;
            if (elderhood == null) return;

            // === Title + info button ===
            Text.Font = GameFont.Small;
            string title = ElderhoodHelper.TitleFor(elderhood, pawn);
            Vector2 titleSize = Text.CalcSize(title);
            float titleW = Math.Min(titleSize.x, width - 28f);
            Widgets.Label(new Rect(0f, curY, titleW, 24f), title);
            if (_infoIcon != null)
            {
                Rect infoRect = new Rect(titleW + 2f, curY, 22f, 22f);
                if (Widgets.ButtonImage(infoRect, _infoIcon))
                    Find.WindowStack.Add(new Dialog_InfoCard(elderhood));
                TooltipHandler.TipRegion(infoRect, () => ElderhoodHelper.TitleCapFor(elderhood, pawn), 63321);
            }
            curY += 24f;

            // === Short title ===
            string shortTitle = ElderhoodHelper.TitleShortFor(elderhood, pawn);
            if (!shortTitle.NullOrEmpty() && shortTitle != title)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, curY, width, 18f), "(" + shortTitle + ")");
                curY += 17f;
                Text.Font = GameFont.Small;
            }

            // === Skill gains ===
            if (elderhood.skillGains != null)
            {
                float sx = 10f, sy = curY;
                for (int i = 0; i < elderhood.skillGains.Count; i++)
                {
                    SkillGain sg = elderhood.skillGains[i];
                    if (sg.skill == null) continue;
                    string txt = sg.skill.LabelCap + " " + (sg.amount >= 0 ? "+" : "") + sg.amount;
                    Vector2 sz = Text.CalcSize(txt);
                    if (sx + sz.x > width - 6f) { sx = 10f; sy += 20f; }
                    Widgets.Label(new Rect(sx, sy, sz.x, 20f), txt);
                    sx += sz.x + 8f;
                }
                curY = Math.Max(curY, sy + 22f);
            }

            // === Description ===
            string desc = elderhood.FullDescriptionFor(pawn);
            if (!desc.NullOrEmpty())
            {
                float descH = Math.Min(Text.CalcHeight(desc, width), 80f);
                Widgets.Label(new Rect(0f, curY, width, descH), desc);
                curY += descH + 2f;
            }

            curY += 4f;
            fiCurY.SetValue(__instance, curY);
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
            this.elderhoods = ElderhoodHelper.ListElderhoods();
            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
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
                    comp.SetElderhood(bs);
                    pawn.skills?.Notify_SkillDisablesChanged();
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
            if (UB_Mod.Settings == null || UB_Mod.Settings.zcbEnabled)
            {
                if (pawn.story?.Childhood is ZCBackstoryDef zcb && zcb.disablingWorkTags != null)
                {
                    for (int i = 0; i < zcb.disablingWorkTags.Count; i++)
                    {
                        if (Enum.TryParse(zcb.disablingWorkTags[i], true, out WorkTags tag))
                            __result |= tag;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Starting possessions from elderhood.
    /// </summary>
    [HarmonyPatch(typeof(StartingPawnUtility), "GeneratePossessions")]
    public static class StartingPawnUtility_GeneratePossessions_Patch
    {
        public static void Postfix(Pawn pawn)
        {
            if (pawn?.story == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;
            BackstoryDef eb = comp.ElderhoodBS;
            if (eb?.possessions == null) return;
            foreach (var item in eb.possessions)
            {
                if (item == null) continue;
                var tdcType = item.GetType();
                ThingDef tDef = tdcType.GetField("thingDef")?.GetValue(item) as ThingDef;
                int tCount = tdcType.GetField("count")?.GetValue(item) is int cv ? cv : 0;
                if (tDef != null && tCount > 0)
                {
                    Thing thing = ThingMaker.MakeThing(tDef);
                    thing.stackCount = tCount;
                    pawn.inventory?.innerContainer?.TryAdd(thing);
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
            Log.Message("[UB] v1.2.4 loaded (build " + ver
                + ") — Elderhood + Gender tokens + Age 60+ + UI edit"
                + " + Mood rebalance + Need grace period + ZCB validator");
        }
    }
}
