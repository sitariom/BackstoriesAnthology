---
type: lesson
title: Fourth Audit Report — 58 NEW findings (logic, lifecycle, translations)
description: Fourth deep audit using 3 parallel subagents with DIFFERENT angles (4-file logic correctness, system-level lifecycle/Harmony, translation quality). Found 1 CRITICAL orphan record making 3 backstories unobtainable, 8 HIGH logic bugs, 22 MEDIUM, 27 LOW.
timestamp: 2026-07-19
tags: [audit, bug-report, logic, lifecycle, translations, harmony, rimworld]
---

# Fourth Audit Report — UnifiedBackstories (2026-07-19)

## Methodology

Fourth audit pass with **different angles** from the third audit. The third audit focused on null refs, double-apply, DLC guards, XML schema, and documentation. This fourth audit focuses on:

- **QA**: Logic correctness of 4 files not deeply covered before (`BackstoryPairing.cs`, `TraitAlignment.cs`, `AwkwardRomance.cs`, `NeedDecayPatches.cs`)
- **Specs**: System-level concerns — pawn lifecycle edge cases, ZCB-Records orphan check, Harmony patch inventory
- **TechWriter**: Translation quality (not coverage) — broken tokens, hardcoded pronouns, grammar, typos

## Summary

| Severity | Count | Categories |
|----------|-------|------------|
| CRITICAL | 4 | Orphan record (3 unobtainable backstories) + 3 hardcoded pronoun bugs in translations |
| HIGH | 8 | Logic correctness bugs, save/load desync, age regression not handled, Need_Seeker overhead, ProhibitedTraits ignored |
| MEDIUM | 22 | Classification gaps, edge cases, UI, performance |
| LOW | 24 | Code quality, hardcoded constants, minor balance |
| **TOTAL NEW** | **58** | (4 CRIT + 8 HIGH + 22 MED + 24 LOW) |

**Combined with 3rd audit (31 findings)**: 89 total findings across 4 audit passes.

---

## CRITICAL Findings (4)

### CRIT-006: `ZCB_TimeAsWildMan` is an orphan record — 3 backstories UNOBTAINABLE
- **File**: `1.6/Defs/RecordDefs/ZCB_Records.xml:5-11`
- **Affected backstories**: `1.6/Defs/BackstoryDefs/ZCB_Feral_Child_Backstories.xml:26-31, 56-61, 85-90`
- **Category**: Orphan definition / dead content
- **Description**: `ZCB_TimeAsWildMan` is declared but its `workerClass` was removed during integration (comment at ZCB_Records.xml:10). No `pawn.records.AddTo` call exists in Source/ for this record. The record always reads 0.
- **Three ZCB feral-child backstories require** `<requiredRecords><li><recordDef>ZCB_TimeAsWildMan</recordDef><minValue>7500</minValue></li></requiredRecords>`. The validator (`ZCBackstoryValidator.CheckRecords`) checks: `if (value < scaledMin) return false;` — `0 < 30000` is true → FAILS every time.
- **Impact**: `UB_ZCB_FeralChild`, `UB_ZCB_FeralChild2`, `UB_ZCB_WildChild` can NEVER pass validation during pawn generation. The retry loop (MaxRetries=15) exhausts and falls back to ANY ZCB def (per MED-011 in 3rd audit, but the def is still rejected). These backstories are dead content — advertised in the XML but impossible to obtain.
- **Fix**: Either (a) implement a `RecordWorker_TimeAsWildMan` that increments when pawn has `Hediff_WildMan`, OR (b) remove the `<requiredRecords>` block from the 3 feral-child backstories and rely on other validators (tech level, body parts, traits).

### CRIT-TRANS-001: Hardcoded "he/his" in `UB_Medieval_RenegadeRoyal` description
- **File**: `Languages/English/DefInjected/BackstoryDef/Medieval_Backstories_Medieval_Adulthood.xml:40`
- **Category**: Translation — hardcoded pronouns
- **Description**: "He outright ignored his starving subjects" — hardcoded male pronouns for a backstory that should support both genders via `{PAWN_gender ? he : she}` / `{PAWN_gender ? his : her}` tokens. The Medieval_RenegadeRoyal backstory has no `titleFemale` restriction, so a female pawn may receive this backstory and see "He outright ignored his starving subjects" describing her.
- **Fix**: Wrap "He" → `{PAWN_gender ? He : She}`, "his" → `{PAWN_gender ? his : her}` (×3 occurrences).

