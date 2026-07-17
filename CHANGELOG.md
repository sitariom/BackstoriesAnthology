# Changelog — Backstories Anthology

## [1.2.1] — 2026-07-17

### Fixed
- **CRITICAL**: Double `PatchAll()` registration eliminated — every Harmony patch was applying twice, causing doubled elderhood skill bonuses, incorrect need-decay restoration (43.75% vs 75%), duplicated CharacterCard UI, and potential item duplication. Consolidated into single `UnifiedBackstoriesPatcher`.
- **CRITICAL**: ZCB backstory validation system implemented — 24+ fields (tech level, traits, skills, records, body parts, parents, etc.) previously declared but never enforced are now validated during pawn generation via `ZCBackstoryValidator`.
- **CRITICAL**: Elderhood skill bonuses no longer double-counted — `ApplyElderhoodEffects` removed redundant skill gain application (was stacking on top of `FinalLevelOfSkill_Patch`).
- **CRITICAL**: `UB_DefOf.SoldMyLovedOne` now resolves dynamically — previously caused crash when Ideology DLC is not loaded.
- Medieval backstories: 211 skillGains entries fixed from invalid format (`<li><key>X</key><value>N</value></li>`) to RimWorld-standard shorthand. 270+ old text tokens (`[PAWN_possessive]` etc.) converted to `{PAWN_gender ? ...}` syntax.
- Saito Purgeworld: 12 backstories' forcedTraits fixed from invalid format; skill values reduced from +17 to +6; workDisables reduced from 10-16 to 3-6 per theme.
- 79 ZCB backstories: English translation file created (238 entries). Previously appeared as raw defNames.
- SettingsCategory suppression returns `string.Empty` instead of `null` (prevented potential NRE in mod settings UI).
- Def mutation in `ApplyZCBEffects` eliminated — work tag disables now applied per-pawn via existing `DisabledWorkTagsBackstoryAndTraits` patch, not by mutating global Def singleton.
- Elderhood backstory lists now cached (avoid per-call LINQ allocations).
- Grammar fixes in elderhood translations: "losts"→"lost", "has always was"→"was always", broken `{PAWN_gender ? {PAWN_gender ? ...} : ...}` nesting, hardcoded "He"/"elderly man" → gender tokens.
- `HarmonyPriority(Priority.High)` set on ZCB validator so childhood validates before BackstoryPairing checks the pair.
- `GetOrCreateElderhoodComp` now has null-guard + try-catch for reflection safety.
- CharacterCard elderhood header now uses translatable key (`UB.ElderhoodHeader`).
- CodeGraph indexing enabled for future semantic code analysis.

## [1.0.0] — 2026-07-12

### Added
- Initial release — unified compilation of 1,273 backstories from 11 community mods
- All mods merged into single, self-contained package (no original mods required)
- All defNames prefixed with `UB_` to prevent conflicts
- Full English translations for all 1,273 backstories
- RimWorld 1.6 only (1.5 support removed)
- 4 PlaceDefs from Seal's Collection for procedural story generation

### Integrated Mods
- Cybranian Backstories+ (401 backstories) — DimonSever000
- Before the Crash (206 backstories) — Ostrich-Hungry
- Vanilla Backstories Expanded (174 backstories) — Oskar Potocki, Legodude17
- Seal's Backstory Collection v1.0-v1.3 (137 backstories) — SneezingSeal
- Medieval Backstories (107 backstories) — Shenanigans
- More Backstories (58 backstories) — Ravinglegend
- SNS Backstories (56 backstories) — Kurzaen
- Tribal Backstories (55 backstories) — Shenanigans
- Elderhood Backstories (42 backstories) — DimonSever000
- Apocalyptic Backstories (25 backstories) — Lovely
- Saito's Backstories (12 curated) — Zaljerem / Saito Yui

### Integrated Systems
- Reasonable Moods & Capable Backstories (Legator) — 8 patches applied directly
- Elderhood Backstory DLL + Harmony patches included

### Fixes Applied During Merge
- 8 non-backstory template defs removed from Saito
- All linked backstory references updated to `UB_` prefix
- 114 missing XML declarations added
- 79 missing English `.description` translations added
- Backstory tags balanced (no orphaned open/close tags)
- Medieval AlienRace.AlienBackstoryDef converted to standard BackstoryDef
- All workDisables from RMCB patches applied directly to defs
- Purgeworld file reconstructed from original source after accidental deletion
