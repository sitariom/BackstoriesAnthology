using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    [DefOf]
    public static class UB_DefOf
    {
        public static ThoughtDef RMCB_SurvivedHardshipTogether;
        public static ThoughtDef RMCB_HeardAboutBetrayal;

        static UB_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(UB_DefOf));
        }

        /// <summary>
        /// Resolves SoldMyLovedOne dynamically — it's from the Ideology DLC
        /// and may not be present. Use this instead of a static DefOf field
        /// to avoid crashing when Ideology is not loaded.
        /// </summary>
        public static ThoughtDef SoldMyLovedOneResolved()
        {
            return DefDatabase<ThoughtDef>.GetNamedSilentFail("SoldMyLovedOne");
        }
    }
}