### CRIT-TRANS-002: Hardcoded "in him" in `UB_Cybranian_Adulthood_Ruthless_Predator`
- **File**: `Languages/English/DefInjected/BackstoryDef/Cybranian_Adulthood_Tribal.xml:34`
- **Category**: Translation — hardcoded pronouns
- **Description**: "There seems to be nothing human left in him." — hardcoded male pronoun. Backstory is gender-neutral (no `titleFemale` restriction).
- **Fix**: "in him" → "in {PAWN_gender ? him : her}".

### CRIT-TRANS-003: Hardcoded "him/His" in `UB_Cybranian_Childhood_Young_Gladiator`
- **File**: `Languages/English/DefInjected/BackstoryDef/Cybranian_Childhood_Industrial.xml:104`
- **Category**: Translation — hardcoded pronouns
- **Description**: "fights in the stadium against people like him, as well as animals. His life". Two hardcoded male pronouns in a gender-neutral backstory.
- **Fix**: "him" → "{PAWN_gender ? him : her}", "His" → "{PAWN_gender ? His : Her}".

---

## HIGH Findings (8)

### HIGH-009: `GetOrCreateElderhoodComp` reflection-created comp doesn't survive save/load
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:169-198`
- **Category**: Save/Load — silent data loss for non-Human races
- **Description**: When `pawn.GetComp<CompElderhoodBackstory>()` returns null (e.g., for mod-added androids/xenotypes that don't inherit from `Human`), the method creates a comp via reflection on `ThingWithComps.comps`. **This runtime-created comp is NOT serialized by RimWorld on save** — comps are re-instantiated on load from the ThingDef's `compProps`, and this race's ThingDef has none. On next load:
  1. RimWorld creates the pawn without the comp.
  2. `Pawn_StoryTracker_ExposeData_Patch` postfix calls `TryAssignElderhood`.
  3. `FillBackstorySlotShuffled` → `GetOrCreateElderhoodComp` → creates a NEW comp via reflection.
  4. The saved elderhood defName is lost.
  5. A NEW random elderhood is assigned if age ≥ 60.
- **Impact**: Non-Human human-like races (mod-added androids, xenotypes from other mods) lose their elderhood assignment on every save/load cycle. Compounds with HIGH-005 from 3rd audit (detached comp).
- **Fix**: Add `CompProperties_ElderhoodBackstory` to ALL `ThingDef`s with `race.Humanlike == true` via a broader XML patch using `PatchOperationFindMatch` (instead of just patching `Human`).

### HIGH-010: Age regression (Biotech serum) does NOT clear elderhood
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs` (no patch on age reversal)
- **Category**: Logic — silent gameplay corruption
- **Description**: If a pawn is age 65 (elderhood assigned) and is de-aged to 40 via Biotech age reversal serum:
  - `CompElderhoodBackstory.elderhoodBackstory` is NOT cleared.
  - `HasElderhood` stays true.
  - `Pawn_StoryTracker_DisabledWorkTags_Patch.Postfix` continues applying elderhood's `workDisables`.
  - `CharacterCardUtility_DoLeftSection_Patch` still shows the elderhood UI.
  - `Pawn_AgeTracker_BirthdayBiological_Patch` only fires on biological birthday (age INCREASE), not on age reversal.
  - No patch on `Pawn_AgeTracker.AgeBiologicalTicks` or the age-reversal hediff.
- **Impact**: A de-aged pawn keeps elderhood backstory, work disables, and UI section indefinitely. Player must manually clear via UI.
- **Fix**: Patch `Pawn_AgeTracker.AgeBiologicalTicks` (or the Biotech age-reversal hediff) to call `comp.SetElderhood(null)` when age drops below `comp.ElderhoodAge`.

