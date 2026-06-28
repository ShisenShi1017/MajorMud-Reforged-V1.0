# AI Development Guide

## Required First Steps

Before implementing anything:

1. Inspect the latest uploaded repository.
2. Read `AI_CONTEXT.md`.
3. Read `Docs/Architecture/PROJECT_ROADMAP.md`.
4. Read `README.md`, `VERSION.md`, `CHANGELOG.md`, and `ProjectManifest.json`.
5. Identify the latest Code Update.
6. Continue from that point.

## Development Behavior

- Make repository-grounded changes only.
- Do not invent file contents when existing files are available.
- Do not claim work was completed unless files were changed and verified.
- Keep update packages self-contained.
- Preserve user data and imported archives.

## Suggestion Policy

All approved suggestions must be implemented in the next applicable update unless explicitly deferred or rejected by the user. Completed suggestions must be reflected in the changelog and manifest.

## Packaging Behavior

Do not provide a download link until the ZIP exists and has been inspected.

## CodeUpdate-0010 Handoff and Update Folder Rule

Added in CodeUpdate-0010:

- `AI_CONTEXT.md` is a living handoff document and must be updated with every Code Update.
- The handoff must include the current milestone, update number, completed/substantially-underway table, Milestone 1 completion checklist, remaining work, and next recommended work.
- Every Code Update and Database Version ZIP must contain exactly one labeled top-level folder named for the update.
- The user must be able to extract the ZIP directly into the main project folder without manually creating a folder.
- The `Apply-*.bat` file must live inside that labeled update folder and run from there.
- The apply script must automatically locate the project root before copying payload files.
- Approved suggestions are implementation requirements unless explicitly deferred or rejected.

