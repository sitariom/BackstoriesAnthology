---
type: decision
title: ADR-003 — Documentation reconciliation required
status: Proposed
date: 2026-07-19
---

# ADR-003 — Documentation Reconciliation Required

## Context

The third audit (2026-07-19) found that user-facing documentation has drifted
significantly from reality. Three independent subagents (QA, Specs, Developer)
all confirmed the same discrepancies:

| Source | Field | Claimed | Actual | Off by |
|--------|-------|---------|--------|--------|
| About.xml | backstory count | 1,408 | 1,353 | +55 |
| About.xml | source mod count | 13 | 12 | +1 (duplicate VBE in CREDITS) |
| About.xml | RimWorld versions | 1.5 + 1.6 | 1.6 only | false 1.5 claim |
| README.md | backstory count | 1,352 | 1,353 | -1 (Elderhood is 43, not 42) |
| README.md | mod count | "12 mods" / "11 source mods" | 12 unique | inconsistent wording |
| README.md | test count | 38 tests pass | 0 test files | false claim |
| CREDITS.txt | total | 1,352 | 1,353 | -1 |
| CREDITS.txt | VBE entry | listed twice | should be once | duplicate |
| CHANGELOG.md | v1.5.2 entry | missing | released 2026-07-18 | undocumented |

The "1,408 backstories" inflation likely originated when ZCB (79) was added
later in development: 1,353 + 79 = 1,432, or perhaps an earlier intermediate
count was used and never reconciled. The "13 mods" claim is downstream of the
CREDITS.txt duplicate VBE entry.

The "38 automated regression tests" claim is the most serious documentation
issue: it's a verifiably false quality-assurance claim that misleads users
into believing the mod has test coverage. Zero test files, scripts, runners,
or CI configuration exist in the repository, and git history shows none ever
existed.

## Decision

1. **Update About.xml**:
   - Change "1,408 backstories from 13 community mods" → "1,353 backstories from 12 community mods"
   - Remove "RimWorld 1.5 and 1.6 supported with separate load folders" → "RimWorld 1.6 supported"
   - Remove "1.5" from any supported-versions wording in description

2. **Update README.md**:
   - Change "1,352 backstories" → "1,353 backstories" (in 4 places: header, badge, table, description)
   - Update Elderhood count in table: 42 → 43
   - Update Total row: 1,352 → 1,353
   - Resolve "12 mods" vs "11 source mods" wording inconsistency
   - **Either implement 38 tests OR remove the entire "🧪 Testing" section**
   - Fix test-table row sum (currently 34) to match stated total (38), or remove the table

3. **Update CREDITS.txt**:
   - Remove duplicate VBE entry (keep only #2 or #9, not both)
   - Change "TOTAL BACKSTORIES IN THIS MOD: 1,352" → "1,353"
   - Change "13 original source mods" → "12 original source mods"

4. **Update CHANGELOG.md**:
   - Add `[1.5.2] — 2026-07-18` entry with DefInjected trailing whitespace fix details

5. **Add automated test infrastructure** (recommended):
   - Create `scripts/test-mod.ps1` (or similar) that implements the 38 tests claimed in README
   - Categories needed: directory structure (17), XML validity (1), DefName prefix (2),
     no duplicates (2), translation coverage (1), title+description (1), linked backstory (3),
     minimal fields (1), RMCB patch (4), elderhood count (1), cybranian cleanup (1)
   - Add CI workflow (`.github/workflows/test.yml`) that runs the test script on push

## Consequences

**Positive**:
- User trust restored — mod description matches reality
- Backstory count is verifiable from def files
- No false claims about test coverage
- Future contributors can rely on docs

**Negative**:
- Reduces advertised backstory count from 1,408 → 1,353 (55 fewer)
- Removes "1.5 support" claim (potential user disappointment)
- Requires implementing tests if "38 tests" claim is kept

**Neutral**:
- Mod actually works on 1.6 only — description was always wrong, this just aligns docs

## Verification

After applying this ADR:
1. Run `grep -c "<BackstoryDef>" 1.6/Defs/BackstoryDefs/*.xml` (subtract 2 abstract) — should equal 1,274
2. Run `grep -c "<UnifiedBackstories.ZCBackstoryDef>" 1.6/Defs/BackstoryDefs/*.xml` — should equal 79
3. Total = 1,353 — should match About.xml, README.md, CREDITS.txt
4. Run test script — should output "38/38 PASS"

## Citations

- [Third Audit Report](../lessons/2026-07-19-third-audit-report.md) — CRIT-DOC-001, CRIT-DOC-002, CRIT-DOC-003, LOW-004, LOW-005
- [About.xml](../../../About/About.xml) — current incorrect values
- [README.md](../../../README.md) — current incorrect values
- [CHANGELOG.md](../../../CHANGELOG.md) — missing v1.5.2 entry