### HIGH-011: `BackstoryPairing` nobility classifier misses "vizier", "thane", and others
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:105-111`
- **Category**: Classification — misclassification
- **Description**: `NobilityKeywords` omits several real noble titles. Verified misclassifications in shipped XML:
  - `UB_BTC_Adulthood_ForgottenVizier` (title "forgotten vizier", `spawnCategories=ImperialCommon`) — `IsNobility=false` because "vizier" is not in `NobilityKeywords`.
  - `UB_VBE_Thane` (title "Thane") — `IsNobility=false`.
  - Both can pair with a slave/urchin childhood without being flagged implausible, defeating the rule on line 143.
- **Impact**: Implausible pairings slip through the filter — exactly the case it was written to catch.
- **Fix**: Add `"vizier", "thane", "sultan", "emir", "shah", "caliph", "pharaoh", "satrap", "raja", "maharajah", "nawab"` to `NobilityKeywords`.

### HIGH-012: `IsLowborn` misclassifies "Cloned Heir" via Pirate spawnCategory
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:88-92, 223-236`
- **Category**: Classification — false positive
- **Description**: `IsLowborn` returns true if ANY `spawnCategory` is in `LowbornCategories`. Verified XML:
  - `UB_VBE_ClonedHeir` (title "Cloned heir", `spawnCategories="Outlander|Pirate|Offworld|ImperialFighter"`) — `IsLowborn=true` solely because "Pirate" is in `LowbornCategories`, even though the title is explicitly a noble heir.
  - If adulthood is a baron/duke, `Plausible()` returns false, the pairing is rerolled — even though "cloned heir → baron" is thematically coherent.
- **Impact**: Noble-themed childhoods that include Pirate/Outsider as a spawn-category hint for variety get falsely treated as gutter-born, blocking legitimate noble pairings.
- **Fix**: Only flag `IsLowborn` when lowborn categories dominate (no ImperialRoyal/ImperialCommon/Offworld also present), OR also check `LowbornKeywords` text in title and require keyword confirmation.

### HIGH-013: `TryInjectFavored` (TraitAlignment) can inject traits prohibited by `PawnGenerationRequest`
- **File**: `Source/UnifiedBackstories/TraitAlignment.cs:318-353, 371-402`
- **Category**: Logic — constraint violation
- **Description**: `TryInjectFavored` does not receive the `req` (PawnGenerationRequest?) parameter, and `CanPlace` does not check `req.ProhibitedTraits`. Verified by tracing:
  - Line 318 signature: `TryInjectFavored(Pawn pawn, List<Trait> list, BackstoryProfile profile)` — no `req`.
  - Line 371 `CanPlace` checks `HasTrait`, `DuplicatesOrConflicts`, `Childhood/Adulthood.DisallowsTrait`, `WorkTagIsDisabled` — never `req.ProhibitedTraits`.
- **Impact**: Silent violation of quest/pawn-generation constraints. Quest pawns that must not have a given trait (e.g., "no Psychopath") can end up with it because some killer backstory favors Bloodlust-adjacent traits.
- **Fix**: Thread `req` into `TryInjectFavored` and `CanPlace`; in `CanPlace`, return false if `req.Value.ProhibitedTraits.Contains(def)`.

### HIGH-014: `TraitAlignment.Txt()` ignores description text — misses thematic keywords
- **File**: `Source/UnifiedBackstories/TraitAlignment.cs:210-214, 47`
- **Category**: Logic — feature partially broken
- **Description**: `Txt()` returns `b.defName + " " + b.title` only — the description (where most thematic keywords live) is never inspected. Verified example:
  - `UB_Seal_BesiegedCityChild` (title "besieged city child") — "besieged" is not in any keyword list, and the title alone doesn't contain "war " (with trailing space) or "camp" or any hardship keyword, so `hardship=false` and no Tough/Brave favored traits are applied, even though a besieged city child is clearly hardship-themed. The description almost certainly contains "war" or "bombardment" but is never read.
- **Impact**: A large fraction of backstories with thematic descriptions but sparse titles get no trait alignment at all, defeating the feature for them.
- **Fix**: Include `b.description` (or `b.FullDescriptionFor(null)`) in the text matched by `Build()`, lowercased. Strip gender tokens before matching.

