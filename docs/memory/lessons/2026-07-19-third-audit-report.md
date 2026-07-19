---
type: lesson
title: Third Audit Report — 27 findings across code, XML, and documentation
description: Comprehensive audit using 3 parallel subagents (QA, Specs, Developer) — found 1 CRITICAL dead feature, 7 HIGH silent-corruption bugs, 13 MEDIUM, 6 LOW
timestamp: 2026-07-19
tags: [audit, bug-report, harmony, rimworld, zcb, elderhood, documentation]
---

# Third Audit Report — UnifiedBackstories (2026-07-19)

## Methodology

Third deep audit of the project, using the full agency infrastructure:
- **CodeGraph MCP** for symbol/call-path analysis (`.codegraph/` index present)
- **3 parallel subagents**: `qa-engineer` (code review), `specs-planner` (XML data audit), `developer` (fix verification + build state)
- **Pipeline engram → CodeGraph → Read** for context retrieval
- Verified all prior fixes (CRIT-01 to CRIT-04, HIGH-01, MED-01 to MED-04) still present

## Summary

| Severity | Count | Categories |
|----------|-------|------------|
| CRITICAL | 4 | Dead feature, false docs (×3) |
| HIGH | 8 | Silent gameplay corruption, performance, NullRef traps |
| MEDIUM | 13 | Logic edge cases, performance, schema |
| LOW | 6 | Code quality, redundant logic |
| **TOTAL** | **31** | (4 are documentation; 27 are code/XML) |

## Prior Fix Verification

| Fix | Status | Notes |
|-----|--------|-------|
| CRIT-03 (single Patcher) | ⚠️ PARTIAL | PatchAll dedup ✅; 2nd `[StaticConstructorOnStartup]` (UB_ModCompat) is benign but technically violates strict criterion |
| CRIT-04 (no Def mutation) | ✅ PASS | `ZCBackstoryValidator.cs:593-620` mutates pawn only |
| HIGH-01 (SettingsCategory) | ✅ PASS | Returns `"Unified Backstories"` |
| MED-01 (cached lists) | ✅ PASS | `_cachedElderhoods` / `_cachedAllElderhoods` lazy-rebuilt |
| MED-03 (catch logs) | ❌ **REGRESSED** | 4 silent `catch { ... }` blocks in `ZCBackstoryValidator.cs` — see HIGH-003 |
| MED-04 (CheckBodyParts) | ✅ PASS | Returns `true` when default ranges cover everything |

## Ground Truth Backstory Count

**Actual: 1,353** (1,274 standard `BackstoryDef` + 79 `ZCBackstoryDef`)

| Source | Claim | Actual | Match |
|--------|-------|--------|-------|
| README.md | 1,352 | 1,353 | ❌ off by 1 (Elderhood is 43, not 42) |
| About.xml | 1,408 | 1,353 | ❌ inflated by 55 |
| CREDITS.txt | 1,352 | 1,353 | ❌ off by 1 |

---

## CRITICAL Findings

### CRIT-005: `MapComponent_RMCBHardship` is dead code — feature never activates
- **File**: `Source/UnifiedBackstories/HardshipBonding.cs:15-231`
- **Category**: Logic — dead feature
- **Description**: `MapComponent_RMCBHardship` is defined as a `MapComponent` subclass but nothing in the entire codebase ever calls `map.GetComponent<MapComponent_RMCBHardship>()` nor adds it to `map.components`. RimWorld does NOT auto-discover MapComponent subclasses via reflection — they must be explicitly requested or added via XML def. No XML def, no StaticConstructorOnStartup registration, no Harmony patch injects it. The `hardshipBonding` settings toggle reads as if the feature works, but `MapComponentTick()` never fires on any map.
- **Impact**: The entire "shared-hardship bonding" feature (opinion memories from raids/fires/disease) silently does nothing. Players see the settings checkbox but no in-game effect. No error is logged.
- **Fix**: Add a `[HarmonyPatch]` postfix on `Map.FinalizeInit` (or `LongEventHandler.ExecuteWhenFinishedLoading`) that calls `map.GetComponent<MapComponent_RMCBHardship>()` to force lazy instantiation on every map; OR add a `MapComponentDef` XML def referencing the class.

