# Chat and Development Rules

These rules let future chats continue development without losing context.

1. Treat the uploaded GitHub ZIP or repository as the code source of truth.
2. Treat user-provided summaries as the project vision authority when the original PDF is unavailable.
3. Use `Database Version` for data/import releases. Use `Patch` for code changes.
4. All update packages must run from the extracted package folder.
5. Apply scripts must auto-locate the project root and must not require manual path entry.
6. Every update package must update docs, release notes, audit/history, and session handoff.
7. Imported databases/native runtime files are reference sources. The standalone server must run from `Data/`.
8. Never overwrite saves or user-owned imported source archives.
9. Ask before changing architecture when information is missing.
10. Be honest about incomplete decoding or unverified data.
