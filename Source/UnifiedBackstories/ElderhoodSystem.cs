using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    // HELPER METHODS
    // ================================================================

    public static class ElderhoodHelper
    {
        public static BackstoryDef GetElderhoodFor(Pawn pawn)
        {
            if (pawn?.story == null) return null;
            BackstoryDef bs = null;
            if (pawn.IsColonist)
                bs = FindDef("UB_Elderhood_PlayerColonist");
            else if (pawn.IsPrisonerOfColony)
                bs = FindDef("UB_Elderhood_PlayerPrisoner");
            else if (pawn.IsSlaveOfColony)
                bs = FindDef("UB_Elderhood_PlayerSlave");
            return bs ?? RandomElderhood();
        }

        private static BackstoryDef FindDef(string name)
        {
            return DefDatabase<BackstoryDef>.GetNamedSilentFail(name);
        }

        private static BackstoryDef RandomElderhood()
        {
            List<BackstoryDef> pool = DefDatabase<BackstoryDef>.AllDefsListForReading
                .Where(d => d.spawnCategories != null &&
                    (d.spawnCategories.Contains("Elderhood") ||
                     d.spawnCategories.Contains("SpecialElderhood")))
                .ToList();
            return pool.Count > 0 ? pool.RandomElement() : null;
        }

        public static bool IsElderhood(this BackstoryDef bs)
        {
            if (bs?.spawnCategories == null) return false;
            return bs.spawnCategories.Contains("Elderhood") ||
                   bs.spawnCategories.Contains("SpecialElderhood");
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
        /// For saves created before this mod, the comp won't exist yet.
        /// </summary>
        public static CompElderhoodBackstory GetOrCreateElderhoodComp(Pawn pawn)
        {
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp != null) return comp;

            var props = new CompProperties_ElderhoodBackstory();
            comp = new CompElderhoodBackstory();
            comp.parent = pawn;
            comp.Initialize(props);
            // Add comp via reflection (ThingWithComps.comps may not be public)
            var compsField = typeof(ThingWithComps).GetField("comps", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (compsField != null)
            {
                var list = compsField.GetValue(pawn);
                if (list is System.Collections.IList ilist)
                {
                    ilist.Add(comp);
                }
            }
            return comp;
        }

        /// <summary>
        /// Assigns elderhood if pawn qualifies. Used by birthday, load, and generation patches.
        /// </summary>
        public static void TryAssignElderhood(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;

            CompElderhoodBackstory comp = GetOrCreateElderhoodComp(pawn);
            if (comp.HasElderhood) return;
            if (pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYears < comp.ElderhoodAge) return;

            BackstoryDef elderhood = GetElderhoodFor(pawn);
            if (elderhood != null)
            {
                comp.SetElderhood(elderhood);
                pawn.skills?.Notify_SkillDisablesChanged();
            }
        }
    }

    // ================================================================
    // HARMONY PATCHES
    // ================================================================

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

    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_Patch
    {
        public static void Postfix(Pawn __result)
        {
            if (__result == null) return;
            ElderhoodHelper.TryAssignElderhood(__result);
        }
    }

    [HarmonyPatch]
    public static class CharacterCardUtility_DoLeftSection_Patch
    {
        private static Type _ccuType;
        private static FieldInfo fiCurY;
        private static FieldInfo fiPawn;
        private static FieldInfo fiLeft;
        private static Texture2D _infoIcon;

        public static MethodBase TargetMethod()
        {
            _ccuType = AccessTools.TypeByName("RimWorld.CharacterCardUtility");
            if (_ccuType == null) return null;
            fiCurY = _ccuType.GetField("curY", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                  ?? _ccuType.GetField("currentY", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            fiPawn = _ccuType.GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            fiLeft = _ccuType.GetField("leftRect", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _infoIcon = AccessTools.Field(typeof(Widgets), "InfoButton")?.GetValue(null) as Texture2D;
            return AccessTools.Method(_ccuType, "DoLeftSection");
        }

        public static void Postfix(object __instance)
        {
            if (__instance == null || fiCurY == null || fiPawn == null) return;

            Pawn pawn = fiPawn.GetValue(__instance) as Pawn;
            if (pawn == null || !pawn.RaceProps.Humanlike || pawn.story == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;

            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;
            BackstoryDef elderhood = comp.ElderhoodBS;
            if (elderhood == null) return;

            float curY = (float)fiCurY.GetValue(__instance);
            float width = 196f;
            if (fiLeft?.GetValue(__instance) is Rect lr) width = lr.width;

            // 1. Category label
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 30f), "Elderhood".Translate());
            curY += 28f;

            // 2. Title + info button
            Text.Font = GameFont.Small;
            string title = ElderhoodHelper.TitleFor(elderhood, pawn);
            string shortTitle = ElderhoodHelper.TitleShortFor(elderhood, pawn);
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

            // 3. Short title
            if (!shortTitle.NullOrEmpty() && shortTitle != title)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, curY, width, 18f), "(" + shortTitle + ")");
                curY += 17f;
                Text.Font = GameFont.Small;
            }

            // 4. Skill gains
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

            // 5. Description
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

    [HarmonyPatch(typeof(Pawn_StoryTracker), "get_DisabledWorkTagsBackstoryAndTraits")]
    public static class Pawn_StoryTracker_DisabledWorkTags_Patch
    {
        public static void Postfix(Pawn_StoryTracker __instance, ref WorkTags __result)
        {
            Pawn pawn = ElderhoodHelper.GetPawnFromTracker(__instance);
            if (pawn == null) return;
            if (UB_Mod.Settings != null && !UB_Mod.Settings.elderhoodEnabled) return;
            CompElderhoodBackstory comp = pawn.GetComp<CompElderhoodBackstory>();
            if (comp == null || !comp.HasElderhood) return;
            BackstoryDef eb = comp.ElderhoodBS;
            if (eb == null) return;
            __result |= eb.workDisables;
        }
    }

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
            for (int i = 0; i < eb.possessions.Count; i++)
            {
                object item = eb.possessions[i];
                if (item == null) continue;
                ThingDef tDef = item.GetType().GetField("thingDef")?.GetValue(item) as ThingDef;
                int tCount = item.GetType().GetField("count")?.GetValue(item) is int cv ? cv : 0;
                if (tDef != null && tCount > 0)
                {
                    Thing thing = ThingMaker.MakeThing(tDef);
                    thing.stackCount = tCount;
                    pawn.inventory?.innerContainer?.TryAdd(thing);
                }
            }
        }
    }

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
                __result = null;
                return false;
            }
            return true;
        }
    }

    [StaticConstructorOnStartup]
    public static class ElderhoodPatcher
    {
        static ElderhoodPatcher()
        {
            new Harmony("UnifiedBackstories.ElderhoodSystem").PatchAll();
            Log.Message("[UB] Elderhood system initialized (replaces ElderhoodBackstory.dll)");
        }
    }
}
