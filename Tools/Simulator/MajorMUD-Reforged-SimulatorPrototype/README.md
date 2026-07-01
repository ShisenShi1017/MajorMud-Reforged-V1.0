# MajorMUD Reforged Evidence-Backed Simulator

This package contains an implemented simulator shell built from the verified RuntimePrep0001-0009 evidence chain.

It is **not Worldgroup** and does **not** certify live gameplay equivalence. It runs an expected behavior model derived from MDB/CSV/WBB/WCC/static RuntimePrep outputs.

## Quick start

```bash
python run_simulator.py summary
python run_simulator.py play
python run_simulator.py simulate-room map1-room101 --ticks 1
python run_simulator.py room map1-room101
python run_simulator.py search-room "Silver Street"
python run_simulator.py exits AADE
python run_simulator.py path AADE ASYL
python run_simulator.py shop 65
python run_simulator.py search-shop sword
python run_simulator.py monster goblin
python run_simulator.py drops 184
python run_simulator.py npc quest
python run_simulator.py spell missile
python run_simulator.py skill slash
python run_simulator.py search justice
```

## Interactive mode

Run:

```bash
python run_simulator.py play
```

Useful commands inside the shell:

```text
summary
look
enter map1-room101
mobs
attack mayor
tick 5
rest
nav AADE
exits
go s
path AADE ASYL
shop 65
buy slow 65
inventory
search quest
monster goblin
drops 184
npc justice
spell missile
skill slash
backlog
quit
```

## Implemented features

- Evidence-backed data loader for RuntimePrep0001-0009 payloads.
- Room lookup by room key, number, name, or partial name.
- Expected room population spawning from RuntimePrep0002.
- Boss/unique monster display where configured.
- Navigation graph queries and interactive navigation-node movement from RuntimePrep0003.
- Shop catalog and inventory browser from RuntimePrep0004.
- Expected item buy/add action for simulator inventory.
- Monster drop lookup and approximate drop rolling from RuntimePrep0005.
- NPC/dialogue/quest/script text search from RuntimePrep0006.
- Combat placeholder using RuntimePrep0007 combat rows where available, with conservative fallback values for monsters not present in the combat matrix.
- Spell, skill, class, and equipment query support.
- RuntimeValidation backlog display from RuntimePrep0009.

## Boundary

This simulator supports **expected-model play and inspection**. It does not prove:

- Original Worldgroup AI decisions.
- Exact combat formulas.
- Script execution order.
- Actual spawn/lair timing.
- Runtime quest state mutation.
- Hidden engine quirks.

Those remain pending live RuntimeValidation once Worldgroup or an equivalent runtime is available.


## SimulatorBuild0002 — Unknown Resolution & Evidence Trace Mode

Added commands:

```bash
python run_simulator.py unknowns
python run_simulator.py find-unknown loot
python run_simulator.py simulate-unknowns
python run_simulator.py export-trace
```

Unknowns are represented as placeholders or runtime-required deferred checks. The simulator does not infer missing facts.
