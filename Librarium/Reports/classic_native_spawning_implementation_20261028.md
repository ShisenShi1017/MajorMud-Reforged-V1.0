# MajorMUD Classic spawning: native lifecycle implementation — 2026-10-28

## Authority and original sources

- Original WCCMP002 native room data SHA256: `06eec04e1020ded5f63d6a9e67a794b871696ca90f6afc0fcdfcccdd4ac657be`
- Original WCCMMUD DLL SHA256: `5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077`
- Original monster codex SHA256: `83d8fb4098107324b454db4299668624a20c838a094204917d75ca4861e12024`
- Source-derived room group/index snapshot: 13,360 rooms (raw SHA256 `d51f51d17c36cdc6bb8212dfab6a6217b831f7401d0d4e473352888d3798dea1`)
- Source-derived per-room generation cap snapshot: 13,360 rooms (raw SHA256 `e45013fcca7350d9ff1561e17f780a1a116b53d58c345cb055b7fda1e760fe2b`)
- Source-derived native monster identities: 1,100 (raw SHA256 `48ac6a8ac5b4df0edd2bf2288b0c20175c06a960ceb6b7f8c4cb40a5e7856cc5`)

Original game files and RAR were accessed read-only. `/Data/Classic` is compiler output, not manually patched.

## Implemented changes on main

1. `ClassicNativeSpawnProjection` projects original group/index qualifications, direct identities, room generation cap (+0x55C), linked-area controller reference and cap (+0x5BE) onto the Classic spawn records already represented in Librarium. Missing controller references are distinguished explicitly from a native zero-cap controller.
2. `RuntimeClassicNativeGeneration.cs` provides dynamic, admission-time new monster identities for qualified ordinary Classic group spawns. Room-entry and the existing five-second active-player-room background scan call this path. Named bosses, direct NPCs, summons and custom-world NPCs retain distinct legacy paths.
3. The native generated-room count (+0x606), controller population count (+0x5C0), 15-monster physical room entry slots (+0x400) and per-monster global game limits are independently enforced. Counters are not incorrectly derived from current room occupancy. The engine uses native-style Borland `genrdn(1,100)` for the candidate replacement/early-stop roll, rather than `Random.Shared`; the monster RNG seed is persisted on restart.
4. Native ordinary monster death decrements its original room/controller counters, records the originating room last-killed time, and leaves a dead identity only until corpse expiry. The next generation chooses a new monster identity rather than reusing the old slot.
5. Native counters, last-killed timestamps, survivor IDs/current rooms and PRNG seed are persisted. Migration from fixed-slot snapshots restores *only valid surviving* ordinary monsters into the dynamic lifecycle; wrong group/index identities are not silently imported, and old dead fixed slots are not resurrected.
6. Native generated monsters do not automatically teleport home after losing an aggro target; explicit return-home admission for these monsters is also rejected. Their original generation reference remains available solely for bookkeeping and is not a behavioral home assignment.
7. SYSOP RESET ROOM zeros the original generated-room counter; SYSOP RESET AREA zeros the designated controller population counter. Neither kills or immediately regenerates ordinary monsters. The legacy fixed-slot RESET/INITIALIZE behavior is prevented from killing native monsters.
8. Deterministic source-eligibility and admission-gate regressions plus an isolated real-world native generation integration smoke are wired into the validator. Native fixed-slot enabling/retry/reset paths cannot resurrect dead dynamically generated monsters. Expired corpse identities and orphaned dead identities after restart are pruned.
9. The original per-room cap extraction can be reproduced with `Tools/MDBExtractor/extract_native_room_spawn_index.py ORIGINAL_WCCMP002 output.br64 --caps-output caps.br64`, verifying both source checksum and output SHA256.

## Original Silver River fixture

Map 1 Rooms 2342–2347, 2363–2367:
- Native group 21, permitted indexes 4–7, per-room generation cap 10;
- linked population controller Room 2342, area cap 15;
- eligible: silverfish (266, index 4), dragonfish (267, index 5), water elemental (268, index 6);
- excluded: fungus lizard (264, index 1).

This does not assign these monsters artificial fixed home rooms. They can move normally after generation.

## REQUIRED verification on local PC

```powershell
git pull origin main
dotnet build RealmGate.sln
dotnet run --project Apps/Compiler/RealmGate.Compiler.csproj -- build --world Classic
dotnet run --project Tools/RealmGateValidator/DotNet/RealmGate.Validation.csproj -- --smoke-classic-spawn-eligibility
dotnet run --project Tools/RealmGateValidator/DotNet/RealmGate.Validation.csproj -- --smoke-classic-native-generation
```

The isolated integration smoke uses a unique temporary save root. Do **not** run a live-host save migration without first backing up existing realm saves and verifying test results. This repository update was checked structurally and source hashes were compared, but the .NET SDK was unavailable in the execution environment, so compilation, native integration smoke and live-game acceptance remain **NOT RUN**.

## Exact remaining parity boundaries

- The original room snapshot contains 12,324 group-bearing rooms, but the existing Librarium spawn family is much smaller (historical 962-record baseline). The compiler only qualifies original existing spawn records; it does not yet synthesize unproven cross-version spawn records. Mixing original WCCMP002 and MMUD Explorer database versions without reconciled provenance would invent monster placement.
- Three native map-qualified group/index ranges remain unmatched to the 1,100 identities (1/1324, 1/2214, 6/386). Do not create fictitious monsters for them.
- Some original controller IDs are absent from the current WCCMP002 extracted room snapshot. The runtime treats unresolved controllers separately rather than inventing an area limit. Native live controller data/version reconciliation remains needed.
- The native group-table iteration ordering and full host-wide CRT random draw interleaving are not independently proven from the current data. The bounded `genrdn` algorithm is now used, but dedicated gameplay-host stream fidelity is not claimed.
- The current population counters model the recovered generation/death/reset ownership, but an exhaustive comparison of every native removal case (scripted deletions, host termination, all NPC combat/lifecycle owners) and full game-wide multi-world NPC limit ownership requires live tests.
- `SYSOP INITIALIZE` now fails safely for Classic instead of resetting/erasing all active generated monsters. Its original initialization/recovery algorithm has not been implemented.
- Reforged's broader existing native NPC AI, roaming, fleeing and scripted ownership are not made parity-complete by this spawning patch. Only the artificial generated-monster return-home teleport is disabled.

## Acceptance criteria

Build must pass; both smoke commands must pass on freshly compiled Classic data; verified live game must demonstrate River Group 21 only produces indexes 4–7, room and controller limits, room-entry and background generation, native delay after kill, fresh identity, global monster limits, monster wandering without home teleport, saved position/identity over restart, boss/direct NPC preservation, SYSOP RESET semantics, and no duplicate surviving NPCs on migration.

**Status: implementation committed, source tests passed; complete runtime parity and acceptance NOT YET VERIFIED.**
