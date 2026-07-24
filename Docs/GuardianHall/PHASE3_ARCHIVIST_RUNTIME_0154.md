# Guardian Hall Phase 3-0154 — Archivist Runtime

The Archivist workbench now provides a bounded, preview-only evidence pipeline.

- Queued files can be decoded individually or in bulk.
- Every decoded file receives SHA-256 identity, byte size, status, findings, and confidence.
- JSON and XML are structurally parsed; CSV/text and binary formats are safely sampled.
- DLL inspection verifies the executable header and defers deeper native analysis to the editable local-tool registry.
- Evidence artifacts and snapshots are stored under the Guardian Hall Archivist workspace.
- Relationship summaries are derived from matching evidence names and types.
- No Librarium file is written or replaced.
