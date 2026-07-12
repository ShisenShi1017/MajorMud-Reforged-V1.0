# Changelog

## CodeUpdate0138 - Server Runtime and Host Enablement

- Closed the local CodeUpdate0138 baseline.
- Integrated server runtime and host enablement surfaces.
- Added/updated host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit coverage.
- Integrated live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world-system smoke surfaces.
- Promoted map-qualified MMExplorer movement records through Librarium and compiler-generated `/Data`.
- Applied MMExplorer textblock quest-flow fix 0142.
- Updated Librarium quests to 2,183 total quest records, including 83 `npc_textblock_quest_flow` records.
- Added the 0142 textblock quest validator to the complete runtime verification gate.
- Consolidated active handoff docs to the compact root/docs surface.
- Removed copied update package folders, generated Python caches, compiler smoke output, and smoke-save artifacts from the working tree.

Verification:

- `python Tools\Validation\run_complete_runtime_verification.py`
- PASS 55/55
- Latest report: `Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md`

GitHub Actions is pending until pushed and confirmed green.
