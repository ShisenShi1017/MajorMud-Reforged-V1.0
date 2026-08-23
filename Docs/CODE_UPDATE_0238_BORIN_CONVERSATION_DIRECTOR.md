# Code Update 0238 — Borin Conversation Director

## Purpose

This cumulative pass builds on Code Update 0237 and adds a model-independent conversation director between Borin's persistent living-character state and his direct llama.cpp/LLamaSharp inference path.

The director does not replace the local GGUF brain and does not generate dialogue itself. It prepares a compact turn plan so the model spends its limited context on the current person, topic, and relevant memories instead of receiving every recalled project memory indiscriminately.

## Added behavior

- Classifies ordinary and external conversation into natural modes such as greeting, check-in, correction, support, celebration, storytelling, opinion, planning, follow-up, and explanation.
- Resolves short follow-ups against Borin's current thread before treating them as new topics.
- Selects no more than three genuinely relevant saved memories for a conversational reply.
- Prevents unrelated project memories from dominating casual conversation.
- Gives Borin an invisible opening, pacing, emotional, continuity, and follow-up-question plan.
- Limits completed history turns according to the current conversation mode.
- Teaches correction turns to accept the corrected premise without defensiveness.
- Keeps external-player conversations within their existing trust, privacy, command, and output contracts.
- Preserves direct GGUF inference and the persistent character systems from Code Update 0237.

## Cumulative payload

This package contains every source file required by Pass 0001 plus the new conversation director and this document. It contains no AI model, AI configuration, credentials, memory JSON, release binary, or rollback payload.

## Expected observable result

Borin should be more likely to:

- answer a one-word or short follow-up in the context of what was just said;
- respond to emotion before dumping instructions;
- give a recognizable opinion when asked;
- tell a compact story instead of a report;
- acknowledge a correction and move forward;
- avoid asking a needless question at the end of every reply;
- avoid bringing unrelated repository details into ordinary chat.