### CRIT-DOC-001: About.xml backstory count is wrong (claims 1,408; actual 1,353)
- **File**: `About/About.xml:7`
- **Category**: Documentation
- **Description**: About.xml description says "1,408 backstories from 13 community mods". Actual count is 1,353 from 12 unique mods. The number 1,408 is not derivable from any source-mod sum (sum of all listed mods including ZCB = 1,452; excluding ZCB = 1,373). The "13 mods" count is also wrong — the SOURCE MODS list contains 12 entries.
- **Impact**: Users see inflated backstory count in mod description. Workshop listing shows incorrect numbers.
- **Fix**: Update About.xml description to "1,353 backstories from 12 community mods".

### CRIT-DOC-002: About.xml claims "RimWorld 1.5 and 1.6 supported" — only 1.6 supported
- **File**: `About/About.xml:36` / `LoadFolders.xml:3-6`
- **Category**: Documentation
- **Description**: Description says "RimWorld 1.5 and 1.6 supported with separate load folders" but:
  - `<supportedVersions>` contains only `<li>1.6</li>`
  - `LoadFolders.xml` contains only `<v1.6>` entry
  - No `1.5/` folder exists in the mod
- **Impact**: Users on RimWorld 1.5 will subscribe expecting it to work; mod will fail to load. Workshop filtering may show mod to 1.5 users incorrectly.
- **Fix**: Either (a) remove "1.5" from description, OR (b) create `1.5/` folder with appropriate content and add `<v1.5>` to LoadFolders.xml and `<li>1.5</li>` to supportedVersions.

### CRIT-DOC-003: README claims "38 automated regression tests pass 100%" — zero test files exist
- **File**: `README.md:74, 219-234`
- **Category**: Documentation — false claim
- **Description**: README's "🧪 Testing" section claims 38 automated regression tests with detailed breakdown table. Actual state: 0 test files, 0 scripts, 0 test runners, 0 CI configuration exist in the repository. Git history shows no test files ever added. Even the documented test-table row sums (34) don't match the stated total (38).
- **Impact**: Misleads users and contributors into thinking the mod is tested. False quality assurance claim.
- **Fix**: Either (a) implement the 38 tests as claimed, OR (b) remove the "🧪 Testing" section from README entirely.

---

## HIGH Findings

