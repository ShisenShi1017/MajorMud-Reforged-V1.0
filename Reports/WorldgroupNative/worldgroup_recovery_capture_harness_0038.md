# Worldgroup Native Closure Update0038 — Controlled Recovery Capture Harness

Update0038 adds a byte-preserving capture and comparison tool for the final external evidence boundary identified in Update0037. It does not reopen or change any proven MajorMUD runtime behavior.

The tool inventories `BTRVLOG.TRN`, `BTRVLOG.RCV`, Btrieve temporary files, hexadecimal transaction logs, and MKDE diagnostic artifacts. Each capture records relative path, byte length, nanosecond modification time, and SHA-256. Artifact bytes may also be copied into an isolated capture folder.

The diff command reports created, deleted, changed, and unchanged artifacts. It deliberately performs no structural parsing and assigns no undo, redo, checksum, opcode, or record-field meaning.

## Controlled use

1. Capture a clean baseline.
2. Perform one documented mutation only.
3. Capture after clean commit or shutdown.
4. Repeat from the same starting state.
5. Promote a format finding only when changed offsets and lifecycle behavior reproduce across independent captures.
6. Run forced-termination testing only against an isolated duplicate installation.

This closes the tooling gap for future evidence acquisition while preserving the Update0037 semantic-exhaustion conclusion.
