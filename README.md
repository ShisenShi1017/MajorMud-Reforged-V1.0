# MajorMUD Reforged

MajorMUD Reforged is now organized around the **Librarium**: an evidence-driven canonical knowledge graph for MajorMUD data, relationships, behavior, validation, and generated runtime output.

## Current Project Rule

**The Librarium is canonical.**

`/Data` is retained for compatibility, but it is generated runtime output. Do not make canonical gameplay changes directly in `/Data`. Update the Librarium first, then regenerate runtime data.

## Repository Layout

- `/Librarium` — canonical knowledge graph, assertions, evidence, relationships, world overlays, contracts, compiler/export model.
- `/Generated` — generated runtime data, generated docs, generated validation reports.
- `/Data` — compatibility runtime data surface generated from Librarium.
- `/Runtime` — runtime consumer code/scaffolding.
- `/Apps` — application projects preserved from the uploaded repo.
- `/Core` — existing core source preserved from the uploaded repo.
- `/Tools` — import, validation, graph, and compiler tools.
- `/Validation` — validation suites and outputs.
- `/Reports` — current and historical reports.
- `/Documentation` — architecture, handoffs, generated docs, historical validation records.
- `/ImportedDatabases`, `/Evidence`, `/KnowledgeBase` — preserved source/provenance material.

## Development Flow

1. Add or update knowledge in `/Librarium`.
2. Preserve evidence/provenance for every assertion.
3. Validate and promote evidence states only with support.
4. Generate `/Generated/RuntimeData` and compatibility `/Data`.
5. Runtime/server/editor/client consume generated output or Librarium APIs.

## Evidence States

- `Verified` — directly supported by authoritative evidence.
- `Derived` — strongly inferred from multiple supporting sources.
- `Observed` — seen in gameplay/runtime/user evidence but not fully decoded.
- `Hypothesis` — plausible but not promoted.
- `WorldOverride` — non-canonical overlay change for a specific world.
- `AIProposal` — AI-created candidate change requiring review.

## Baseline

This replacement repository was generated from the uploaded repo `MajorMud-Reforged-V1.0-main(22).zip` plus the latest Librarium/Validation baseline through Validation-0007.
