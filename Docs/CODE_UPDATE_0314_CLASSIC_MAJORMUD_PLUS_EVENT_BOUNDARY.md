# Code Update 0314 — Pass 43 Classic MajorMUD Plus Event Boundary

## Parity movement

- `native.events`: **UNKNOWN → STRONG**
- Score: **97 PASS / 84 STRONG / 19 UNKNOWN = 200**
- Strict parity: **48.5%**
- Evidence-backed parity: **90.5%**

## Native evidence connected

Retained MajorMUD Plus evidence already contains an executable-derived contest contract from `WCCMMUTL.EXE`. Pass 43 makes that boundary explicit in the parity ledger and runtime evidence registry instead of leaving the broad event assertion UNKNOWN.

Recovered narrow contracts:

- rune item: legacy **965** / canonical `item.965`
- exactly **17** placement ranges totaling **40** runes
- at most **50** placement attempts per rune
- reject room attribute mask **0x10**
- per-room capacity **17 visible / 15 hidden**
- visible and hidden contest modes
- status/finish ownership-location enumeration
- destructive finish cleanup from world and saved character state
- recovered contest routines do **not** perform an automatic prize/ranking mutation

Native routines retained in the prior MajorMUD Plus closure:

- placement: `0x00404D80-0x00404FA1`
- room admission/insert: `0x00404CC4-0x00404D7F`
- result scan: `0x00404FA2-0x0040521C`

## Deliberate UNKNOWN boundary

Pass 43 does **not** claim exact native contest RNG parity. `RuntimeMajorMudPlus` currently selects candidate rooms with the Reforged runtime RNG, but the exact native MajorMUD/Worldgroup contest callsite, execution-thread ownership, and draw ordering are still unresolved. Those remain gated by `native.global-rng.native-contract`. No global RNG replacement was made.

Pass 43 also does not promote a universal native event scheduler, guessed duration/reset cadence, or automatic prize/ranking behavior.

## Warning cleanup

The first cumulative Pass 43 update removes the existing CS0162 unreachable-code warning pattern without changing gameplay semantics. Evidence-policy flags that are intentionally checked by runtime validators were changed from compile-time `const` values to `static readonly` values. This preserves the same invariant values and validation branches while preventing the C# compiler from proving those branches unreachable.

The cleanup covers the existing warning sources in the RNG callsite, character modifier order, Classic configuration, Shadow evidence, lifecycle/death, regeneration, and scheduler evidence registries. The authoritative Pass 40 `RuntimeClassicCharacterAppearance` implementation is not rolled back or replaced.

The current Borin Trainer file in the uploaded authoritative repo is newer than the stale protected hash embedded in Pass 35–42 validators. Those validator guards are re-anchored to the uploaded current Borin file hash; the Borin source itself is not modified.

## Protected boundaries

- no `WCCMMUD.INI`
- Borin Trainer source unchanged
- Custom1 Simple Editor sources unchanged
- WPF/UI protected sources unchanged
- Pass 40 Shadow LOOK implementation preserved
- Pass 41 ordinary rejected-action no-op boundary preserved
