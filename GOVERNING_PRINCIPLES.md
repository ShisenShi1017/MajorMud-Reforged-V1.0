# Librarium Governing Principles

## 1. Librarium Is Canonical

The Librarium is the single source of truth for MajorMUD Reforged. Canonical content, formulas, relationships, evidence, world overlays, validation status, and runtime meaning belong in `/Librarium`.

## 2. `/Data` Is Generated Output

`/Data` exists for compatibility with runtime tools. It is not the master copy. Treat `/Data` and `/Generated/RuntimeData` as compiled/exported artifacts from Librarium knowledge.

## 3. Every Fact Requires Evidence Metadata

Every entity, relationship, assertion, formula, behavior, and world override must carry evidence state, confidence, provenance/source, relationships, and validation status.

## 4. Relationships Are First-Class Knowledge

A relationship is not just a link. It is a knowledge record with its own evidence, confidence, provenance, world applicability, and runtime consumers.

## 5. Runtime/Server/Editor Are Consumers

Runtime, server, editor, client, validation tools, and AI systems must not invent canonical game truth independently. They consume Librarium records, generated runtime output, or approved APIs.

## 6. World Overlays Do Not Corrupt Canon

Classic data remains the base graph. Reforged, custom worlds, test realms, and AI worlds extend the base through overlays. Overlay changes must be explicit and reversible.

## 7. Promotion Requires Evidence

Knowledge moves through the lifecycle: `Hypothesis → Observed → Derived → Verified`. Promotion requires recorded evidence. Do not silently promote assumptions.

## 8. Source Artifacts Are Provenance

Imported databases, MMExplorer/MBBSEmu/reference artifacts, validation reports, runtime traces, docs, and user-confirmed observations are provenance sources. They should be indexed, not discarded.

## 9. Codex/Agents Must Preserve This Architecture

Future automated work must keep the Librarium-first architecture. Do not rewrite runtime/server logic as a separate source of truth.
