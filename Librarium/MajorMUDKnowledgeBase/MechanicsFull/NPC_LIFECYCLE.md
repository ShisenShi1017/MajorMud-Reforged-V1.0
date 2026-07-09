# NPC lifecycle

**Mechanic ID:** `NPC.LIFECYCLE`  
**Bucket:** NPC AI  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Spawn, death, despawn, respawn, and cleanup behavior.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 895
- Representative evidence entries: 36
- Source/path coverage: 10

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 273 — `-UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 179 — `-UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 261 — `-UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 124 — `roamSecs        As Double   'Seconds waiting for respawns (per-cycle, rarely used)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 465 — `'   -> No movement or recovery; cycle time = respawn time.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 581 — `'   - Instant respawn (stand-in-place): no movement.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 1487 — `' Lairs respawn on the wall-clock minute, so the real wait is the stated`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2179 — `'       Spawn-gating waits when the loop outruns respawns`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2211 — `'       - Scale for respawn throughput: compute effectiveLairs & roomsScaled;`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 3534 — `'       Spawn-gating awareness when the loop outruns respawns.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 3605 — `'       - **Instant-respawn micro-floor:** if final loop < MIN_LOOP on bInstant routes,`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 4487 — `'===== Final loop time (with instant-respawn micro-floor) =====`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 83 — `2688|0|0|(this is just the last part) dao lords death spell casts another spell which executes this block.  it has the flag checks, exp gain, and giving the item.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 302 — `9518|0|9519|flag check and gives gem - executed by death spell of the "Death Must of Ozrinom"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 317 — `9537|0|9483|exp and flags - executed by dark mage's death spell`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 476 — `9516|0|9517|death spell of hanging tree -- flag check, give item`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 498 — `9537|0|9483|death spell of dark mage -- flag check, give 175 mill exp bonus`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 503 — `9547|0|0|death spell connected to text block -- flag check, add 50 mill exp bonus`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 860 — `1417|0|0|from death spell, flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 862 — `1460|0|0|from death spell, flag check but no update`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 864 — `1418|0|0|from death spell, flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 899 — `1443|0|0|from death spell, flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 901 — `1444|0|0|from death spell, flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 903 — `1445|0|0|from death spell, flag check`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 66 — `4. **Host Process** (`HostProcess/`): `MbbsHost` manages module lifecycle, session scheduling, and execution units. `ExecutionUnit` wraps CPU contexts for concurrent module operations.`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