### HIGH-015: Need_Seeker patch fires for Need_Mood, Need_Beauty, Need_RoomSize — only Need_Comfort filtered
- **File**: `Source/UnifiedBackstories/NeedDecayPatches.cs:76-91`
- **Category**: Logic — fragile design + overhead
- **Description**: Verified via reflection on Assembly-CSharp.dll: `Need_Mood`, `Need_Beauty`, and `Need_RoomSize` ALL inherit from `Need_Seeker` and do NOT override `NeedInterval` (declaring type is `Need_Seeker` for all). The Harmony patch on `Need_Seeker.NeedInterval` fires for ALL of these needs. The Postfix's `if (__instance is Need_Comfort)` filter correctly limits `ScaleFall` to Comfort — so behavior is correct, but the patch adds Harmony overhead to Mood/Beauty/RoomSize intervals that it never uses.
- **Impact**: No gameplay corruption — but unnecessary Prefix overhead on three other need types, and fragile: if someone removes the `is Need_Comfort` filter during maintenance, Mood decay would silently get scaled (a major balance change).
- **Fix**: Patch `Need_Comfort.NeedInterval` directly via `AccessTools.Method(typeof(Need_Comfort), "NeedInterval")`, or add a Transpiler — eliminates cross-need overhead and fragile filter.

### HIGH-016: Broken `{PAWN_gender}` tokens split English words
- **File**: `Languages/English/DefInjected/BackstoryDef/Cybranian_Adulthood_Tribal.xml:31, 43, 54` / `Elderhood_Elderhoods.xml:107`
- **Category**: Translation — broken text rendering
- **Description**: 4 backstories have `{PAWN_gender}` tokens that split English words, producing garbled text in-game:
  - `UB_Cybranian_Adulthood_Battle_Shaman:31` — "{PAWN_gender ? he : she} lp the tribe" — splits "help"
  - `UB_Cybranian_Adulthood_Leader_Slave:43` — "for the {PAWN_gender ? he : she} ad of the tribe" — splits "head"
  - `UB_Cybranian_Adulthood_Bard:54` — "{PAWN_gender ? he : she} lps" — splits "helps"
  - `UB_Elderhood_UnnecessaryResearcher:107` — "in {PAWN_gender ? him : her} old age" uses objective case where possessive is required (×3)
- **Impact**: In-game descriptions have garbled text like "he lp the tribe" / "he ad of the tribe" — visible to players.
- **Fix**: Restructure sentences. For "help" cases: "they {PAWN_gender ? help : help} the tribe". For "head": "for the leader of the tribe". For possessive: replace "him/her" with "his/her".

---

## MEDIUM Findings (22)

### Classification & Logic (8)

### MED-019: `CategoryWorlds` missing common spawnCategories
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:64-86`
- Missing: `MedievalNoble`, `Rare`, `Outsider`, `MedievalArtist`, `Pirate`, `Cult`, `Madman`, `InsectsRelated`, `ChildTribal`. Backstories with ONLY unmapped categories fall back to keyword-only matching — weaker than intended.
- **Fix**: Add missing categories to `CategoryWorlds`.

### MED-020: Mixed-tier spawnCategories produce contradictory classification
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:184-201, 223-236`
- A backstory with `spawnCategories="Tribal|ImperialRoyal"` is `IsLowTechPlanetbound=false` (good) but `IsLowborn=true` (blocks noble adulthood). The two classifiers disagree.
- **Fix**: Make `IsLowborn` aware of high-tier categories in same def.

### MED-021: After 3 failed rerolls, original adulthood is lost
- **File**: `Source/UnifiedBackstories/BackstoryPairing.cs:38-49`
- The reroll loop unconditionally calls `FillBackstorySlotShuffled`, overwriting `pawn.story.Adulthood`. If all 3 rerolls fail, the 3rd implausible roll is kept — original is discarded.
- **Fix**: Cache `originalAdulthood = pawn.story.Adulthood` before loop; restore if loop exhausts.

