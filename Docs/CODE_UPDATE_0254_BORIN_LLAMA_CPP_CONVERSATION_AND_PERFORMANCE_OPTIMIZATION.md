# Code Update 0254 — Borin llama.cpp Conversation and Performance Optimization

## Purpose

Pass 0018 optimizes Borin's existing direct LLamaSharp/llama.cpp brain. It does not add LM Studio, replace the GGUF model, or alter credentials. The goal is to make ordinary Forge and MajorMUD conversation faster, clearer, and less robotic while preserving the evidence, privacy, permission, memory, and 270-character in-game boundaries established by Passes 0001–0017.

## Prompt planning before inference

Every turn now receives a purpose-specific prompt plan before LLamaSharp creates the chat session. The plan reserves output tokens first, keeps a safety margin for the model template, and then divides the remaining input budget among the system prompt, relevant history, and the current turn.

Prompt modes are separated for:

- private Forge conversation;
- external MajorMUD conversation;
- natural initiative;
- repository research;
- controlled engineering drafts; and
- general reasoning.

External and proactive MajorMUD turns do not receive the global Forge conversation history. They rely on the already isolated per-speaker social, episodic, temporal, repair, group, and situational snapshots. This prevents private Forge/project discussion from leaking into public or telepath conversations and avoids wasting context on unrelated turns.

Private Forge conversation keeps only completed recent turns and semantically relevant older turns. Pending, canceled, failed, or duplicate current user messages are not added as history.

## Compact priority-ordered prompts

The previous conversation prompt could combine every character system at full size. Pass 0018 uses a bounded prompt composer and supplies information in this order:

1. privacy, group-floor, safety, evidence, and permission limits;
2. verified live game state and honest self-knowledge;
3. interruption repair and the current conversation direction;
4. the unified current situation;
5. only the most relevant isolated memory, relationship, growth, motivation, voice, episode, time, and gameplay-learning details.

Lower-priority text is compacted when the configured context cannot hold it. Technical research has a separate evidence-first layout so conversational personality does not crowd out source paths, line ranges, trace stages, or indexed evidence.

## Purpose-specific output and sampling

The existing settings now support separate output caps for private conversation, external conversation, natural initiative, repository research, and engineering drafts. Balanced defaults are:

- private Forge conversation: up to 320 tokens;
- external MajorMUD conversation: up to 128 tokens;
- natural initiative: up to 128 tokens;
- repository research: up to 768 tokens; and
- engineering drafts: up to 768 tokens.

External text still passes through the absolute 270-character send limiter immediately before SAY or TELEPATH.

Conversation uses controlled natural variation from Borin's voice plan. Repository research and engineering drafts use a lower technical temperature for more exact, repeatable answers. Compact retries use a smaller deterministic output and no global history.

## Knowledge-index startup and reuse

The existing knowledge corpus may be very large. Status checks now use a small runtime sidecar at:

`AI\Memory\Knowledge\borin-knowledge-index.status.json`

A legacy index larger than 64 MB is no longer fully deserialized merely to display its status. The first actual indexed search warms the document once for the current application process; later searches reuse that parsed document while it remains valid. Identical repeated queries also use a bounded query-result cache. Rebuilding or changing the index invalidates both caches and refreshes the status sidecar.

Ordinary character conversation does not search or load the repository knowledge index unless the existing intent and evidence pipeline explicitly requests indexed project knowledge.

## Configuration compatibility

The package does not include or overwrite `AI\Config\borin-brain.json`. New settings have safe defaults when an older configuration is loaded and are normalized whenever the local-brain settings are saved or a performance preset is applied.

No AI model, GGUF file, native backend, credential, stored character memory, knowledge corpus, or release binary is included in this update.

## Automated coverage

The deterministic no-model character harness now includes fourteen scenarios. Two new cases verify:

- context/output reservation, external-history isolation, and technical-versus-conversational sampling; and
- knowledge-index sidecar creation plus warm document/query cache behavior using a disposable tiny repository.
