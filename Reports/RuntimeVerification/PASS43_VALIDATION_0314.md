# Pass 43 Validation — Update 0314

Pass 43 promotes only `native.events` from UNKNOWN to STRONG. The retained exact MajorMUD Plus rune-contest placement, room-admission, status/finish, and destructive result-scan contracts are now explicitly connected to the live `RuntimeMajorMudPlus` owner. Exact native contest RNG callsite/thread/draw ordering remains unresolved under `native.global-rng.native-contract`, so no PASS is claimed for the broad event assertion.

## Focused validation

- `validate_pass43_classic_majormud_plus_event_boundary.py`: **203/203 PASS**
- dedicated runtime smoke path added: `--smoke-pass43-classic-majormud-plus-event-boundary`
- Pass 43 event evidence registry: **7 Proven / 1 Strong / 1 Unknown / 1 Excluded** rows
- exact retained contest contract checked: item 965, 17 ranges, 40 runes, 50 attempts, room mask `0x10`, 17-visible / 15-hidden capacities
- exact recovered native routine ranges retained: `0x00404D80-0x00404FA1`, `0x00404CC4-0x00404D7F`, `0x00404FA2-0x0040521C`
- no universal native event scheduler, automatic prize/ranking mutation, or exact native RNG draw ordering is promoted

## Warning cleanup

The 30 CS0162 warning sites reported by the supplied successful Release build are caused by evidence-policy invariant values being compile-time constants. Pass 43 changes only the warning-producing evidence flags to `static readonly`, preserving their values and runtime validation branches while preventing compile-time unreachable-code diagnostics.

Covered warning sources:

- `WorldgroupRngDirectCallsiteRegistry.cs`
- `RuntimeCharacterModifierOrderEvidenceRegistry.cs`
- `RuntimeClassicConfigurationAuthority.cs`
- `RuntimeClassicIdentityShadowEvidenceRegistry.cs`
- `RuntimeClassicLifecycleDeathEvidenceRegistry.cs`
- `RuntimeClassicRegenerationEvidenceRegistry.cs`
- `RuntimeClassicSchedulerEvidenceRegistry.cs`
- the dependent world-rules validation branch is cleared by the configuration-authority change

The uploaded authoritative Borin Trainer source is newer than the stale protected hash embedded in Pass 35–42 validators. Retained validators were re-anchored to the current Borin source hash; the Borin source itself was not modified.

## Retained parity regression

- Pass 15: **60/60 PASS**
- Pass 24: **53/53 PASS**
- Pass 26: **70/70 PASS**
- Pass 27: **68/68 PASS**
- Pass 28: **51/51 PASS**
- Pass 29: **78/78 PASS**
- Pass 30: **56/56 PASS**
- Pass 31: **57/57 PASS**
- Pass 32: **109/109 PASS**
- Pass 33: **93/93 PASS**
- Pass 34: **106/106 PASS**
- Pass 35: **136/136 PASS**
- Pass 36: **93/93 PASS**
- Pass 37: **110/110 PASS**
- Pass 38: **121/121 PASS**
- Pass 39: **118/118 PASS**
- Pass 40: **97/97 PASS**
- Pass 41: **124/124 PASS**
- Pass 42: **118/118 PASS**
- Pass 43: **203/203 PASS**

## Librarium / generation

- Librarium graph validation: **PASS**
  - nodes: 20,826
  - relationships: 71,026
  - missing sources: 0
  - missing entity targets: 0
  - duplicate-edge diagnostics: 0
- Generation readiness: **PASS**, 32 object families, 0 failures

## Build boundary

No C#/.NET compiler is installed in this execution environment (`dotnet`, `csc`, `msbuild`, `mono`, and `mcs` are absent), so Pass 43 does **not** claim a compiled-build PASS here. The supplied baseline log shows the authoritative repo building successfully before this pass with 30 warnings and 0 errors; Pass 43 statically removes the exact constant-expression sources of those warning sites. A post-patch compiled warning count must be confirmed on a machine with the .NET SDK.

## Protected boundaries

- no `WCCMMUD.INI` file
- Borin Trainer protected source unchanged from the uploaded authoritative baseline
- Custom1 Simple Editor protected sources unchanged
- protected WPF sources unchanged
- Pass 40 `RuntimeClassicCharacterAppearance.cs` hash and Shadow LOOK members preserved
- Pass 41 state-preserving ordinary gameplay-failure boundary preserved
- `RuntimeMajorMudPlus.cs` gameplay source unchanged; Pass 43 connects retained evidence rather than inventing event behavior

## Parity

- **97 PASS / 84 STRONG / 19 UNKNOWN = 200**
- strict parity: **48.5%**
- evidence-backed: **90.5%**
