# Librarium Governing Principles

Status: Active project law  
Created: 2026-07-08T00:09:22.782731Z

## 1. Librarium is canonical

The Librarium knowledge graph is the authoritative source of truth for MajorMUD Reforged.

The previous `/Data` folder is no longer treated as the master canonical source. `/Data` becomes generated runtime output produced from Librarium records, evidence, world overlays, and compiler/export rules.

## 2. Runtime/server/editor are consumers

Runtime, server, client, editor, validation tools, and AI worlds must consume Librarium records. They must not invent or silently hardcode game truth.

## 3. Every fact requires evidence

Every entity, relationship, formula, behavior, and world override must carry:

- Evidence state
- Confidence
- Provenance
- Source artifact(s)
- Runtime consumers
- Validation status

## 4. Evidence states

Allowed evidence states:

- Verified — directly supported by canonical data or convergent evidence.
- Derived — strongly inferred from multiple evidence records.
- Observed — seen in gameplay/runtime/user-confirmed observation but not fully decoded.
- Hypothesis — proposed but not promoted.
- WorldOverride — explicit world-layer change from Classic base.
- AIProposal — AI-generated candidate change requiring approval.

## 5. Relationships are first-class records

Relationships are not disposable links. A relationship such as `White Gold Ring grants Ability 123` is a knowledge assertion with provenance, confidence, world applicability, consumers, and validation history.

## 6. World overlays do not corrupt Classic

Classic Base Graph remains immutable except by verified canonical promotion. Reforged, Custom World 1, Custom World 2, AI World, Test Realm, and Live Runtime State are overlays layered on top of the base graph.

## 7. Promotion is explicit

Knowledge moves through:

Hypothesis -> Observed -> Derived -> Verified

Promotion requires recorded evidence. No generated runtime output should be treated as canonical proof by itself.

## 8. Formula records are knowledge objects

A formula is not only an equation. It is a knowledge object with inputs, outputs, dependencies, consumers, candidate equations, conflicts, confidence, exact-equation status, and provenance.

## 9. Generated output is disposable

Runtime data, server config, editor data, documentation, validation indexes, and client resources are compiled/exported artifacts. If they disagree with the Librarium, the Librarium wins.

## 10. Codex implementation rule

When Codex updates the repo, it must preserve this architecture:

Repository evidence -> Librarium -> Generated Runtime Data -> Runtime/Server/Client

Do not restore `/Data` as the master source of truth.
