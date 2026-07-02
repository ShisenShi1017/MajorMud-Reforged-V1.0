# Codex Handoff — DataSimVer0017

Continue from MajorMUD-Reforged-DataSimVer0017-Cumulative.zip.

Focus completed: cleanup/hidden-item/search expected model.

Evidence-based fields:
- rooms.json: roomItems, hiddenItemIds
- skills.json: search, governingAttributes agi/int

Important guardrails:
- `/Data` unchanged.
- Cleanup timing and hide/delete behavior are documented hypotheses only.
- Search formula is expected-model only; original runtime formula requires engine/MBBSEmu evidence.
- No room endpoints or special-exit semantics promoted.

Recommended next pass: DataSimVer0018 reset scheduling/world refresh policy.
