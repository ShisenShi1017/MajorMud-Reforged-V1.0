# CodeUpdate-0030 - Truth Documentation Cleanup and Agent Handoff Rules

This update cleans the project handoff rules and creates a root `AGENTS.md` file so ChatGPT, Codex, and future sessions follow the same instructions.

## Changed

- Added `AGENTS.md`.
- Updated `AI_CONTEXT.md`.
- Updated `README.md`.
- Updated `CHANGELOG.md`.
- Updated `VERSION.md`.
- Updated `ProjectManifest.json`.
- Updated `Docs/Architecture/PROJECT_ROADMAP.md`.
- Updated `Apps/Server/README.md`.
- Added `Reports/CodeUpdates/CodeUpdate-0030/CodeUpdate-0030-Truth-Documentation-Cleanup.md`.

## Rules Recorded

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the source of truth.
- Each chat window must identify whether it is Codex mode or ChatGPT mode before making changes.
- Codex updates local only and waits for user push plus stated green GitHub Actions before continuing.
- ChatGPT uses ZIP patch workflow for local updates unless explicitly instructed otherwise.
- Never guess when creating, fixing, packaging, reviewing, or reporting updates.

## Notes

- No source implementation code is added.
- No gameplay features are added.
- No combat, character creation, world simulation, editor implementation, or networking expansion is added.
- GitHub Actions remains the official validation gate after the user applies, commits, and pushes this update.
