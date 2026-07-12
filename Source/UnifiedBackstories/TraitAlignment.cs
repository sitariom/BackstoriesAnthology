using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    // A trait we'd like to see (defName + degree).
    public struct FavoredTrait
    {
        public string defName;
        public int degree;
        public FavoredTrait(string d, int deg) { defName = d; degree = deg; }
    }

    // A trait/degree-range we'd rather the backstory not produce.
    public struct OpposedTrait
    {
        public string defName;
        public int minDeg;
        public int maxDeg;
        public OpposedTrait(string d, int lo, int hi) { defName = d; minDeg = lo; maxDeg = hi; }
    }

    public class BackstoryProfile
    {
        public List<FavoredTrait> favored = new List<FavoredTrait>();
        public List<OpposedTrait> opposed = new List<OpposedTrait>();

        private void Fav(string d, int deg = 0) { favored.Add(new FavoredTrait(d, deg)); }
        private void Opp(string d, int lo = 0, int hi = 0) { opposed.Add(new OpposedTrait(d, lo, hi)); }

        private static bool Any(string text, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (text.Contains(keys[i])) return true;
            }
            return false;
        }

        public static BackstoryProfile Build(Pawn pawn)
        {
            var p = new BackstoryProfile();
            BackstoryDef c = pawn.story.Childhood;
            BackstoryDef a = pawn.story.Adulthood;
            string text = (Txt(c) + " " + Txt(a)).ToLowerInvariant();

            bool violentDisabled =
                (c != null && (c.workDisables & WorkTags.Violent) != 0) ||
                (a != null && (a.workDisables & WorkTags.Violent) != 0);

            bool combat = !violentDisabled && Any(text,
                "soldier", "marine", "warrior", "mercenary", "merc ", "fighter", "gladiator",
                "sniper", "assassin", "raider", "guard", "knight", "trooper", "veteran",
                "commando", "militia", "combat", "gunfighter", "gunslinger", "bounty",
                "sergeant", "legion", "janissary", "infantry", "cavalry", "artillery",
                "warlord", "warchief", "chieftain", "enforcer", "brawler", "duelist",
                "swordsman", "musketeer", "gangster", "kingpin", "cadet");
            bool hardship = Any(text,
                "slave", "labor", "camp", "wasteland", "surviv", "orphan", "street", "urchin",
                "war ", "refugee", "prison", "bunker", "apocalyp", "gang", "pit ", "coliseum",
                "exile", "banish", "scaveng", "hooligan", "brute", "outlaw", "feral", "wild");
            bool pampered = Any(text,
                "pamper", "spoil", "noble", "lordling", "royal", "baron", "prince", "privileg",
                "idol", "dilettante", "aristocrat", "heir", "wealthy", "socialite", "rich",
                "courtier", "diplomat", "regent", "emperor", "empress",
                "queen", "manor", "estate", "highborn", "duchess", "high lord");
            bool sickly = Any(text, "sickly", "frail", "invalid", "weak child", "bedridden");
            bool killer = Any(text,
                "assassin", "killer", "murder", "serial", "torturer", "executioner",
                "psychopath", "butcher", "maniac", "slayer", "headhunter", "sadist",
                "ruthless", "cannibal", "trafficker", "warlord", "terrorist", "slaughter");
            // A hardened fighter or survivor is not "soft", even if the title also
            // carries a noble word (raider king, royal guard, mercenary lord).
            bool soft = (pampered || sickly) && !combat && !hardship && !killer;

            bool brainy = Any(text,
                "scientist", "research", "engineer", "professor", "scholar", "academ",
                "physicist", "chemist", "mathematic", "genius", "hacker", "programm",
                "technician", "biolog", "genetic", "astronom", "philosoph", "nerd",
                "prodigy", "inventor", "doctor", "medic", "surgeon", "analyst", "coder");
            bool dumb = !brainy && Any(text,
                "drone", "oaf", "dunce", "grunt", "menial", "dimwit", "simpleton", "thug");
            bool gentle = !killer && Any(text,
                "healer", "nurse", "missionary", "monastery", "priest", "chaplain", "caretaker",
                "empath", "counselor", "teacher", "gardener", "florist", "medic ",
                "pacifist");
            bool hardworking = Any(text,
                "farmer", "laborer", "labourer", "miner", "worker", "builder", "digger",
                "hauler", "factory", "smith", "mason", "carpenter", "hand ", "roughneck",
                "stalwart", "diligent");
            bool lazy = Any(text,
                "lazy", "idol", "gamer", "addict", "couch", "slacker",
                "malingerer", "party", "drifter", "loafer", "slob");
            bool recluse = Any(text,
                "hermit", "loner", "recluse", "isolat", "antisocial", "solitary", "feral",
                "castaway", "mute", "quiet", "wolves", "monkey", "reclusive");
            bool animalLover = Any(text,
                "rancher", "herder", "ranger", "animal", "pet ", "wildlife", "beast",
                "huntress", "muffalo", "veterinar", "tamer", "stable", "shepherd", "cynolog",
                "reptile", "zoolog", "herpetolog");
            bool techphobe = Any(text,
                "tribal", "primitiv", "luddite", "medieval", "shaman", "feral", "vaulter");
            bool wanderer = Any(text,
                "nomad", "wander", "drifter", "traveler", "traveller", "vagabond",
                "adventurer", "roamer", "voyager", "explorer");
            bool rebel = Any(text,
                "rebel", "revolution", "insurgent", "anarchist", "dissident", "resistance", "mutin");
            bool vengeful = Any(text, "vengean", "revenge", "avenger", "vengeful", "retribution", "vendetta");
            bool foodie = Any(text,
                "chef", "cook", "culinary", "gastronom", "baker", "confection", "taster",
                "sommelier", "gourmet", "brewer", "winemak");
            bool beauty = Any(text, "model", "pop idol", "beaut", "pageant", "glamour");
            bool coldEnv = Any(text, "arctic", "ice", "tundra", "frozen", "snow", "northern", "glacier", "iceworld", "eskimo");
            bool hotEnv = Any(text, "desert", "dune", "sand", "scorch", "ashworld", "dustball");
            bool mountainEnv = Any(text, "cave", "mountain", "undergroun", "tunnel", "digger", "caveworld", "spelunk");
            bool seaEnv = Any(text, "sailor", "mariner", "fisher", "ocean", "boatman", "seaman", "naval sailor");
            bool pyro = Any(text, "arson", "pyroman", "firestarter", "firebug");

            // ---- Toughness / bravery ----
            if (combat || hardship)
            {
                p.Fav("Tough"); p.Fav("VTE_ThickSkinned"); p.Fav("VTE_Brave"); p.Fav("Bravery", 1);
                p.Opp("Wimp"); p.Opp("Delicate"); p.Opp("VTE_ThinSkinned"); p.Opp("VTE_Clumsy");
                p.Opp("VTE_Coward"); p.Opp("VTE_Anxious"); p.Opp("Bravery", -1, -1);
                if (combat) { p.Fav("VTE_MartialArtist"); }
            }
            if (soft)
            {
                p.Fav("VTE_ThinSkinned"); p.Fav("VTE_Coward"); p.Fav("Bravery", -1);
                if (sickly) { p.Fav("Wimp"); p.Fav("Delicate"); }
                p.Opp("Tough"); p.Opp("VTE_ThickSkinned"); p.Opp("VTE_Brave"); p.Opp("Bravery", 1, 2);
                p.Opp("VTE_MartialArtist");
            }

            // ---- Work ethic ----
            if (hardworking && !soft)
            {
                p.Fav("Industriousness", 1); p.Fav("VTE_Workaholic");
                p.Opp("Industriousness", -2, -1); p.Opp("VTE_CouchPotato");
            }
            if (lazy || soft)
            {
                p.Fav("Industriousness", -1); p.Fav("VTE_CouchPotato");
                p.Opp("Industriousness", 1, 2); p.Opp("VTE_Workaholic");
            }

            // ---- Intellect ----
            if (brainy)
            {
                p.Fav("TooSmart"); p.Fav("VTE_Academian"); p.Fav("FastLearner");
                p.Opp("SlowLearner"); p.Opp("VTE_Dunce");
            }
            if (dumb)
            {
                p.Fav("VTE_Dunce"); p.Fav("SlowLearner");
                p.Opp("TooSmart"); p.Opp("VTE_Academian"); p.Opp("VTE_Prodigy");
            }

            // ---- Violence disposition ----
            if (killer)
            {
                p.Fav("Bloodlust"); p.Fav("VTE_Desensitized");
                p.Opp("Kind");
            }
            if (gentle)
            {
                p.Fav("Kind");
                p.Opp("Bloodlust"); p.Opp("Psychopath"); p.Opp("VTE_Desensitized");
            }

            // ---- Social ----
            if (recluse)
            {
                p.Fav("Recluse"); p.Fav("VTE_Schizoid");
            }

            // ---- Animals ----
            if (animalLover)
            {
                p.Fav("VTE_AnimalLover");
                p.Opp("VTE_AnimalHater");
            }

            // ---- Tech ----
            if (techphobe)
            {
                p.Fav("VTE_Technophobe");
                p.Opp("Transhumanist");
            }

            // ---- Disposition ----
            if (wanderer) { p.Fav("VTE_Wanderlust"); }
            if (rebel) { p.Fav("VTE_Rebel"); p.Opp("VTE_Submissive"); }
            if (vengeful) { p.Fav("VTE_Vengeful"); }
            if (foodie) { p.Fav("Gourmand"); p.Fav("VTE_Gastronomist"); }
            if (beauty) { p.Fav("Beauty", 1); }

            // ---- Thematic environment ----
            if (coldEnv) { p.Fav("VTE_ColdInclined"); p.Opp("VTE_HeatInclined"); }
            if (hotEnv) { p.Fav("VTE_HeatInclined"); p.Opp("VTE_ColdInclined"); }
            if (mountainEnv) { p.Fav("Undergrounder"); p.Fav("VTE_ChildOfMountain"); }
            if (seaEnv) { p.Fav("VTE_ChildOfSea"); }
            if (pyro) { p.Fav("Pyromaniac"); }

            return p;
        }

        private static string Txt(BackstoryDef b)
        {
            if (b == null) return "";
            return b.defName + " " + b.title;
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateTraitsFor))]
    public static class TraitAlignment_Patch
    {
        private const float RerollOpposedChance = 0.85f;
        private const float InjectFavoredChance = 0.6f;
        private const int RerollTries = 6;

        private static bool busy;

        public static void Postfix(List<Trait> __result, Pawn pawn, int traitCount,
            PawnGenerationRequest? req, bool growthMomentTrait)
        {
            if (busy || __result == null || pawn?.story == null)
            {
                return;
            }
            if (UB_Mod.Settings != null && !UB_Mod.Settings.traitAlignment)
            {
                return;
            }
            if (pawn.story.Childhood == null && pawn.story.Adulthood == null)
            {
                return;
            }
            BackstoryProfile profile = BackstoryProfile.Build(pawn);
            if (profile.favored.Count == 0 && profile.opposed.Count == 0)
            {
                return;
            }
            busy = true;
            try
            {
                // 1. Re-roll backstory-opposed traits (usually).
                for (int i = 0; i < __result.Count; i++)
                {
                    if (IsOpposed(profile, __result[i]) && Rand.Value < RerollOpposedChance)
                    {
                        Trait repl = RerollNonOpposed(pawn, req, growthMomentTrait, __result, profile, i);
                        if (repl != null)
                        {
                            __result[i] = repl;
                        }
                    }
                }
                // 2. Nudge one fitting trait in, by swapping a neutral slot.
                if (Rand.Value < InjectFavoredChance)
                {
                    TryInjectFavored(pawn, __result, profile);
                }
            }
            finally
            {
                busy = false;
            }
        }

        private static bool IsOpposed(BackstoryProfile profile, Trait t)
        {
            for (int i = 0; i < profile.opposed.Count; i++)
            {
                OpposedTrait o = profile.opposed[i];
                if (t.def.defName == o.defName && t.Degree >= o.minDeg && t.Degree <= o.maxDeg)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsFavoredDef(BackstoryProfile profile, TraitDef def)
        {
            for (int i = 0; i < profile.favored.Count; i++)
            {
                if (profile.favored[i].defName == def.defName)
                {
                    return true;
                }
            }
            return false;
        }

        private static Trait RerollNonOpposed(Pawn pawn, PawnGenerationRequest? req,
            bool growthMomentTrait, List<Trait> list, BackstoryProfile profile, int slot)
        {
            for (int attempt = 0; attempt < RerollTries; attempt++)
            {
                List<Trait> rolled = PawnGenerator.GenerateTraitsFor(pawn, 1, req, growthMomentTrait);
                if (rolled == null || rolled.Count == 0)
                {
                    continue;
                }
                Trait cand = rolled[0];
                if (IsOpposed(profile, cand) || DuplicatesOrConflicts(list, cand.def, slot))
                {
                    continue;
                }
                return cand;
            }
            return null;
        }

        private static void TryInjectFavored(Pawn pawn, List<Trait> list, BackstoryProfile profile)
        {
            // Find a slot holding a "neutral" trait (neither favored nor opposed).
            int neutralSlot = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (!IsFavoredDef(profile, list[i].def) && !IsOpposed(profile, list[i]))
                {
                    neutralSlot = i;
                    break;
                }
            }
            if (neutralSlot < 0)
            {
                return;
            }
            // Collect addable favored traits.
            List<Trait> options = new List<Trait>();
            for (int i = 0; i < profile.favored.Count; i++)
            {
                FavoredTrait f = profile.favored[i];
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(f.defName);
                if (def == null)
                {
                    continue;
                }
                if (CanPlace(pawn, list, def, f.degree, neutralSlot))
                {
                    options.Add(new Trait(def, f.degree));
                }
            }
            if (options.Count > 0)
            {
                list[neutralSlot] = options.RandomElement();
            }
        }

        private static bool DuplicatesOrConflicts(List<Trait> list, TraitDef def, int exceptIndex)
        {
            for (int j = 0; j < list.Count; j++)
            {
                if (j == exceptIndex)
                {
                    continue;
                }
                if (list[j].def == def || def.ConflictsWith(list[j]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CanPlace(Pawn pawn, List<Trait> list, TraitDef def, int degree, int exceptIndex)
        {
            if (pawn.story.traits.HasTrait(def))
            {
                return false;
            }
            if (DuplicatesOrConflicts(list, def, exceptIndex))
            {
                return false;
            }
            List<Trait> owned = pawn.story.traits.allTraits;
            for (int i = 0; i < owned.Count; i++)
            {
                if (def.ConflictsWith(owned[i]))
                {
                    return false;
                }
            }
            if (pawn.story.Childhood != null && pawn.story.Childhood.DisallowsTrait(def, degree))
            {
                return false;
            }
            if (pawn.story.Adulthood != null && pawn.story.Adulthood.DisallowsTrait(def, degree))
            {
                return false;
            }
            if (pawn.WorkTagIsDisabled(def.requiredWorkTags))
            {
                return false;
            }
            return true;
        }
    }
}
