# Changelog — Backstories Anthology

## [1.5.0] — 2026-07-18

### Fixed
- **CRITICAL**: Medieval forcedTraits in OLD LI format (`<li><defName>Nerves</defName><degree>-1</degree></li>`) converted to DIRECT format (`<Nerves>-1</Nerves>`) — 12 traits across 2 files were NOT being applied to pawns since RimWorld 1.6's BackstoryTrait parser requires DIRECT format
- **CRITICAL**: About.xml modVersion corrected from 1.0.0 to 1.5.0 (was showing wrong version in-game)
- **CRITICAL**: Simples LI forcedTraits (`<li>Kind</li>`) converted to DIRECT format (`<Kind>0</Kind>`) in Medieval files — 4 entries fixed
- Dev artifacts removed from git tracking (`.codegraph/`, `.pi/`, `*.bak`, `obj/`)
- CHANGELOG.md updated with complete version history v1.2.2 through v1.5.0

## [1.4.0] — 2026-07-18

### Fixed — Complete ZCB Field Integration (matches original ZCB v1.0.0)

All 18 ZCB fields now behave identically to the original decompiled ZCB.dll (3000770956):

- **bodyPartsMissing / bodyPartsReplaced**: Now uses `GetMissingPartsCommonAncestors()` and `GeneUtility.AddedAndImplantedPartsWithXenogenesCount()` instead of manual `Hediff_MissingPart`/`Hediff_AddedPart` counting
- **father / mother**: Now uses `ParentRelationUtility.GetFather/Mother()` with `FamilyStatusFlags` bitwise matching instead of manual `DirectPawnRelation` scanning
- **requiredTraits / disallowedTraits**: Changed from `List<string>` (defName only) to `List<BackstoryTrait>` (defName + degree via `HasTrait(def, degree)`)
- **requiredRecords / requiredSkills**: Values now scaled by `childAgingRate / 4f` matching original ZCB behavior
- **passionGains**: Changed from ABSOLUTE (overwrite) to ADDITIVE (sum + clamp 0-2)
- **commonality**: Changed from `int` to `float` with `Rand.Value * totalWeight` matching `RandomElementByWeight`

### Type Changes (ZCBackstoryDef)
- `commonality`: int → float
- `minTechLevel/maxTechLevel`: string → TechLevel enum
- `colonySize`, `bodyPartsMissing`, `bodyPartsReplaced`: string → IntRange
- `father`, `mother`: string → FamilyStatusFlags (Flags enum)
- `requiredTraits`: List<string> → List<BackstoryTrait>
- `disallowedTraits`: removed `new` shadow (inherited from BackstoryDef)

### XML
- 85 files converted from UTF-16 to UTF-8
- 7 ZCB files: LI → DIRECT format for requiredTraits/disallowedTraits
- `FamilyStatusFlags` enum added for parent checks

## [1.3.2] — 2026-07-18

### Added
- `SelectByCommonality()` now used during ZCB re-rolls (commonality-weighted selection)
- `ValidPoolFor()` method for filtered commonality pool
- `CheckDevelopmentalStage()` validation (1=child, 2=adult)

### Changed
- ZCB re-roll logic: tries commonality-weighted selection before falling back to FillBackstorySlotShuffled
- Version string updated

## [1.3.1] — 2026-07-18

### Removed
- Dead code `StartingPawnUtility_GeneratePossessions_Patch` (21 lines, never executed)

### Changed
- Version string updated to v1.3.1

## [1.3.0] — 2026-07-18

### Fixed
- `bodyTypeGlobal` → `bodyTypeMale` + `bodyTypeFemale` in 2 XML files
- Version string updated to v1.3.0

## [1.2.9] — 2026-07-17

### Fixed
- Standardized ALL forcedTraits/disallowedTraits to DIRECT format across 37 files
- Converted 15 empty `<TraitName />` entries and 16 MayRequire entries to `<TraitName>0</TraitName>`
- Fixed Medieval_Childhood disallowedTraits from LI to DIRECT

## [1.2.8] — 2026-07-17

### Fixed
- **CRITICAL**: Restored 112 XML files corrupted in v1.2.7 from git v1.2.6
- Fixed Elderhood forcedTraits to DIRECT format `<Kind>0</Kind>`

## [1.2.7] — 2026-07-17

### Changed
- Converted 22 files from forcedTraits DIRECT format to LI format

### Known Issues
- INTRODUCED XML CORRUPTION in 112 files — reverted in v1.2.8

## [1.2.6] — 2026-07-17

### Fixed
- Removed Elderhood possessions entirely (caused NRE in PossessionThingDefCountClass)
- Fixed trailing whitespace in baseDesc across all XML files via Trim()
- Fixed Medieval disallowedTraits from `<defName>Kind</defName>` to simple format

## [1.2.5] — 2026-07-17

### Changed
- Reverted forcedTraits/disallowedTraits to SIMPLE `<li>` format for ZCB files (List<string\>)

## [1.2.4] — 2026-07-17

### Fixed
- Fixed Elderhood possessions format

## [1.2.3] — 2026-07-17

### Fixed
- Converted 209 ZCB skillGain entries from `<li><skill>X</skill><amount>Y</amount></li>` to `<X>Y</X>` format
- Changed ZCB forcedTraits from simple `<li>X</li>` to complex format (reverted in v1.2.5)

## [1.2.2] — 2026-07-17

### Removed
- 232 obsolete Medieval XML fields (linkedBackstory, relationSettings, maleCommonality, femaleCommonality, forcedItems)

### Changed
- Rounded 7 ZCB commonality floats to ints

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
