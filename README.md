# MajorMUD Reforged

Decoded Map 17 boulder failures execute their complete native chain: message 3309 followed by level-scaled self-damage spell 1324 with persistent HP mutation.

CodeUpdate0139 includes reproducible WCCMSG2 output: 3,869 logical messages retain all three native channels, `message` and `text` namespaces are distinct, and decoded pedestal/boulder output is evidence-bound.

Classic commerce is fully data-connected across generated shops, banks, trainers, item prices, markup, persistent stock, and decoded regeneration rules.

The Classic runtime now has exhaustive spell/status family coverage: all 1,350 generated spells, 105 decoded ability IDs, and 111 status effects are mapped to explicit runtime consumers or structural control behavior.

CodeUpdate0139 includes complete decoded `remoteaction` runtime coverage: all 85 signatures from 139 textblock invocations compile into the existing consolidated Classic room-interaction data.

MajorMUD Reforged is a standalone, data-driven game engine and toolset.

## Current Status

Read [Docs/PROJECT_STATUS.md](Docs/PROJECT_STATUS.md) for the live project handoff.

Short version:

- Current Code Update: `0139`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0139 - Telnet Runtime Cohesion`.
- Local verification: PASS current-health validator 13/13, Classic compiler/runtime audits with 26,668/26,668 room descriptions, interaction/narrative smoke with 83 executable quest entries, Reforged app Release build, and Reforged app publish to `Release/MajorMudReforged`.
- CodeUpdate0138 is green by user report.
- GitHub Actions is pending for CodeUpdate0139 until the pushed commit is confirmed green.
- Librarium is authoritative.
- `/Data` is generated runtime output.
- Clean CI/local checkouts regenerate `/Data` from Librarium before server bootstrap; generated `Data/Classic` is not committed as source.
- Generated runtime readers support compiler chunk manifests; optional NPC AI runtime relationships can be derived from generated spawns.
- Classic is evidence-locked.
- `ImportedDatabases/` and real `Saves/` must be preserved.
- Guardian Hall Engineering exposes all ten engineering phases through a guarded queue with independent controls for planning, engineering, builds, validation, runtime/gameplay testing, optimization, documentation, candidate ZIP generation, repository updates, and overnight operation. Candidate ZIP generation defaults on; autonomous and live-update capabilities default off, and Emergency Stop cancels execution without changing saved preferences.
- Borin accepts a persisted Engineering Objective and includes a one-click **Fix Runtime Disconnects** assignment preset.
- With Autonomous Engineering enabled and a Forge coding model selected, Borin can author and safely apply repair diffs inside the isolated worktree; later enabled gates test them and Candidate ZIP Generation packages the actual changed files.

## Verified Runtime Scope

- Character Foundation: generated races/classes, 195/195 race/class combinations, ready creation gate, map-qualified Classic start, and character save/reload smoke.
- Items and equipment: generated item/equipment data plus runtime smoke coverage for inventory, equipment, containers, corpses, loot, validation, rollback, and persistence.
- Server runtime smokes: generated data, room/exits, NPCs, spells, spell execution, combat, combat runtime, actors/abilities, interaction/narrative, services/economy, world/multiplayer, and scripted local console session.
- Host enablement: host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit surfaces.
- Classic repairs: drop-roll uniformity, mask face-slot correction, Lyrist's Companion spell binding, bard shop minimum, cloaked figure XP, and related runtime contracts.
- Librarium data promotion: map-qualified movement records and 0142 textblock quest-flow records are promoted through Librarium, then generated into `/Data`.
- Telnet runtime cohesion: connected live combat targeting, combat damage, spell/ability NPC effects, and movement guards to shared authoritative living-world runtime state.
- Classic runtime audit: diagnostic commands prove where generated data, runtime loading, live-world materialization, room inspection, and spawn materialization succeed or fail.
- Current audit finding: regenerated live `Data/Classic` has 26,668/26,668 room descriptions; runtime loads 26,668/26,668 descriptions, 1,015 live NPC instances, and 628 live monster instances with hard map-qualified placement validation.
- Room-text recovery: Librarium contains 2,417 clean native-backed room descriptions, 800 lower-priority OmniVerse map-qualified reference gap fills, 23,309 WCCMP002 default-install binary gap fills, and 140 WCCMP002 placeholder room recoveries. WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this pass, with WBBMP002 as secondary corroboration only.
- WCC exhaustion audit: `Reports/DataDecode/wcc_librarium_exhaustion_audit.md` records available local WCC/WBB evidence against active Librarium coverage.
- Reference reconciliation: `Reports/DataDecode/mmexplorer_omniverse_librarium_reconciliation.md` records MMExplorer SQL/MDB and OmniVerse public codex gap fills promoted into Librarium with provenance.
- Command parsing: runtime commands now use centralized exact alias, unique-prefix, and ambiguity-rejection rules for Telnet/MegaMUD input.
- Spawn placement: live NPC materialization now requires exact map-qualified room resolution, skips unresolved spawns, and room rendering displays only room-scoped living NPC instances with disposition-based color.
- Placement audit: `room.map1.21` currently proves one neutral `guardsman` from `spawn.21.5`; the user-expected `elite guardsman` is a separate data correction if the generated source field is wrong.
- Room decode closure: exact decoded evidence promotes the sphinx, rubble, ten Map 17 boulder, four pedestal-item, and final `stars` sphinx chains. Runtime now binds all 206/206 action gates with zero native/passive placeholders.
- Room message catalog: 117 unique native WCCMSG offset-backed records now cover room actions, doors, hidden exits, lock/key failures, search successes, and generic failures.
- Quest dialogue dispatch: Librarium now generates 182 decoded textblock stage scripts and 83 NPC quest-entry triggers; `talk <npc>` dispatches through room-scoped live NPCs and applies decoded quest flags, item consumption/rewards, experience/alignment, and response branches atomically.
- Quest/script edge closure: generated random textblock tables execute through decoded threshold bindings; generated cast/summon/teleport/remoteaction targets validate before commit; quest teleports update the active session/character; summon intents materialize generated NPCs; remoteaction and room/object commits are recorded as durable living-world events; exhaustive generated NPC quest-entry dispatch validation passes.
- Final native closure/WCC decode intake: native decode evidence, reports, tools, manifests, Librarium/reference files, `Native*` registries, and active server runtime slices are imported; duplicate runtime definitions were merged/replaced.
- Verified/strong Librarium activation: strongly-correlated sidecar records now compile into generated runtime data, generated triggers contain 367 strong records, and unresolved exit targets are blocked by compiler cross-family validation.
- Server/native-runtime audit repair: compiler room-description catalog discovery now includes plain `*_records.json` files, generated quest/item numeric strings are read safely by runtime catalog loaders, and remaining native decode gaps are tracked in `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md`.
- Bundle G native presentation closure: prior F10 native payload disconnects are resolved with explicit no-output decoded text mappings and exact WCCMSG payload evidence for `message 2044` and `message 3290`; F10 native payload through F15 persistence audits pass locally.
- Native presentation reproducibility closure promotes all 3,467 verified WCCTEXT2 payloads into Librarium; clean compilation now regenerates the single payload runtime file instead of deleting an ignored prerequisite.
- Reforged app Librarium tab: the app now exposes a read-only searchable Classic data browser with tabs for Weapons, Armor, Spells, Abilities, Classes, Races, Items, Monsters, Quests, and Rooms. The left list shows names only. Classes and races use the richer character-foundation gameplay projection for abilities, magic/armor/combat info, and race min/max stats. Quests show nontechnical Host guidance such as who to talk to, what to say, needed items, and plain-language steps. The published EXE is `Release/MajorMudReforged/MajorMudReforged.exe`.
- Reforged app Client tab: the Play workspace now behaves as a server/session launcher. The play surface opens in a dedicated MegaMUD-style terminal window with black terminal output, command input, menu tools, and a separate tabbed Client Options window.

## Development Flow

```text
Librarium -> generated /Data/<world> -> Server runtime -> read-only client view
```

Do not hand-edit generated `/Data` as canonical source. Update Librarium/source compilers, regenerate data, then verify.

One-time validation proofs are recorded in ValidationManifest.json.
WCCMMUD online administration is exposed through authenticated `SYSOP` dispatch backed by generated Librarium data. The legacy SYSOP unlock value is configuration evidence and is never entered as a gameplay command.
