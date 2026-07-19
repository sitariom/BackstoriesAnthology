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
        /// MED-007 fix: cache the resolved ThoughtDef after first lookup to
        /// avoid DefDatabase.GetNamedSilentFail on every TryGainMemory call
        /// (which fires frequently for all thought types).
        /// </summary>
        private static ThoughtDef _cachedSoldMyLovedOne;
        private static bool _soldMyLovedOneResolved;

        public static ThoughtDef SoldMyLovedOneResolved()
        {
            if (_soldMyLovedOneResolved)
                return _cachedSoldMyLovedOne;
            _cachedSoldMyLovedOne = DefDatabase<ThoughtDef>.GetNamedSilentFail("SoldMyLovedOne");
            _soldMyLovedOneResolved = true;
            return _cachedSoldMyLovedOne;
        }
    }
}
