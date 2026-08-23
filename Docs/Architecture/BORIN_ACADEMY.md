# Borin Academy architecture

Date: 2026-08-21

## Authority and storage

`BorinAcademy` is curriculum and evaluation orchestration over the existing `BorinKnowledgeCore`. It does not own MajorMUD or Reforged facts. Its JSON ledger at `AI/Memory/Knowledge/borin-academy.json` stores only generated-question metadata, attempts, component scores, failure classifications, knowledge gaps, historical evidence references, activity, and idle-training settings.

Lightning and Ultra receive the same provider-neutral `BrainContext` and retrieved Knowledge Core evidence. Basic and relationship questions use normal routing; multi-hop, comparison, architecture, diagnostic, and conflicting-evidence prompts are Ultra-eligible through the existing tier selector. Academy calls are classified as background requests by the shared NVIDIA rate limiter and cannot consume the interactive burst reserve.

## Implemented learning loop

1. A first training run bulk-builds the existing source/Roslyn and generated MajorMUD indexes when they are absent.
2. The generator rotates across factual recall, relationship, multi-hop, source trace, comparison, diagnostic, and runtime-verification questions for all 31 curriculum subjects.
3. Expected evidence remains local. Borin receives the question and a bounded current evidence package, not a stored answer.
4. The evaluator separately scores correctness, completeness, source quality, retrieval accuracy, confidence accuracy, relationship reasoning, and hallucination penalty.
5. Full factual credit requires current provenance-bearing local evidence. Claimed paths not present in the retrieved evidence fail the hallucination gate.
6. Failed or unsupported attempts create persistent gaps with a classified cause and recommended investigation.
7. Open Knowledge Core conflicts automatically become comparative investigation gaps. Runtime/Test Realm gaps cannot be resolved from source evidence alone.
8. Explicit user corrections become verification candidates; they do not overwrite authority. User-confirmed statements retain that provenance but still request corroboration.
9. Historical replay scans existing logs, reports, test results, and Borin engineering ledgers. Only evidence carrying successful verification markers becomes a referenced investigation episode and trusted replay procedure.
10. Successful live engineering work continues to feed the existing verified-learning store. Normal play continues to feed runtime observations and conflicts.
11. When either source or generated MajorMUD index changes, old attempts remain historical but are excluded from current mastery percentages until retested.

## Scheduling and safety

- Manual training and clean mastery evaluation are asynchronous and cancellable.
- Idle training runs one bounded question per idle interval, pauses during user work, autonomous play, active repair stages, or constrained shared quota, and is capped at ten configured requests per minute.
- Priority remains user request, gameplay, developer repair, Sysop work, Academy, then idle gap hunting.
- Gap resolution requires evidence. No model response is promoted directly into factual authority.
- Existing production, Test Realm, filesystem, patch, and incorporation permissions remain unchanged.

## Forge UI

The existing Forge screen now includes a Borin Academy section with current MajorMUD/Reforged measured mastery, verified facts, gaps, conflicts, unverified claims, tested subjects, current activity/model, Train, Test Mastery, Investigate Gaps, Replay History, and persistent idle-training controls.
