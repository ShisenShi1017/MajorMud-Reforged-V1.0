# Current State

Completed evidence phases:

- DataDecode0101–0110: static decode / imported database verification lineage.
- RuntimePrep0001–0009: runtime preparation evidence lineage.
- SimulatorBuild0001–0020: simulator framework and full systems matrix lineage.
- Build0021: Behavioral Recovery / formula mining.
- Build0022: Recovery Completion combined pass.
- Build0023: Final Static Pass covering engine logic, world validation, hidden data, simulator gap analysis, and final audit.
- Build0024: Simulator Behavioral Matrix for archetypes/hybrids and scenario coverage.
- Build0025: Formula Isolation Matrix for sensitivity-model testing.
- Build0026: Last-two-passes retest package; useful as a structural/log artifact but must be interpreted conservatively.
- Build0027: Real executable simulator test package; use only the actual logs/files inside the ZIP, not chat summaries, as evidence.

Project status:

- Static data/content recovery: effectively complete.
- Entity/world/catalog recovery: effectively complete.
- Simulator framework: present and partially runnable.
- Expected-model matrices: present for Build0024 and Build0025.
- Original runtime validation: unavailable.
- Remaining behavior work: convert expected-model rows into real executable simulator tests and implement missing formula hooks where supported by source/data.