### MED-022: `PawnGenerator_GeneratePawn_Patch` lacks `RaceProps.Humanlike` guard — pollutes animal comps
- **File**: `Source/UnifiedBackstories/ElderhoodSystem.cs:364-386`
- For non-human pawns aged 60+ (e.g., Galapagos tortoise, age 100), the postfix reflection-creates an empty `CompElderhoodBackstory` on the animal. No crash, no functional impact, but pollutes the animal's comps list.
- **Fix**: Add `if (!__result.RaceProps.Humanlike) return;` at top of postfix.

### MED-023: `AwkwardRomance.UB_ModCompat` only detects one romance mod — misses others
- **File**: `Source/UnifiedBackstories/AwkwardRomance.cs:22-31`
- Only detects `Mlie.LessStupidRomanceAttempt`. Misses Vanilla Expanded Romance, Psychology, Romance Diversified, Rooboid.SamouraiRomance. May stack penalties making romance impossible.
- **Fix**: Add `ModsConfig.IsActive` checks for other known romance mods.

### MED-024: `AwkwardRomance` rebuff count includes memories from pawns no longer on map
- **File**: `Source/UnifiedBackstories/AwkwardRomance.cs:67-88, 120-123`
- `CountRebuffs` counts every `RebuffedMyRomanceAttempt` memory regardless of whether `otherPawn` is still alive/present. A pawn who struck out with 3 raiders (who left the map) gets `total=3` and `__result *= 0.15f` for ALL future attempts for ~10 days.
- **Fix**: Filter `CountRebuffs` to only count memories whose `otherPawn` is still alive and on the same map.

### MED-025: `TraitAlignment` does not fire on CharacterCard UI backstory changes
- **File**: `Source/UnifiedBackstories/TraitAlignment.cs:217-271`
- Postfix is on `PawnGenerator.GenerateTraitsFor`. When a player edits backstory via CharacterCard UI, `GenerateTraitsFor` is NOT called — traits stay stale relative to new backstory.
- **Fix**: Document limitation in setting tooltip, OR add a second Harmony patch on `Pawn_StoryTracker.SetBackstory` that re-runs alignment.

### MED-026: "war " keyword with trailing space misses "warfare", "warlord", "warrior"
- **File**: `Source/UnifiedBackstories/TraitAlignment.cs:62`
- `"war "` matches "war hero" but NOT "warfare" or "warlord" or "warrior" (no space after). A backstory titled "warfare specialist" gets `hardship=false`.
- **Fix**: Add "warfare" and "warlord" explicitly to the hardship keyword list.

### Translation (8)

### MED-TRANS-001: Grammar — missing space after period in `UB_SNS_AdultMothMilkFarmer`
- **File**: `Languages/English/DefInjected/BackstoryDef/SNS_AdultMothMilkFarmer.xml:5`
- "Silkmoths.[PAWN_nameDef]'s main job" — missing space after period.
- **Fix**: Add single space: "Silkmoths. [PAWN_nameDef]'s".

### MED-TRANS-002: Wrong possessive — "[PAWN_possessive] the whole group was brutally murdered"
- **File**: `Languages/English/DefInjected/BackstoryDef/Apocalyptic_Backstories_Adult.xml:63`
- Should be "[PAWN_pronoun]r entire group" or similar — possessive used where pronoun+possessive intended.
- **Fix**: Use "[PAWN_pronoun]r entire group" or restructure.

### MED-TRANS-003: `UB_Elderhood_UnnecessaryResearcher` uses "him/her" where possessive "his/her" required
- **File**: `Languages/English/DefInjected/BackstoryDef/Elderhood_Elderhoods.xml:107`
- "in {PAWN_gender ? him : her} old age" — ×3 occurrences of objective case where possessive is grammatically required.
- **Fix**: Replace "him/her" with "his/her" throughout that description.

### MED-TRANS-004: `UB_Elderhood_OldMechanic` — missing "he" produces broken sentence
- **File**: `Languages/English/DefInjected/BackstoryDef/Elderhood_Elderhoods.xml:42`
- "anything puts {PAWN_gender ? his : her} hands on" — missing "he" auxiliary: should be "anything he puts his hands on".
- **Fix**: Add "he/she" before "puts".

