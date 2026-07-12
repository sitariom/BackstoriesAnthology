using RimWorld;
using Verse;

namespace UnifiedBackstories
{
    [DefOf]
    public static class UB_DefOf
    {
        public static ThoughtDef RMCB_SurvivedHardshipTogether;
        public static ThoughtDef RMCB_HeardAboutBetrayal;
        public static ThoughtDef SoldMyLovedOne;

        static UB_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(UB_DefOf));
        }
    }
}
