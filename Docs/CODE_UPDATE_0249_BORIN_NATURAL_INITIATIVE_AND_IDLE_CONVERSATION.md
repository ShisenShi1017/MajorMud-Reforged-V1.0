# Code Update 0249 — Borin Natural Initiative and Idle Conversation

## Purpose

This cumulative pass allows Borin to speak first occasionally without turning him into a noisy notification bot. It adds a persistent, surface-aware initiative gate for private Forge conversation and Borin's external MajorMUD character.

The initiative gate decides whether silence should be interrupted before the local GGUF model is asked to form a line. Silence is the default. The model may also return `[NO_REPLY]` when no useful thought is worth saying.

## Forge behavior

Borin may add one short private thought in the Forge only when:

- William and Borin already have a conversation history;
- the local GGUF model is already loaded;
- the Forge has been quiet for at least eight minutes;
- William is not typing and the Forge is not processing another request;
- Borin is not already waiting for William to answer a direct question;
- the 30-minute cooldown and one-per-hour limit are clear; and
- Borin has not already spoken first and been left waiting.

A new user message cancels an in-progress proactive generation. The generated line is discarded if William begins typing or starts another task before it is displayed.

Forge initiative never performs repository scans, edits files, runs tools, starts builds, changes settings, or records a synthetic user turn in Borin's ordinary conversation memory.

## MajorMUD behavior

External initiative is private by design. Borin may telepath the configured primary teacher after a safe quiet interval, but he does not broadcast unsolicited `SAY` messages into a public room.

The external initiative gate requires:

- the external player profile and conversation feature to be enabled;
- a verified MajorMUD login and live session;
- an established prior conversation with the recipient;
- no active combat, movement, danger-level health, local repository audit, or busy public conversation;
- no newer incoming message while the local model is forming the line;
- the configured quiet interval, cooldown, and hourly limit; and
- no unanswered earlier proactive message.

The default external values are five quiet minutes, a 20-minute cooldown, and no more than two proactive telepaths per hour. These values persist in `AI\Config\borin-player.json` and remain bounded by normalization.

Every proactive in-game line passes through the existing final speech cleaner immediately before `TELEPATH`. The hard 270-character server/client limit remains absolute; the initiative prompt targets 220 characters or fewer.

## Verified events

Meaningful verified client events such as entering a room, a failed movement, a purchase, equipment change, combat transition, or rest transition may provide a relevant subject for a later private initiative. Raw public conversation is not stored as an initiative event, and a remembered or model-generated claim cannot become verified game state.

A fresh event is used only once as an initiative trigger. Borin cannot repeatedly comment on the same event.

## Persistence and anti-spam behavior

The application creates `AI\Memory\borin-natural-initiative.json` at runtime. It stores only bounded initiative metadata, including:

- surface and recipient;
- last human activity;
- last meaningful event classification and a short summary;
- last evaluation, attempt, and successful initiative time;
- the last proactive line;
- hourly initiative timestamps; and
- whether Borin is waiting for a response.

The store keeps at most 24 surface/recipient profiles and 12 recent initiative timestamps per profile. It does not contain credentials, GGUF weights, commands, hidden reasoning, full terminal logs, or private conversation transcripts.

## Permission boundary

Natural initiative is conversational only. It grants no permission to:

- send gameplay commands other than the final private `TELEPATH` line;
- move, fight, rest, buy, equip, trade, or manipulate inventory;
- connect, log in, or expose credentials;
- edit repository files;
- run scripts, tools, builds, tests, publish, or deploy; or
- claim off-screen activity or continuous consciousness.

All earlier cumulative privacy, evidence, conversation-floor, loop-prevention, game-state, and 270-character safeguards remain in force.