### HIGH-001: `PawnGenerator_GeneratePawn_Patch` uses hardcoded age 60 instead of `comp.ElderhoodAge`
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:369`
- **Category**: Logic
- **Description**: The age gate `if (__result.ageTracker.AgeBiologicalYears < 60) return;` hardcodes 60, but the comp (fetched on line 371 AFTER the check) exposes `ElderhoodAge` from `CompProperties_ElderhoodBackstory.elderhoodAge` which is XML-configurable (`Patches.xml:10` sets `<elderhoodAge>60</elderhoodAge>`, but a modder/user could change it). The sibling path `FillBackstorySlotShuffled` (line 214) correctly uses `comp.ElderhoodAge`. If a user sets `elderhoodAge=80` via XML, this patch still processes pawns aged 60-79 and assigns elderhood, bypassing the configured threshold.
- **Impact**: Any non-default `elderhoodAge` setting is partially ignored. Pawns get elderhood at the hardcoded age instead of the configured age when generated via the `GeneratePawn` path (the primary path for new pawns).
- **Fix**: Fetch comp first, then check `__result.ageTracker.AgeBiologicalYears < comp.ElderhoodAge`. If comp is null, fall back to 60.

### HIGH-002: `HardshipBonding` participant lists accumulate duplicates every tick interval
- **File**: `Source/UnifiedBackstories/HardshipBonding.cs:63-69, 109-115, 138-141`
- **Category**: Logic — duplicate accumulation
- **Description**: `TickThreat` (lines 63-69) does `foreach (Pawn p in FreeColonistsSpawned) { if (IsFighting(p,tick)) combatParticipants.Add(p); }` every 250 ticks while a threat is active. A pawn fighting across 10 check intervals gets added 10 times. Same for `fireParticipants` (lines 109-115, `BeatFire` job) and `outbreakParticipants` (lines 138-141, sick pawns added every interval). `GrantBonds` (lines 191-201) then iterates the duplicated list, calling `TryGainMemory` for each (i,j) pair including duplicates — creating stacked social memories.
- **Impact**: Colonists surviving a long raid (20+ check intervals) get 20 duplicate "survived battle" opinion memories toward each other, inflating the relationship boost far beyond intended. Stacks subject to per-def stack limits but still massively over-applied.
- **Note**: This bug is currently masked by CRIT-005 (feature never activates), but if CRIT-005 is fixed without fixing this, the bonding effect will be severely broken.
- **Fix**: Use `if (!combatParticipants.Contains(p)) combatParticipants.Add(p);` or switch to `HashSet<Pawn>` for participants.

### HIGH-003: MED-03 regression — 4 silent `catch` blocks in `ZCBackstoryValidator` swallow exceptions
- **File**: `Source/UnifiedBackstories/ZCBackstoryValidator.cs:78, 106, 230, 302`
- **Category**: Logic — silent exception swallow
- **Description**: `CheckTechLevel` (line 78), `CheckColonySize` (line 106), `CheckRecords` (line 230), and `CheckRequiredSkills` (line 302) all use bare `catch { return true; }` or `catch { agingScale = 0.25f; }` with no `Log.Warning`/`Log.Error`. This regresses the MED-03 fix which required catch blocks to log exceptions. Only `ElderhoodSystem.cs:193` logs (the one compliant catch). Line 78 has a brief justification comment; lines 106 and 302 have NO comment at all; line 230 has only a terse value comment.
- **Impact**: When `Faction.OfPlayer`, `PawnsFinder`, or `Find.Storyteller` throws (e.g., during world gen, mod load order issues, or 1.7 API changes), the validator silently returns true/false with no diagnostic. Bugs become untraceable.
- **Fix**: Add `Log.Warning("[UB] CheckXxx deferred: " + ex.Message);` inside each catch, matching the `ElderhoodSystem.cs:193` pattern. Use `catch (Exception ex)` to capture the exception object.

### HIGH-004: `Pawn_StoryTracker_DisabledWorkTags_Patch` re-parses ZCB strings via `Enum.TryParse` on every getter call (hot path)
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:770-777`
- **Category**: Performance
- **Description**: The Postfix runs on every access to `get_DisabledWorkTagsBackstoryAndTraits` — a property getter called by work assignment, UI rendering, main tab updates, etc. (dozens of times per tick per pawn). For ZCB childhoods, it iterates `zcb.disablingWorkTags` (List<string>) and calls `Enum.TryParse` on each string every single call. The elderhood branch (line 763) uses the pre-parsed `workDisablers` WorkTags enum so it's fine, but the ZCB branch re-parses strings repeatedly.
- **Impact**: Significant CPU overhead on colonies with many ZCB-childhood pawns. `Enum.TryParse` is reflection-heavy. With 20 colonists and the getter called ~10x/sec each, that's 200 `Enum.TryParse` calls/sec per disabled tag string.
- **Fix**: Cache the parsed `WorkTags` on the `ZCBackstoryDef` (compute once in a lazy property or at def load) or cache a per-pawn resolved `WorkTags` value on the comp.

