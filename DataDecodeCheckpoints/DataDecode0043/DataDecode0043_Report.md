# DataDecode0043 — Import Readiness Validation (PreMirror)

## Purpose
Validate whether the canonical schemas and relationship candidates can be safely staged for future Reforged import without relying on the full OmniVerse mirror.

## Inputs
- Verified repository ZIP and extracted repository.
- DataDecode0032 canonical schemas.
- DataDecode0038 foreign-key reconstruction outputs.
- DataDecode0041/0042 WCC-WBB and executable correlation packages where available.

## What this pass did
- Audited canonical schema metadata and field status buckets.
- Cross-checked repository JSON datasets against canonical schema targets.
- Reviewed high-confidence foreign-key candidates for import gating.
- Hashed native source files used as import evidence.
- Cataloged repository validation/import surfaces in source and docs.
- Built a blocking issue register for anything that must not be hard-promoted before mirror/runtime proof.

## Findings
- Canonical schemas are suitable for staged import planning.
- Repository JSON files are useful as staging/test fixtures but do not supersede original binaries.
- Foreign-key candidates should remain soft references until the public-ID to binary-row bridge is proven.
- Unknown bitfields and event/script references should be imported as raw metadata or candidate annotations only.
- No noisy fields were promoted.

## Decision
Import planning can continue, but production import should keep binary evidence first and enforce hard constraints only after mirror/runtime verification.
