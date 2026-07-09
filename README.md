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

## Current Runtime Status

CodeUpdate 0114 applies Runtime Milestone 1. The server now has an opt-in Classic-first runtime path with local console mode, telnet/network mode, multi-session tracking, login name prompt, world selection foundation, runtime world loading from generated `/Data`, command dispatch, and scheduler shell.

GitHub Actions server build automation has been restored under `.github/workflows/dotnet-server.yml` and is configured to run on every branch push, every pull request, and manual dispatch. The official gate remains GitHub Actions after the user pushes local changes.

Runtime Milestone 1 does not add combat runtime, persistent accounts/characters, saves, AI worlds, custom worlds, formula changes, or non-Classic gameplay expansion.


## CodeUpdate 0082 — Timing Constant Verification

Added evidence-backed timing-constant claim records for 3.0s system/spell tick, 1.5s combat round, tick/round ratio, and tick clipping. No promotions or runtime replacements. Status: PASS_WITH_LIMITATIONS.

## CodeUpdate 0083

Performed internal timing evidence extraction across WCC/WBB imports, imported disassembly evidence, MMUD Explorer, and MBBSEmu. Host/module-driven timing is now internally corroborated. Exact Classic HP/mana/buff tick and combat-round constants remain unverified; MMUD Explorer introduces a conflicting 5-second combat-round modeling constant. No runtime formulas were replaced.