### HIGH-005: `GetOrCreateElderhoodComp` returns a detached comp when reflection fails
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:169-198`
- **Category**: NullRef / data loss
- **Description**: If the reflection lookup of `ThingWithComps.comps` field fails (`compsField == null`) or the `IList` cast fails, the method catches the exception, logs a warning, but still returns the manually-created comp whose parent was set but was never added to `pawn.comps`. The caller (e.g. `CharacterCardUtility_DoLeftSection_Patch:456`, `PawnGenerator_GeneratePawn_Patch:371`) then uses this detached comp — calling `SetElderhood` on it has no effect on the pawn's saved state (the comp isn't persisted), and the next `GetComp<CompElderhoodBackstory>()` call returns null, causing `GetOrCreateElderhoodComp` to create ANOTHER detached comp.
- **Impact**: Silent data loss — elderhood appears to assign but is never saved with the pawn. On reload, the pawn has no elderhood. Also creates orphaned comp instances (memory leak). The UI would show inconsistent state.
- **Fix**: Return `null` when the `compsField` lookup or `IList` cast fails, instead of returning the detached comp. Callers already null-check the return value.

### HIGH-006: `CheckTechLevel` rejects all ZCB defs when only `minTechLevel` is set
- **File**: `Source/UnifiedBackstories/ZCBackstoryValidator.cs:36, 68-90`
- **Category**: Logic
- **Description**: `ZCBackstoryDef` declares `public TechLevel maxTechLevel;` (line 36) with no initializer, defaulting to `TechLevel.Undefined` (value 0). The guard on line 70 only short-circuits when BOTH min and max are `<= Undefined`. If a ZCB def sets only `<minTechLevel>Medieval</minTechLevel>` (3) and leaves `maxTechLevel` unset (0), the check proceeds: `if (playerTech < 3 || playerTech > 0)` — for an Industrial (4) colony, `4 > 0` is true, so the def is rejected. Setting only a minimum effectively rejects everything above `Undefined`.
- **Impact**: Any ZCB backstory that sets `minTechLevel` without `maxTechLevel` can never spawn. The def is silently filtered out of the pool, reducing ZCB variety. This is a common XML authoring pattern ("at least Medieval, no upper bound") that breaks the entire ZCB feature for those defs.
- **Fix**: Initialize `public TechLevel maxTechLevel = TechLevel.Archotech;` (the max enum value), or treat `maxTechLevel == TechLevel.Undefined` as "no upper bound" in the comparison.

### HIGH-007: Elderhood skill bonuses skipped for pawns whose elderhood is assigned in `GeneratePawn` postfix
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:361-417, 727-744`
- **Category**: Logic — silent gameplay corruption
- **Description**: `PawnGenerator_FinalLevelOfSkill_Patch` (line 727) applies elderhood skillGains only if `comp.HasElderhood` is true at the time `FinalLevelOfSkill` fires — which is DURING `GeneratePawn`, BEFORE the `GeneratePawn` postfix. For pawns where elderhood is assigned inside the `GeneratePawn` postfix (line 379, e.g. when `GiveShuffledBioTo`/`TryGiveSolidBioTo` didn't fire because `forceNoBackstory` was true, or `FillBackstorySlotShuffled` returned early), `FinalLevelOfSkill` already fired with `HasElderhood=false` and added no bonus. `ApplyElderhoodEffects` (line 388-417) explicitly skips skill bonuses per the comment on line 407-409 ("Skill bonuses are applied by `PawnGenerator_FinalLevelOfSkill_Patch`. Do NOT apply them here"). Result: skill bonus is permanently missed.
- **Impact**: Pawns generated with `forceNoBackstory=true` (common for raiders, some quest pawns) who are 60+ get elderhood traits and body type but no skill bonuses — a silent gameplay corruption. The elderhood is only half-applied.
- **Fix**: In `ApplyElderhoodEffects`, when elderhood was just assigned (track a flag), manually apply skill bonuses by iterating `eb.skillGains` and adjusting `pawn.skills.GetSkill(sg.skill).Level`, or re-call `FinalLevelOfSkill` for each skill.

### HIGH-008: 8 `PatchOperationReplace` blocks in `Backstories_Solid.xml` use comma-separated `workDisables` text format
- **File**: `1.6/Patches/Backstories_Solid.xml` — 8 occurrences
- **Category**: XML — patch silently fails
- **Description**: 8 `PatchOperationReplace` blocks use `<value><workDisables>Social, Firefighting</workDisables></value>`. RimWorld's `List<WorkTags>` parser expects `<li>` children, not a comma-separated text node. These patches likely silently fail (per v1.5.1 changelog: "Child operation failures are now silently absorbed" by `PatchOperation_IfSetting`). The intended `workDisables` replacement does not apply.
- **Impact**: 8 RMCB balance patches do not actually modify the target backstories — the rebalancing intent is lost. Players get the original (un-rebalanced) workDisables.
- **Fix**: Convert comma-separated format to `<li>` format: `<workDisables><li>Social</li><li>Firefighting</li></workDisables>`.

---

## MEDIUM Findings

### MED-005: `CharacterCardUtility_DoLeftSection_Patch` uses hardcoded age 60 for UI gate
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:460`
- **Category**: Logic — inconsistency with config
- **Description**: `if (!hasElderhood && (pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYears < 60)) return;` hardcodes 60, inconsistent with `comp.ElderhoodAge` used by `FillBackstorySlotShuffled` (line 214). If `elderhoodAge` is configured to 80, the UI shows the elderhood section for pawns aged 60-79 who cannot yet have elderhood, displaying "No elderhood" / "Click to add" misleadingly.
- **Fix**: Resolve comp first and use `comp.ElderhoodAge` for the UI age gate.

### MED-006: `ZCB_BackstoryValidator_Patch` sets `pawn.story.Childhood` without `Notify_SkillDisablesChanged`
- **File**: `Source/UnifiedBackstories/ZCBackstoryValidator.cs:558`
- **Category**: Logic — stale UI
- **Description**: `pawn.story.Childhood = commonalityPick;` directly assigns the Childhood property but does not call `pawn.skills?.Notify_SkillDisablesChanged()` afterward. Compare with `ElderhoodHelper.FillBackstorySlotShuffled` (`ElderhoodSystem.cs:220`) and the clear/edit buttons in `CharacterCardUtility` (lines 501, 663) — all call `Notify_SkillDisablesChanged` after changing backstory state. The ZCB validator bypasses this, so the skill system doesn't re-evaluate disabled work tags / skill disables until the next natural trigger.
- **Fix**: Add `pawn.skills?.Notify_SkillDisablesChanged();` after line 558 (and after line 565 for the fallback path).

### MED-007: `BetrayalWitnesses` resolves `SoldMyLovedOne` via `DefDatabase` on every `TryGainMemory` call
- **File**: `Source/UnifiedBackstories/BetrayalWitnesses.cs:27` / `UB_DefOf.cs:22-25`
- **Category**: Performance
- **Description**: `ThoughtDef soldMyLovedOne = UB_DefOf.SoldMyLovedOneResolved();` calls `DefDatabase<ThoughtDef>.GetNamedSilentFail("SoldMyLovedOne")` on every Postfix invocation. `MemoryThoughtHandler.TryGainMemory` fires frequently for many thought types, and this postfix runs for ALL of them (the early-return on line 28 only fires after the def lookup). The lookup does a string-keyed dictionary probe + null check every call.
- **Fix**: Cache the resolved `ThoughtDef` in a static field initialized once (lazy via static ctor or first-access), since `DefDatabase` is stable after load.

### MED-008: `ZCBackstoryValidator.ValidPoolFor` scans all 1,408 `BackstoryDef`s per pawn generation
- **File**: `Source/UnifiedBackstories/ZCBackstoryValidator.cs:480-490`
- **Category**: Performance
- **Description**: `ValidPoolFor` iterates `DefDatabase<BackstoryDef>.AllDefsListForReading` (1,408 defs) and runs `IsValidFor` on each `ZCBackstoryDef` found. During world generation dozens of pawns are generated sequentially, each paying the full O(n) scan with `IsValidFor`'s multi-check validation.
- **Fix**: Cache the list of `ZCBackstoryDef` references once (static field populated lazily after def load), then filter that smaller list per pawn.

### MED-009: `BackstoryPlausibility.Text` has no null-guard — NRE if called with null def
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:151-154`
- **Category**: NullRef — latent
- **Description**: `private static string Text(BackstoryDef bs) { return ((bs.defName ?? "") + " " + (bs.title ?? "")).ToLowerInvariant(); }` dereferences `bs.defName` without checking `bs == null`. The sole caller path is currently guarded by `BackstoryPairing_Patch.Postfix` line 34, so null never reaches `Text` today. But `Plausible` is `public static` — any future caller (or a regression in the guard) would NRE.
- **Fix**: Add `if (bs == null) return "";` at the top of `Text`, matching `TraitAlignment.Txt`.

### MED-010: `BackstoryPlausibility.CategoryTiers` allocates a new `HashSet` per call
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:168-182`
- **Category**: Performance
- **Description**: `CategoryTiers` creates `new HashSet<World>()` on every call. Each `Plausible()` check calls `IsLowTechPlanetbound` (1 call to `CategoryTiers`) and `IsSpacefaring` (1 call) — so 2 `HashSet` allocations per plausibility check. With `MaxRerolls=3`, that's up to 8 `HashSet` allocations per Adulthood slot fill. During world gen (hundreds of pawns), this is thousands of short-lived `HashSet` allocations.
- **Fix**: Replace `HashSet<World>` with a flags enum or pre-allocated reusable buffer.

### MED-011: `ApplyZCBEffects` applies `passionGains` even after `MaxRetries` exhausted on an invalid def
- **File**: `Source/UnifiedBackstories/ZCBackstoryValidator.cs:534-576`
- **Category**: Logic — silent gameplay corruption
- **Description**: The retry loop (line 534-571) exits after `MaxRetries=15` if no valid ZCB was found. The fallback block (line 573-576) then calls `ApplyZCBEffects(pawn, finalZcb)` on whatever ZCB def remains — even if `IsValidFor` returned false for it on every iteration. This "give up and accept" path still adds `passionGains` (additive, clamped) for a def that doesn't match the pawn.
- **Impact**: A pawn can end up with a ZCB childhood that violates its own requirements (wrong tech level, wrong body parts, wrong traits) AND receive the passion bonuses from that invalid def.
- **Fix**: After `MaxRetries`, either skip `ApplyZCBEffects` (accept the def for story purposes but don't apply effects) or fall back to a non-ZCB childhood via `PawnBioAndNameGenerator.FillBackstorySlotShuffled` without the ZCB cast.

### MED-012: `Pawn_AgeTracker_BirthdayBiological_Patch` doesn't update body type when aging into elderhood
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:329-338, 709-722`
- **Category**: Logic — visual inconsistency
- **Description**: When a pawn turns 60 (or `comp.ElderhoodAge`) via `BirthdayBiological`, the postfix calls `TryAssignElderhood` → `FillBackstorySlotShuffled` → `SetElderhood`. But none of these update `pawn.story.bodyType` to `MaleElderly`/`FemaleElderly`. The `PawnGenerator_GetBodyTypeFor_Patch` (line 709-722) only fires during generation, not on birthday. The `GeneratePawn` postfix's `ApplyElderhoodEffects` (line 411-415) sets body type but only runs at generation. So pawns who age into elderhood keep their old (Thin/Fat/Hulk/etc.) body type forever, while pawns generated at 60+ get the elderly body type.
- **Fix**: In `FillBackstorySlotShuffled` after `SetElderhood` (or in the `BirthdayBiological` postfix), apply the elderly body type the same way `ApplyElderhoodEffects` does.

### MED-013: `ZCBackstoryDef` custom list types lack `LoadDataFromXmlCustom` — DIRECT format parsing may fail
- **File**: `Source/UnifiedBackstories/ZCBackstoryDef.cs:32-53`
- **Category**: Logic — schema/parsing
- **Description**: The class doc (lines 25-31) states "Complex lists use `LoadDataFromXmlCustom`" and "requiredTraits/disallowedTraits use DIRECT format: `<TraitDefName>degree</TraitDefName>`; passionGains use DIRECT format: `<SkillDefName>degree</SkillDefName>`". But `ZCBackstoryDef` does not override `LoadDataFromXmlCustom` anywhere in the assembly (grep confirmed). The custom helper types (`ZCBRecordReq`, `ZCBRecordRatio`, `ZCBSkillReq`, `ZCBPassionGain`) have only public fields with no `[LoadAlias]` or custom parser. RimWorld's default `ParseHelper.FromString` does not handle DIRECT format (tag-name-as-defName, inner-text-as-degree) — it expects standard `<li><field>value</field></li>` structure.
- **Impact**: If the XML uses DIRECT format as documented, `requiredRecords`/`recordRatios`/`requiredSkills`/`passionGains` would silently parse as empty/null lists, making those ZCB requirements no-ops. ZCB backstories would spawn without their intended constraints. **Needs XML cross-verification** — the audit found ZCB XML uses standard `<li>` format for these fields, so the doc comment is misleading but parsing works.
- **Fix**: Either implement `LoadDataFromXmlCustom` on `ZCBackstoryDef` (and the helper types) to handle DIRECT format as documented, OR correct the doc comment and ensure the XML uses standard `<li><skill>X</skill><level>1</level></li>` format.

### MED-014: `GetOrCreateElderhoodComp` fallback ignores XML-configured `elderhoodAge`
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:177-180`
- **Category**: Logic — config bypass
- **Description**: When the comp isn't found on the pawn (XML patch failed), the fallback creates `new CompProperties_ElderhoodBackstory()` which uses the default `elderhoodAge = 60` (line 19). If the XML had configured a different `elderhoodAge` (e.g., 80), the fallback ignores it. The manually-created comp then enforces 60, inconsistent with the XML intent.
- **Fix**: Read the configured `elderhoodAge` from `DefDatabase<ThingDef>.GetNamed("Human")`'s `CompProperties_ElderhoodBackstory` if present, before falling back to the default.

### MED-015: Description field name inconsistency — 650 defs use legacy `<baseDesc>` instead of 1.6-standard `<description>`
- **Files**: 1.6/Defs/BackstoryDefs/* (650 of 1,353 defs, 48%)
- **Category**: Schema inconsistency
- **Description**: RimWorld 1.6 `BackstoryDef` uses `<description>`. 650 defs (48%) use the older `<baseDesc>` field name. If RimWorld 1.6 ignores `<baseDesc>` (since the field is named `description`), these defs have an empty in-def description; the displayed text comes from `DefInjected` `.description` translations instead. Works in practice but is a schema inconsistency. Distribution:
  - Uses `<description>` (703 defs, correct): Apocalyptic 25, Cybranian 401, Saito 12, Seal 137, BTC 128
  - Uses `<baseDesc>` (650 defs, legacy): Elderhood 43, Medieval 107, More_VB 58, SNS 56, Tribal 55, VBE 174, ZCB 79, BTC 78
  - Files using BOTH (mixed): `BTC_BTC_Adult.xml`, `BTC_BTC_Ancient.xml`, `BTC_BTC_Child.xml`, `BTC_BTC_Imperial.xml`, `BTC_BTC_Pirates.xml`
- **Fix**: Standardize all 650 to `<description>` (or confirm RimWorld 1.6 accepts both).

### MED-016: Invalid WorkTag "Commoner" in `UB_VBE_BloodKnight`
- **File**: `1.6/Defs/BackstoryDefs/VBE_Vanilla_Expanded_Backstories.xml:2406`
- **Category**: Schema — invalid enum value
- **Description**: `"Commoner"` is not a vanilla RimWorld 1.6 `WorkTags` enum value. Likely a Vanilla Expanded framework worktag; will be silently ignored by the vanilla `BackstoryDef` parser.
- **Fix**: Remove `"Commoner"` from `workDisables` or replace with a valid vanilla `WorkTags` value.

### MED-017: `spawnCategory` typo "Ourlander" (should be "Outlander")
- **Files**: `1.6/Defs/BackstoryDefs/SNS_AdultLawyer.xml:14`, `1.6/Defs/BackstoryDefs/SNS_ChildViewpad.xml:14`
- **Category**: Schema — typo
- **Description**: Two SNS defs use `"Ourlander"` instead of `"Outlander"`. These defs will not spawn in the intended Outlander category.
- **Fix**: Replace `"Ourlander"` with `"Outlander"` in both files.

---

## LOW Findings

### LOW-001: `PawnGenerator_GenerateTraits_ElderhoodPatch` has redundant `te.def` null check
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:309, 320`
- **Category**: Code quality
- **Description**: Line 309 checks `if (te == null || te.def == null) continue;` and line 320 then checks `if (te.def != null)` — but `te.def` was already verified non-null on line 309, so the second check is dead code.
- **Fix**: Remove the `if (te.def != null)` guard on line 320.

### LOW-002: `BackstoryDef_FullDescriptionFor_Patch` applies gender-token regex to ALL backstories, not just UB_ defs
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:247-255`
- **Category**: Harmony — latent intermod conflict
- **Description**: The postfix on `BackstoryDef.FullDescriptionFor` runs for every backstory def in the game, processing `{PAWN_gender ? X : Y}` tokens. The regex is specific enough that non-UB backstories are unlikely to use this exact syntax, but if another mod (or future RimWorld content) uses the same token syntax with different semantics, this patch would silently transform their text too.
- **Fix**: Consider gating on `__instance.defName.StartsWith("UB_") || __instance.spawnCategories?.Contains("Elderhood") == true` to limit the patch to UB-managed defs.

### LOW-003: Typo "suvivor" (should be "survivor") in `UB_ZCB_SoleSurvivor`
- **File**: `1.6/Defs/BackstoryDefs/ZCB_Misc_Backstories.xml:59-60`
- **Category**: Content typo
- **Description**: `<title>sole suvivor</title>` and `<titleShort>suvivor</titleShort>` — typo in both fields.
- **Fix**: Replace `"suvivor"` with `"survivor"`.

### LOW-004: `CREDITS.txt` duplicates Vanilla Backstories Expanded entry (#2 and #9)
- **File**: `CREDITS.txt`
- **Category**: Documentation
- **Description**: VBE appears twice in the credits table — entry #2 and entry #9 — same mod, same authors, same `packageId`. This is the source of the "13 mods" overcount in About.xml.
- **Fix**: Remove the duplicate VBE entry.

### LOW-005: CHANGELOG.md missing v1.5.2 entry
- **File**: `CHANGELOG.md`
- **Category**: Documentation
- **Description**: Latest changelog entry is `[1.5.1] — 2026-07-18`, but `About.xml` `modVersion` is `1.5.2`, source code logs `"v1.5.2 loaded"` (`ElderhoodSystem.cs:826`), and git log shows v1.5.2 commit (`adf4885 auto: v1.5.2 fix DefInjected trailing whitespace (45 entries)`). The v1.5.2 release is undocumented in CHANGELOG.
- **Fix**: Add `[1.5.2] — 2026-07-18` entry with the DefInjected trailing whitespace fix details.

### LOW-006: `.csproj` uses machine-specific hardcoded paths
- **File**: `Source/UnifiedBackstories/UnifiedBackstories.csproj`
- **Category**: Build portability
- **Description**: `<GameDir>C:\Program Files (x86)\Steam\steamapps\common\RimWorld</GameDir>` and `<HarmonyDir>...workshop\content\294100\2009463077\...` are hardcoded to this developer's machine. Build will fail on any other machine without env override.
- **Fix**: Acceptable for solo dev, but for open-source contributors, document the env vars needed or use `$(RIMWORLD_DIR)` fallback.

---

## PASS Items (verified clean)

- No duplicate defNames across all 112 XML files (1,353 unique defNames verified)
- All 1,353 defNames use `UB_` prefix — no orphan defNames from original mods
- All 1,353 defs have `.title` and `.description` translations
- All 26 Keyed translations present
- All `forcedTraits`/`disallowedTraits`/`requiredTraits` degrees within [-2, +2]
- Prior XML data fixes (Medieval skillGains, Saito Purgeworld, ZCB translations) all verified still fixed
- `Harmony` ID is unique and consistent: `"UnifiedBackstories.UnifiedBackstories"`
- Only 1 `PatchAll()` call in entire assembly
- DLL is up-to-date relative to source (62,464 bytes, 2026-07-18 02:42:14)
- Git tree is clean
- `bodyTypeGlobal` field eliminated (all use `bodyTypeMale`/`bodyTypeFemale`)
- 4 PlaceDefs confirmed present, Ideology-DLC-gated
- 10 ZCB `RecordDef`s confirmed present

## Recommendations (Priority Order)

1. **Fix CRIT-005** (dead `MapComponent_RMCBHardship`) — feature is sold in settings but doesn't work
2. **Fix HIGH-003** (MED-03 regression) — re-instate exception logging in all 4 bare catches
3. **Fix HIGH-006** (`CheckTechLevel` rejects valid defs) — likely filtering many ZCB defs out
4. **Fix HIGH-007** (Elderhood skill bonuses skipped for `forceNoBackstory` pawns) — silent corruption for common pawn type
5. **Fix HIGH-001** (hardcoded age 60) — breaks `elderhoodAge` config
6. **Fix HIGH-005** (detached comp on reflection failure) — silent data loss
7. **Fix HIGH-008** (comma-separated `workDisables` patches) — 8 RMCB balance patches silently failing
8. **Fix CRIT-DOC-001, CRIT-DOC-002, CRIT-DOC-003** (About.xml + README inaccuracies)
9. **Fix HIGH-002** (HardshipBonding duplicate participants) — preemptive fix before CRIT-005 re-enables the feature
10. **Fix HIGH-004** (ZCB `Enum.TryParse` hot path) — performance
11. Address MED-005 through MED-017 as time permits
12. Address LOW-001 through LOW-006 as part of normal cleanup

## Citations

- `engram` observation #54 — first audit (CRIT-01 through CRIT-02)
- `engram` observation #57 — second audit (CRIT-03, CRIT-04, HIGH-01, MED-01, MED-03, MED-04)
- `engram` observation #61 — implementation session of second-audit fixes
- `CHANGELOG.md` — v1.0.0 through v1.5.1 entries
- `About/About.xml` — mod metadata (has known inaccuracies, see CRIT-DOC-001/002)
- `README.md` — user-facing docs (has known inaccuracies, see CRIT-DOC-003)
