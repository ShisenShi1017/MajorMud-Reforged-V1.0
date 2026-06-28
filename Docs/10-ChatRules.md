# Chat and Development Rules

- Treat the uploaded GitHub ZIP/project as the code source of truth.
- Treat user-provided summaries as the architecture/design authority when the original PDF is unavailable.
- Database versions are for converted/imported game data.
- Code patches are for server/editor/client code changes.
- Every database version and patch must update documentation.
- Every official update must run from its own extracted folder.
- Every official update must include an `Apply-*.bat` file.
- Do not require the user to type the project path when avoidable; the apply script should locate the project root.
- Never overwrite `Saves/`.
- Never overwrite `ImportedDatabases/`.
- Never claim full import/decoding is complete unless every table/file/field/record is verified.
- Numeric fields in `.DAT` files must be labeled carefully: counts, IDs, flags, skills, offsets, references, and unknowns are different things.