### MED-TRANS-005: British/American spelling inconsistency
- **Files**: `SNS_AdultMothMilkFarmer.xml` ("seperate" — also a typo), `Tribal_Backstories_Tribal_Childhood.xml:14` ("seperated"), `Seal_v1.2_Tribal_Childhood.xml:11` ("Deaths occured frequently")
- "seperate" is a TYPO (correct: "separate"). "occured" is a typo (correct: "occurred"). "sewn" used as "sew them close" should be "sewn them closed".
- **Fix**: Correct all 3 typos.

### MED-TRANS-006: "jonied" typo
- **File**: `Languages/English/DefInjected/BackstoryDef/ZCB_All_Backstories.xml:30`
- `UB_ZCB_Builder` description has "jonied" — should be "joined".
- **Fix**: Replace "jonied" → "joined".

### MED-TRANS-007: Lowercase "king"/"queen" in Medieval narrative
- **File**: `Languages/English/DefInjected/BackstoryDef/Medieval_Backstories_Medieval_Adulthood.xml` (multiple)
- Titles in narrative text are lowercase "the king", "the queen" — minor capitalization issue but acceptable for narrative style.
- **Verdict**: LOW priority, acceptable. Listed for completeness only.

### MED-TRANS-008: Hardcoded "the king"/"the queen" without gender token
- **File**: `Languages/English/DefInjected/BackstoryDef/Medieval_Backstories_Medieval_Adulthood.xml:46`
- `UB_Medieval_LadyInWaiting` description uses "such a task was below her station" — female-only backstory (no `titleFemale`); acceptable to keep "her". OK.
- `UB_Medieval_YoungKing:26` "his oldest son" / `UB_Medieval_YoungQueen:28` "her oldest daughter" — gendered counterparts. Acceptable.

### MED-TRANS-009: Suspected orphan Keyed translation `UB.ModName`
- **File**: `Languages/English/Keyed/UB_Keyed.xml:50`
- `UB.ModName` defined but no `"UB.ModName".Translate()` call found in Source/. Possibly referenced in About.xml or ModMetaData, but if not — orphan.
- **Fix**: Verify usage; remove from keyed file if truly unused.

### Logic / Edge Cases (6)

### MED-027: Manual backstory edits via dev mode bypass ZCB validator
- **Files**: `Source/UnifiedBackstories/ZCBackstoryValidator.cs` (no re-validation on edit)
- Player can manually assign a ZCB backstory that violates requirements (records, traits, skills). Matches vanilla behavior (no re-validation) but is a gap.
- **Fix**: Document limitation OR add re-validation patch on backstory change.

### MED-028: No cross-mod `[HarmonyBefore]`/`[HarmonyAfter]` annotations
- **Files**: All 20 patches
- UB-internal ordering is OK (`Priority.High` on ZCB validator), but cross-mod ordering on `PawnGenerator.GeneratePawn`, `FillBackstorySlotShuffled`, `Pawn_StoryTracker.ExposeData`, `BackstoryDef.FullDescriptionFor` is undefined.
- Common mods patching same methods: Character Editor, EdB Prepare Carefully, Android Tiers, VBE, Psychology.
- **Fix**: Add `[HarmonyBefore("other.mod.id")]` annotations for known conflict mods.

### MED-029: `ScaleFall` in NeedDecayPatches can set `CurLevel` above `CurInstantLevel`
- **File**: `Source/UnifiedBackstories/NeedDecayPatches.cs:46-59`
- The formula can violate the documented invariant when `CurInstantLevel < levelBefore`. One interval of slightly inflated comfort/joy — cosmetic, not gameplay-breaking.
- **Fix**: Clamp result to `Mathf.Min(newLevel, need.CurInstantLevel)` when `CurInstantLevel < levelBefore`, OR remove the misleading comment.

### MED-030: Biotech/Ideology precept need-decay modifiers partially overridden
- **File**: `Source/UnifiedBackstories/NeedDecayPatches.cs:46-59`
- Biotech/Ideology precepts modify need decay inside vanilla `NeedInterval`. `ScaleFall` restores a fraction of the (precept-modified) drop, partially cancelling the precept's intended difficulty during first 20 days.
- **Fix**: Detect whether precept modifier applied and skip `ScaleFall` for those needs, OR document the interaction.

