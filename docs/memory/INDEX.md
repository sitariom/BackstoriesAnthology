---
type: index
title: UnifiedBackstories — Project Memory Index
description: Catalog of project knowledge, audit reports, ADRs, and lessons
timestamp: 2026-07-19
---

# Project Memory Index — UnifiedBackstories

Mod RimWorld 1.6 que unifica 1.353 backstories de 12 mods fonte em um pacote standalone.

## Backstory Count (Ground Truth — 2026-07-19)

| Source Mod | Count | Verified |
|------------|-------|----------|
| Cybranian Backstories+ | 401 | ✅ |
| Before the Crash | 206 | ✅ |
| Vanilla Backstories Expanded | 174 | ✅ |
| Seal's Backstory Collection | 137 | ✅ |
| Medieval Backstories | 107 | ✅ |
| More Backstories | 58 | ✅ |
| SNS (Kurzaen) | 56 | ✅ |
| Tribal Backstories | 55 | ✅ |
| Elderhood Backstories | **43** | ⚠️ docs say 42 |
| Apocalyptic Backstories | 25 | ✅ |
| Saito's Backstories | 12 | ✅ |
| ZCB Childhood (Zylle) | 79 | ✅ (integrated system) |
| **TOTAL** | **1,353** | ground truth |

## Lessons

- [2026-07-19 — Third audit report](lessons/2026-07-19-third-audit-report.md) — 31 findings (4 CRIT, 8 HIGH, 13 MED, 6 LOW) — code/XML/docs review with 3 parallel subagents
- [2026-07-19 — Fourth audit report](lessons/2026-07-19-fourth-audit-report.md) — 58 NEW findings (4 CRIT, 8 HIGH, 22 MED, 24 LOW) — logic correctness + lifecycle + translations (different angles)

## Decisions (ADRs)

- [ADR-001 — Single Patcher consolidation](decisions/ADR-001-single-patcher.md) (2026-07-17)
- [ADR-002 — Per-pawn work tags patch](decisions/ADR-002-per-pawn-work-tags.md) (2026-07-17)
- [ADR-003 — Documentation reconciliation required](decisions/ADR-003-documentation-reconciliation.md) (2026-07-19)

## Combined Audit Totals (3rd + 4th passes)

**89 total findings** across code, XML, translations, and documentation:

| Severity | 3rd Audit | 4th Audit | Total |
|----------|-----------|-----------|-------|
| CRITICAL | 4 | 4 | 8 |
| HIGH | 8 | 8 | 16 |
| MEDIUM | 13 | 22 | 35 |
| LOW | 6 | 24 | 30 |
| **TOTAL** | **31** | **58** | **89** |

## Raw Sources

- `raw/` — immutable audit inputs (none yet)

## See Also

- `../../../CHANGELOG.md` — version history
- `../../../README.md` — user-facing docs (has known inaccuracies, see ADR-003)
- `../../../About/About.xml` — mod metadata (has known inaccuracies, see ADR-003)

## Active Issues (2026-07-19)

See [lessons/2026-07-19-third-audit-report.md](lessons/2026-07-19-third-audit-report.md) for the full bug list. Top priorities:

1. **CRITICAL**: `MapComponent_RMCBHardship` is dead code — hardship bonding feature never activates
2. **HIGH**: 4 silent `catch` blocks in `ZCBackstoryValidator.cs` (MED-03 regression)
3. **HIGH**: `PawnGenerator_GeneratePawn_Patch` uses hardcoded age 60 instead of `comp.ElderhoodAge`
4. **HIGH**: `CheckTechLevel` rejects all ZCB defs when only `minTechLevel` is set
5. **HIGH**: Elderhood skill bonuses skipped for pawns with `forceNoBackstory=true`
6. **CRITICAL (docs)**: About.xml claims 1,408 backstories / 13 mods / 1.5+1.6 support — all incorrect
7. **CRITICAL (docs)**: README claims "38 automated regression tests" — zero test files exist
