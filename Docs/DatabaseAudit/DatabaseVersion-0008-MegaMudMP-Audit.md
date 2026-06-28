# Database Audit - Database Version 0008

## Scope

Integrated companion client `.mp` files as human-readable map/navigation evidence.

## Counts

```json
{
  "databaseVersion": "0008",
  "title": "companion client MP Knowledge Integration",
  "generatedUtc": "2026-06-28T05:01:25.171491Z",
  "mpFiles": 1957,
  "roomsCsvRecords": 26666,
  "mpRoomReferences": 2919,
  "uniqueMpRoomReferences": 648,
  "mpRoomReferencesMatchedToRoomsCsv": 2911,
  "exactNameMatches": 803,
  "duplicateRoomReferencesAcrossMpFiles": 611,
  "uniqueHumanCommands": 430,
  "parseWarnings": 116,
  "note": "companion client .mp files are used as evidence for map/room/navigation knowledge; numeric route fields remain unknown until decoded against DAT/runtime behavior."
}
```

## Duplicate policy

No generated JSON record filenames are duplicated because each source `.mp` file becomes exactly one record named after its source file stem.

Duplicate room references across multiple `.mp` files are not automatically errors. They are recorded in `Data/companion clientMaps/Index/duplicateRoomReferences.json` for review because different routes may legitimately reference the same room.

## Unknown numeric field policy

Numeric route fields remain unknown until proven. They may represent coordinates, packed direction masks, room pointers, flags, skills, or command metadata. No field is promoted to a named meaning without evidence.