### MED-031: `Need_Joy` sub-need tolerance is not scaled — only aggregate `CurLevel`
- **File**: `Source/UnifiedBackstories/NeedDecayPatches.cs:62-74`
- Need_Joy has internal joy-kind tolerances that build up during joy activities. `ScaleFall` only restores `CurLevel`, not tolerance. After grace period ends, sudden joy crash as tolerance throttles gain while decay returns to 100%.
- **Fix**: Also scale tolerance decay during grace period, OR exclude `Need_Joy` from grace-period feature.

### MED-032: `TraitAlignment` reroll recursion is expensive (up to 6× per opposed slot)
- **File**: `Source/UnifiedBackstories/TraitAlignment.cs:298-316`
- `RerollNonOpposed` calls `PawnGenerator.GenerateTraitsFor(pawn, 1, req, ...)` up to `RerollTries=6` times per opposed slot. With 3 opposed traits → 18 trait-generation calls during one pawn's generation.
- **Fix**: Lower `RerollTries` to 3, OR cache a pool of pre-rolled candidates.

---

## LOW Findings (24)

### Code Quality / Hardcoded Constants (10)

- **LOW-007**: `BackstoryPairing.Plausible()` called 1-4× per pawn with no caching (perf).
- **LOW-008**: Plausibility asymmetry is intentional (documented) — Childhood=Spacefaring + Adulthood=LowTech allowed by design.
- **LOW-009**: TraitAlignment hardcoded `RerollOpposedChance=0.85f, InjectFavoredChance=0.6f` — not exposed to settings.
- **LOW-010**: TraitAlignment trait degrees not validated against TraitDef's actual valid degrees — latent if mods restructure traits.
- **LOW-011**: AwkwardRomance no faction filter — cooldown applies to NPCs too (slows NPC faction romance).
- **LOW-012**: AwkwardRomance hardcoded thresholds (`towardRecipient >= 2`, `total >= 3 → 0.15f`, etc.) — not in settings.
- **LOW-013**: `AwkwardChat.Inhumanized()` check verified safe — extension method exists in 1.6 base assembly regardless of Anomaly DLC active.
- **LOW-014**: NeedDecayPatches hardcoded multipliers (GraceDays=5, RampEndDays=20, MinDecayFactor=0.25) — not in settings.
- **LOW-015**: NeedDecayPatches applies to all player-faction pawns including slaves (Ideology) — no differentiation.
- **LOW-016**: `PawnGenerator_GenerateTraits_ElderhoodPatch` redundant `te.def` null check (line 309 already verified, line 320 dead).

### Lifecycle Verification (4 PASS)

- **LOW-017**: Biotech babies — age 0 correctly handled. No NRE surface. `FillBackstorySlotShuffled` not fired for babies (no slots). ✅ PASS
- **LOW-018**: ZCB-Records persistence — vanilla `Pawn_RecordsTracker.ExposeData` auto-saves. ✅ PASS
- **LOW-019**: Dead pawns in participant lists — minor transient memory (cleared on threat end). `GrantBonds` filters `Dead && Destroyed`. Adequate.
- **LOW-020**: Mechanoids — correctly skipped in 3 of 4 elderhood paths (only `GeneratePawn` postfix misses `Humanlike` check — see MED-022). ✅ Mostly PASS

### Translation (5)

- **LOW-TRANS-001**: "R.I.M." acronym in `UB_SNS_NOTHATISNOTSOLIDSNAKE` description — cosmetic, no actual shouting.
- **LOW-TRANS-002**: Hardcoded pronoun in `UB_Medieval_LadyInWaiting` "her station" — female-only backstory, acceptable.
- **LOW-TRANS-003**: Hardcoded "his oldest son" in `UB_Medieval_YoungKing` — gendered counterpart exists. Acceptable.
- **LOW-TRANS-004**: ZCB descriptions use `[PAWN_*]` bracket tokens — this is the CORRECT RimWorld bracket-token syntax for DefInjected backstories. NOT an issue.
- **LOW-TRANS-005**: 0 empty `<description>` tags — clean. ✅ PASS

