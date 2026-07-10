# MajorMUD Reforged Version

Current repository baseline: **CodeUpdate 0135 - Final Exhaustive Classic Fixes**

Status: CodeUpdate0135 is green by user report. Internal CodeUpdate0136 work has started with the Classic kill-time drop percent roll repair. Local Codex verification passed. GitHub Actions remains pending until the user pushes this internal 0136 commit and reports green.

Current internal 0136 scope: Classic kill-time loot drops use a full-width integer `1..100` roll and succeed on `roll <= drop_percent`. This fixes the rounded endpoint-bucket bug that under-dropped 1% and 3% loot such as orc-heads.

Boundary: 0135 does not complete Classic single-player gameplay. CodeUpdate0136 is the active internal milestone for character creation/selection, inventory/equipment, full world generation, NPC lifecycle/AI, combat rounds, magic/statuses, quests/triggers/events, shops/economy, persistence, and final exhaustive verification. No partial public CodeUpdate0136 should be created.

Local verification passed: `python Tools/Validation/validate_codeupdate0136_classic_drop_roll.py` and `python Tools/Validation/run_complete_runtime_verification.py`; complete runtime verification 50/50 PASS.

GitHub Actions remains pending until user push and green report.
