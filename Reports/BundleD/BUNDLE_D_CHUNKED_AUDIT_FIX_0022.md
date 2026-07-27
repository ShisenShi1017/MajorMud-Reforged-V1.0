# Bundle D Revision 0023 — Chunked Audit Compatibility

Updates Bundle D audits to consume chunked `rooms.json` and `exits.json` manifests through all listed record parts.

Affected audits:
- World authority
- Rooms
- Hidden objects
- Teleporters
- Maps

The shared reader validates manifest record counts and fails if any listed chunk is missing. Monolithic array and object-with-records catalogs remain supported for backward compatibility.
