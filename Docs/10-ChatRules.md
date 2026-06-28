# Chat and Development Rules

- Always inspect the existing project before changing anything.
- The uploaded/GitHub project is the code source of truth.
- User-provided design summaries are the architecture source of truth when the original PDF is unavailable.
- Never recreate the project from scratch.
- Use targeted updates only unless a full build is explicitly requested.
- Code changes are Patches.
- Data/import releases are Database Versions.
- Every Patch or Database Version must run from its own extracted folder using an `Apply-*.bat` file.
- Do not require the user to manually type the project path when applying an update.
- Every Patch or Database Version must include release notes and update docs.
- Never overwrite saves or imported/reference databases.
- Maintain backward compatibility unless a migration is explicitly approved.
- Never claim a feature, executable, import, or decode is complete unless it has actually been created and verified.
- Preserve unknown fields/bytes during data conversion.
- The final server must be standalone and load from editable `Data/` files.
