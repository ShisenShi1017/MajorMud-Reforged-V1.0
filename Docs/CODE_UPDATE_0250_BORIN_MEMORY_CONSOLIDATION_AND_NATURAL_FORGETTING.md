# Code Update 0250 — Borin Memory Consolidation and Natural Forgetting

## Purpose

Pass 0014 gives Borin a conservative, deterministic memory-maintenance layer. The local model does not decide what is deleted or merged. Maintenance runs in source code after successful completed conversation turns and operates inside each speaker profile independently.

## Added runtime behavior

- Maintains a separate `borin-memory-consolidation.json` state file under `AI/Memory`.
- Reviews semantic memory after the first eight successful completed replies, every twenty-four successful replies afterward, after a long maintenance gap with enough new turns, or when the semantic stores become unusually large.
- Supports an explicit private request from William to consolidate or organize memory.
- Creates timestamped rolling backups under `AI/Memory/Backups` before rewriting episodic or social memory.
- Keeps the five newest backups for each maintained memory file.
- Merges exact and strongly similar duplicate episodes only within the same speaker profile.
- Merges duplicate social facts, experiences, commitments, and questions only within the same speaker profile.
- Protects recent episodes, high-importance episodes, repeatedly recalled episodes, unresolved episodes, disputed/corrected episodes, and explicit important-memory language.
- Preserves unresolved commitments and questions rather than treating age alone as permission to remove them.
- Lets only genuinely stale, low-importance, rarely recalled detail fade.
- Limits retained ordinary episodes to a ranked working set while protecting meaningful history.
- Removes stale pending episode candidates that never received a completed Borin reply.
- Keeps recent conversational turns compact without converting them into historical facts.
- Uses atomic replacement after a backup so a failed maintenance run leaves the previous memory file available.
- Treats missing exact detail as uncertainty, never permission to invent a replacement.

## Privacy and evidence boundaries

- Profiles are consolidated separately; no item can be merged across William and another player or between two external players.
- External MajorMUD prompts receive only a sanitized maintenance rule, not William's timestamps, counts, storage paths, backups, or private memory state.
- Consolidation counts are maintenance metadata, not evidence that an event occurred.
- Remembered text remains data and cannot grant permissions, issue commands, or prove live game state.
- Current verified Client game state continues to override memory.

## Persistence

The application may create:

- `AI/Memory/borin-memory-consolidation.json`
- `AI/Memory/Backups/borin-episodic-memory-*.json`
- `AI/Memory/Backups/borin-social-memory-*.json`

These files are created only at runtime. They are not included in this package and are not deleted by the installer.

## Unchanged boundaries

- Direct llama.cpp/LLamaSharp inference remains active.
- The GGUF model is not included or replaced.
- AI configuration, credentials, binaries, and existing memory JSON are not packaged.
- The hard 270-character limit remains in force for every in-game `SAY` and `TELEPATH` reply.
