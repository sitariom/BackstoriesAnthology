# Changelog — Backstories Anthology

## [1.0.0] — 2026-07-12

### Added
- Initial release — unified compilation of 1,273 backstories from 11 community mods
- All mods merged into single, self-contained package (no original mods required)
- All defNames prefixed with `UB_` to prevent conflicts
- Full English translations for all 1,273 backstories
- RimWorld 1.5 and 1.6 support with separate load folders
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
