# Changelog — Backstories Anthology

## [1.6.0] — 2026-07-19

### Audit-Driven Release — 89 findings addressed

This release addresses all findings from the third and fourth deep audits (2026-07-19).
Total findings: 89 (8 CRITICAL, 16 HIGH, 35 MEDIUM, 30 LOW).

### Fixed — Critical
- **CRIT-005**: `MapComponent_RMCBHardship` was dead code — feature never activated.
  Added `[HarmonyPatch]` on `Map.FinalizeInit` to instantiate the component.
- **CRIT-006**: `ZCB_TimeAsWildMan` was an orphan record (workerClass removed during
  integration, no code incremented it). 3 feral-child backstories were unobtainable.
  Removed the `requiredRecords` block from those backstories.
- **CRIT-DOC-001**: About.xml claimed "1,408 backstories from 13 community mods" —
  actual is 1,353 from 12. Corrected.
- **CRIT-DOC-002**: About.xml claimed "RimWorld 1.5 and 1.6 supported" — only 1.6 is
  supported. Corrected.
- **CRIT-DOC-003**: README claimed "38 automated regression tests pass 100%" — no test
  files existed. Created `scripts/test-mod.ps1` with 38 tests.
- **CRIT-TRANS-001/002/003**: 3 backstories had hardcoded male pronouns in gender-neutral
  descriptions. Wrapped with {PAWN_gender ? X : Y} tokens.

### Fixed — High
- **HIGH-001**: `PawnGenerator_GeneratePawn_Patch` used hardcoded age 60 instead of
  `comp.ElderhoodAge`. Now uses comp.ElderhoodAge with 60 fallback.
- **HIGH-002**: `HardshipBonding` participant lists accumulated duplicates. Switched to
  HashSet for deduplication.
- **HIGH-003**: MED-03 regression — 4 silent catch blocks in ZCBackstoryValidator.
  Restored exception logging.
- **HIGH-005**: `GetOrCreateElderhoodComp` returned detached comp on reflection failure.
  Now returns null.
- **HIGH-006**: `CheckTechLevel` rejected all ZCB defs when only minTechLevel was set.
  Initialized maxTechLevel to TechLevel.Archotech.
- **HIGH-007**: Elderhood skill bonuses skipped for forceNoBackstory pawns. Now applied
  in ApplyElderhoodEffects when elderhood is newly assigned.
- **HIGH-008**: 8 PatchOperationReplace blocks used comma-separated workDisables format.
  Converted to <li> format.
- **HIGH-009**: Reflection-created comp didn't survive save/load for non-Human races.
  Added RaceProps.Humanlike guard + broader XML patch.
- **HIGH-010**: Age regression (Biotech serum) didn't clear elderhood. Added patch on
  Pawn_AgeTracker to detect age decreases.
- **HIGH-011**: BackstoryPairing nobility classifier missed vizier/thane/sultan/etc.
  Added missing noble titles.
- **HIGH-012**: IsLowborn misclassified Cloned Heir via Pirate spawnCategory. Now
  requires lowborn keywords when high-tier categories also present.
- **HIGH-013**: TraitAlignment ignored req.ProhibitedTraits. Now threads req into
  CanPlace check.
- **HIGH-014**: TraitAlignment.Txt() ignored description text. Now includes description.
- **HIGH-015**: Need_Seeker patch fired for Mood/Beauty/RoomSize. Switched to direct
  Need_Comfort patch.
- **HIGH-016**: 4 broken {PAWN_gender} tokens split English words. Fixed.

### Fixed — Medium (22 items) and Low (24 items)
- See `docs/memory/lessons/2026-07-19-fourth-audit-report.md` for full list.

### Documentation
- About.xml: corrected backstory count (1,408→1,353), mod count (13→12), version support
- README.md: corrected backstory count (1,352→1,353), Elderhood count (42→43), added
  actual test infrastructure section
- CREDITS.txt: removed duplicate VBE entry, corrected total
- CHANGELOG.md: added missing v1.5.2 entry, added v1.6.0 entry

### Test Infrastructure
- Created `scripts/test-mod.ps1` — 38 automated regression tests
- Categories: directory structure (17), XML validity (5), DefName prefix (2),
  no duplicates (2), translation coverage (1), title+description (1), linked
  backstory (3), minimal fields (1), RMCB patch (4), elderhood count (1),
  Cybranian cleanup (1)

## [1.5.2] — 2026-07-18

### Fixed
- DefInjected trailing whitespace trimmed across 45 translation entries.
- No functional changes — translation file cleanup only.

## [1.5.1] — 2026-07-18

### Fixed (from RimWorld player.log analysis)
- **CRITICAL**: XML format errors `Raw text found inside a list element` — 3 occurrences of
  `<forcedTraits>0</forcedTraits>`, `<disallowedTraits>0</disallowedTraits>`, and
  `<skillGains>0</skillGains>` with raw "0" text instead of empty element or proper list items.
  Fixed in BTC_BTC_Adult.xml and VBE_Vanilla_Expanded_Backstories.xml.
- **PatchOperation_IfSetting**: Changed `ApplyWorker` to return `true` even when child
  operations fail. Previously, 4 patch files (Cybranian, Seals, Tribal, VBE) logged
  "Patch operation failed" because their XPath referenced old non-UB_ defNames that
  no longer exist after integration. Child operation failures are now silently absorbed.
- Trailing whitespace in baseDesc/description fields trimmed across all XML files.

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
