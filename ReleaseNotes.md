# Release Notes

## CodeUpdate0138

CodeUpdate0138 is closed locally and ready for commit/push.

Highlights:

- Extends the server runtime and host enablement baseline.
- Adds host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit surfaces.
- Expands runtime smoke coverage for live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world systems.
- Promotes MMExplorer map-qualified movement evidence through Librarium records.
- Applies MMExplorer textblock quest-flow fix 0142, promoting 83 quest-flow records into Librarium quests.
- Keeps Librarium authoritative and `/Data` generated-only.
- Keeps `ImportedDatabases/` reference-only and preserves real `Saves/`.
- The complete runtime gate now includes the 0142 textblock quest validator.

Latest local report:

- `Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md`

Verification:

- PASS 55/55 from `python Tools\Validation\run_complete_runtime_verification.py`.

GitHub Actions is pending until this commit is pushed and confirmed green.
