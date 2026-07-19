using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    /// <summary>
    /// Realistic backstory pairing. A tribal childhood followed by a void-space
    /// raider adulthood makes no sense; a slave childhood rarely leads to high
    /// royalty. When shuffled generation produces an implausible pairing, the
    /// adulthood is re-rolled up to three times - implausible life stories
    /// become rare, not impossible.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.FillBackstorySlotShuffled))]
    public static class BackstoryPairing_Patch
    {
        private const int MaxRerolls = 3;
        private static bool rerolling;

        public static void Postfix(Pawn pawn, BackstorySlot slot,
            List<BackstoryCategoryFilter> backstoryCategories, FactionDef factionType,
            BackstorySlot? mustBeCompatibleTo)
        {
            if (rerolling || slot != BackstorySlot.Adulthood)
            {
                return;
            }
            if (UB_Mod.Settings != null && !UB_Mod.Settings.backstoryPairing)
            {
                return;
            }
            BackstoryDef childhood = pawn.story?.Childhood;
            if (childhood == null || pawn.story.Adulthood == null)
            {
                return;
            }
            // MED-021 fix: preserve original adulthood in case all rerolls fail.
            // Without this, the 3rd implausible roll replaces the original.
            BackstoryDef originalAdulthood = pawn.story.Adulthood;
            rerolling = true;
            try
            {
                for (int i = 0; i < MaxRerolls && !BackstoryPlausibility.Plausible(childhood, pawn.story.Adulthood); i++)
                {
                    PawnBioAndNameGenerator.FillBackstorySlotShuffled(pawn, slot, backstoryCategories, factionType, mustBeCompatibleTo);
                }
                // If still implausible after all rerolls, restore original
                if (!BackstoryPlausibility.Plausible(childhood, pawn.story.Adulthood))
                {
                    pawn.story.Adulthood = originalAdulthood;
                }
            }
            finally
            {
                rerolling = false;
            }
        }
    }

    public static class BackstoryPlausibility
    {
        private enum World
        {
            Unknown,     // pirates, outsiders, slaves, cults - could be anywhere
            Primitive,   // tribal
            Medieval,    // medieval worlds
            Industrial,  // outlander towns, urbworlds, rimworld colonies
            Spacefaring  // offworld, glitterworld, imperial
        }

        private static readonly Dictionary<string, World> CategoryWorlds = new Dictionary<string, World>
        {
            { "Tribal", World.Primitive },
            { "TribalHunter", World.Primitive },
            { "TribalFarmer", World.Primitive },
            { "TribalLogger", World.Primitive },
            { "TribalMiner", World.Primitive },
            { "ChildTribal", World.Primitive },
            { "AdultTribal", World.Primitive },
            { "MedievalCommon", World.Medieval },
            { "MedievalHigh", World.Medieval },
            { "MedievalRoyal", World.Medieval },
            { "MedievalNoble", World.Medieval },     // MED-019 fix: added missing categories
            { "MedievalArtist", World.Medieval },
            { "Outlander", World.Industrial },
            { "Civil", World.Industrial },
            { "Researcher", World.Industrial },
            { "Scientist", World.Industrial },
            { "Offworld", World.Spacefaring },
            { "ImperialCommon", World.Spacefaring },
            { "ImperialFighter", World.Spacefaring },
            { "ImperialRoyal", World.Spacefaring },
            { "EmpireCommon", World.Spacefaring },
            { "Vatgrown", World.Spacefaring },
            // MED-019: unmapped categories default to Unknown (no tier signal)
            { "Pirate", World.Unknown },
            { "Slave", World.Unknown },
            { "Cult", World.Unknown },
            { "Madman", World.Unknown },
            { "InsectsRelated", World.Unknown },
            { "Outsider", World.Unknown },
            { "Rare", World.Unknown },
        };

        private static readonly HashSet<string> LowbornCategories = new HashSet<string>
        {
            "Slave", "Pirate", "Cult", "Madman", "InsectsRelated",
            "Tribal", "TribalHunter", "TribalFarmer", "TribalLogger", "TribalMiner", "ChildTribal",
        };

        // Childhoods whose stories make a later noble title absurd.
        private static readonly string[] LowbornKeywords =
        {
            "slave", "urchin", "beggar", "street", "organ farm", "feral", "wolves",
            "abandoned", "thrown", "refugee", "captive", "hostage", "mowgli", "savage",
            "gutter", "orphan", "peasant", "serf",
        };

        // Genuine hereditary nobility - NOT merely a backstory that is *eligible*
        // to spawn on royal-tier pawns (the ImperialRoyal spawn category is put on
        // common soldiers too, so it can't be trusted). Title words only.
        // HIGH-011 fix: added vizier, thane, sultan, emir, shah, caliph, pharaoh,
        // satrap, raja, maharajah, nawab — real noble titles used in BTC/VBE defs.
        private static readonly string[] NobilityKeywords =
        {
            "baron", "duke", "duchess", "viscount", "marquis", "marquess",
            "prince", "princess", "emperor", "empress", "monarch", "aristocrat", "nobleman",
            "noblewoman", "lordling", "archduke", "tsar", "kaiser", "highborn", "high lord",
            "boyar", "highness", "heir to",
            "vizier", "thane", "sultan", "emir", "shah", "caliph", "pharaoh",
            "satrap", "raja", "maharajah", "nawab",
        };

        // A low-tech, planetbound upbringing.
        private static readonly string[] PrimitiveKeywords =
        {
            "tribal", "tribe", "primitiv", "stone age", "hunter-gather", "caveman", "shaman", "witch doctor",
        };
        private static readonly string[] MedievalKeywords =
        {
            "medieval", "feudal", "squire", "serf", "peasant", "jester", "knight",
        };

        // Growing up in one of these needs a space-faring civilization.
        private static readonly string[] SpacerKeywords =
        {
            "glitterworld", "spacer", "orbital", "starship", "void", "galactic", "interstellar",
            "cosmonaut", "astronaut", "space ", "urbworld", "coreworld", "star system",
            "bionic", "cyber", "android", "archotech", "deep space", "space station",
            "vatgrown", "vat-grown", "cryptosleep",
        };

        public static bool Plausible(BackstoryDef childhood, BackstoryDef adulthood)
        {
            // A low-tech, planetbound childhood cannot lead to a spacefaring adulthood -
            // there is no ride off the planet. The reverse (a spacer crash-landing into a
            // tribal life) is perfectly fine.
            if (IsLowTechPlanetbound(childhood) && IsSpacefaring(adulthood))
            {
                return false;
            }

            // Hereditary high nobility is not handed to gutter children.
            if (IsNobility(adulthood) && IsLowborn(childhood))
            {
                return false;
            }

            return true;
        }

        private static string Text(BackstoryDef bs)
        {
            // MED-009 fix: null-guard to prevent NRE if Plausible is ever called
            // with null (currently caller guards, but defensive programming).
            if (bs == null) return "";
            return ((bs.defName ?? "") + " " + (bs.title ?? "")).ToLowerInvariant();
        }

        private static bool AnyKw(string text, string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (text.Contains(keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static HashSet<World> CategoryTiers(BackstoryDef bs)
        {
            var tiers = new HashSet<World>();
            if (bs.spawnCategories != null)
            {
                for (int i = 0; i < bs.spawnCategories.Count; i++)
                {
                    if (CategoryWorlds.TryGetValue(bs.spawnCategories[i], out World w))
                    {
                        tiers.Add(w);
                    }
                }
            }
            return tiers;
        }

        private static bool IsLowTechPlanetbound(BackstoryDef bs)
        {
            string t = Text(bs);
            if (AnyKw(t, SpacerKeywords))
            {
                return false; // raised among the stars => not planetbound low-tech
            }
            HashSet<World> tiers = CategoryTiers(bs);
            bool hasHigh = tiers.Contains(World.Spacefaring) || tiers.Contains(World.Industrial);
            bool lowCat = tiers.Contains(World.Primitive) || tiers.Contains(World.Medieval);
            bool lowKw = AnyKw(t, PrimitiveKeywords) || AnyKw(t, MedievalKeywords);
            // Spans up into space/industry with no explicit low-tech signal => not clearly low-tech.
            if (hasHigh && !lowKw)
            {
                return false;
            }
            return lowCat || lowKw;
        }

        private static bool IsSpacefaring(BackstoryDef bs)
        {
            string t = Text(bs);
            if (AnyKw(t, SpacerKeywords))
            {
                return true;
            }
            HashSet<World> tiers = CategoryTiers(bs);
            // Purely spacefaring categories, nothing lower mixed in.
            return tiers.Contains(World.Spacefaring)
                && !tiers.Contains(World.Primitive)
                && !tiers.Contains(World.Medieval)
                && !tiers.Contains(World.Industrial);
        }

        private static bool IsNobility(BackstoryDef bs)
        {
            return AnyKw(Text(bs), NobilityKeywords);
        }

        private static bool IsLowborn(BackstoryDef bs)
        {
            // HIGH-012 fix: if the backstory also has high-tier spawnCategories
            // (Spacefaring/Industrial), don't classify as lowborn purely because
            // one of its categories is in LowbornCategories. This handles cases
            // like UB_VBE_ClonedHeir (spawnCategories=Outlander|Pirate|Offworld|
            // ImperialFighter, title="Cloned heir") which should NOT be lowborn.
            HashSet<World> tiers = CategoryTiers(bs);
            bool hasHighTier = tiers.Contains(World.Spacefaring) || tiers.Contains(World.Industrial);
            bool hasLowTier = false;
            if (bs.spawnCategories != null)
            {
                for (int i = 0; i < bs.spawnCategories.Count; i++)
                {
                    if (LowbornCategories.Contains(bs.spawnCategories[i]))
                    {
                        hasLowTier = true;
                        break;
                    }
                }
            }
            // If high-tier present, require keyword confirmation for lowborn
            if (hasHighTier)
            {
                return hasLowTier && AnyKw(Text(bs), LowbornKeywords);
            }
            return hasLowTier || AnyKw(Text(bs), LowbornKeywords);
        }
    }
}
