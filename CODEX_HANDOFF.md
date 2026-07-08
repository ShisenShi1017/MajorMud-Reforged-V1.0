# Codex / Developer Handoff

## Immediate Rule

Work from this repository as a complete PC replacement baseline. The project is now Librarium-first.

## Do Not

- Do not treat `/Data` as canonical.
- Do not add game rules directly to runtime/server code without a Librarium record.
- Do not discard provenance.
- Do not flatten world overlays into Classic canon.

## Do

- Add records/assertions/relationships in `/Librarium`.
- Keep evidence state, confidence, and provenance attached.
- Generate runtime data from the Librarium.
- Preserve existing app/core/tooling folders unless a migration report explicitly marks them obsolete.

## Next Recommended Work

1. Implement/refine Librarium compiler/export tooling.
2. Generate runtime data from Librarium records.
3. Continue formula reconciliation and quest/dialogue ordering validation.
4. Only after Librarium export is stable, integrate runtime/server consumers.

## Baseline Inputs

- Uploaded repo: `MajorMud-Reforged-V1.0-main(22).zip`
- Latest knowledge baseline: `Validation-0007 Final Librarium Completion / Compiler Foundation`