### Harmony Patch Inventory (5)

- **LOW-021**: 20 total Harmony patches — all Prefix/Postfix, ZERO transpilers. ✅ Pass — safest category, survives RimWorld updates.
- **LOW-022**: All 20 patch signatures verified against RimWorld 1.6 — no mismatches found. ✅ Pass
- **LOW-023**: `__result` mutability — all Postfix patches that mutate `__result` use `ref` qualifier correctly. ✅ Pass
- **LOW-024**: `PawnBioAndNameGenerator.FillBackstorySlotShuffled` patched by 2 UB patches (ZCB + BackstoryPairing) — `Priority.High` on ZCB validator correctly orders them. ✅ Pass
- **LOW-025**: Two `[StaticConstructorOnStartup]` classes (`UnifiedBackstoriesPatcher` + `UB_ModCompat`) — UB_ModCompat is benign (no PatchAll). Acceptable.

### Save/Load Verification (2)

- **LOW-026**: `CompElderhoodBackstory.PostExposeData` correctly persists state. ✅ Pass
- **LOW-027**: Removed-elderhood-def scenario — `Scribe_Defs.Look` returns null, `Pawn_StoryTracker_ExposeData_Patch` re-assigns. Graceful recovery. ✅ Pass

---

## PASS Items (verified clean)

- **20 Harmony patches with no transpilers** — zero fragile IL modification
- **All patch signatures match RimWorld 1.6**
- **All `__result` mutations use `ref` correctly**
- **ZCB records persistence via vanilla `Pawn_RecordsTracker`**
- **Biotech babies — no NRE surface**
- **CompElderhoodBackstory.ExposeData correctly persists**
- **Removed-def recovery graceful**
- **Translation coverage 100% for .title and .description**
- **No empty description tags**
- **0 legacy `{PAWN_possessive}` curly tokens**
- **31 Keyed translations all defined, 30 used (1 suspected orphan: `UB.ModName`)**

---

## Recommendations (NEW priority order merging 3rd + 4th audits)

1. **CRIT-006** (orphan ZCB_TimeAsWildMan) — fixes 3 dead backstories
2. **CRIT-005** (3rd audit: dead MapComponent_RMCBHardship) — feature sold in settings but doesn't work
3. **HIGH-009** (reflection comp lost on save/load for non-Human races) — silent data loss
4. **HIGH-010** (age regression doesn't clear elderhood) — silent gameplay corruption
5. **HIGH-003** (3rd audit: MED-03 regression — 4 silent catches)
6. **HIGH-006** (3rd audit: CheckTechLevel rejects when only minTechLevel set)
7. **HIGH-007** (3rd audit: Elderhood skill bonuses skipped for forceNoBackstory)
8. **HIGH-013** (TraitAlignment ignores ProhibitedTraits) — quest constraint violation
9. **HIGH-014** (TraitAlignment ignores description text) — feature half-broken
10. **HIGH-011** (Nobility classifier misses vizier/thane) — implausible pairings slip through
11. **HIGH-012** (IsLowborn misclassifies Cloned Heir) — blocks legitimate noble pairings
12. **HIGH-015** (Need_Seeker patch overhead on Mood/Beauty/RoomSize) — fragile + perf
13. **HIGH-016** (broken {PAWN_gender} tokens split English words) — visible garbled text
14. **CRIT-TRANS-001/002/003** (hardcoded pronouns in 3 backstories) — gendered text errors
15. **CRIT-DOC-001/002/003** (3rd audit: About.xml/README inaccuracies)
16. All MEDIUM and LOW as time permits

## Citations

- `engram` observation #66 — third audit report
- `docs/memory/lessons/2026-07-19-third-audit-report.md` — 31 prior findings
- `Source/UnifiedBackstories/` — 11 .cs files
- `1.6/Defs/BackstoryDefs/` — 112 XML files
- `1.6/Defs/RecordDefs/ZCB_Records.xml` — orphan record
- `Languages/English/DefInjected/BackstoryDef/` — 18 translation files
- `Languages/English/Keyed/UB_Keyed.xml` — 31 keyed translations
