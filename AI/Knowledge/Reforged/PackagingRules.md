# Update ZIP Packaging Rules

- Produce an update-only ZIP containing changed and newly created files, not the full repository.
- Place everything inside one top-level update folder and preserve repository-relative paths.
- Include one combined `Install-And-Validate.bat` at the top of that folder.
- The installer must display an unmistakable SUCCESS or FAILED result, show the full log path, and pause on both outcomes.
- Do not include GGUF models, credentials, memory ledgers, knowledge-index state, build output, release binaries, or workspace snapshots unless explicitly requested.
- Current project preference is no backup and no rollback unless the user explicitly requests them.
- Never claim a package built or validated unless the actual installer/build/validator evidence proves it.
