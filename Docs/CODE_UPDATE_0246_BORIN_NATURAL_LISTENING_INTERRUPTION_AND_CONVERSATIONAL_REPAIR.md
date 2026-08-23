# Code Update 0246 — Borin Natural Listening, Interruptions, and Conversational Repair

## Purpose

Pass 0010 teaches Borin to listen to how a conversation changes, not merely to process the newest text as another isolated request. The direct local GGUF path now distinguishes corrections, interruptions, incomplete thoughts, deliberate topic changes, abandoned subjects, and requests to resume an earlier thread.

This pass is cumulative with Code Updates 0237 through 0245.

## Added source

- `Apps/Source/Reforged.App/Brains/BorinConversationRepair.cs`

## Updated integration

- `BrainContracts.cs` carries a `BorinConversationRepairSnapshot` with every conversational turn.
- `App.xaml.cs` creates and registers one `BorinConversationRepairStore`.
- `LocalGgufBrain.cs` prepares the repair state before the conversation director, includes it in the conversational prompt, and commits it only after a usable completed reply.
- `BorinConversationDirector.cs` gives repair modes priority over generic chat, planning, storytelling, and initiative.
- `BorinVoice.cs` applies restrained turn shapes for corrections, pauses, interruptions, resumptions, topic switches, and dropped threads.
- `BorinSituationalAwareness.cs` treats the listening plan as authoritative when ordinary continuity would conflict with the speaker's newest intent.

## Runtime behavior

### Corrections

Borin accepts the newest explicit meaning without defending the previous answer. When the correction contains the replacement meaning, he continues from it. When it only says that he misunderstood, he asks one short clarification question and stops.

Examples:

- `No, I meant the Forge terminal.`
- `That isn't what I was asking.`
- `You misunderstood me.`

### Interruptions

Messages such as `wait`, `hold on`, `before you continue`, or `let me stop you` make Borin yield immediately. He does not finish the prior response before listening. A temporary interruption may permit one return to the parked thought after the interruption is answered; otherwise the old thread remains parked.

### Incomplete thoughts

When the speaker explicitly says they are still typing or ends a short thought with an ellipsis or em dash, Borin does not invent the missing conclusion. He uses one short acknowledgement and gives the speaker room to finish.

### Deliberate topic changes

Phrases such as `new topic`, `different question`, or `on another note` park the earlier subject. Borin follows the new topic without a forced recap and without dragging unrelated promises or questions into it.

### Resuming a thread

`Continue`, `where were we`, `back to ...`, and similar requests restore the paused per-speaker thread when one exists. Borin anchors it briefly and continues from the stopping point instead of restarting the entire explanation.

### Abandoning a thread

`Never mind`, `drop it`, and explicit requests to leave a topic clear the paused thread after a completed reply. Borin does not repeatedly ask why or revive the subject later unless the speaker introduces it again.

## Persistence and privacy

The runtime may create:

- `AI/Memory/borin-conversation-repair.json`

The file is created only by the running application. It is not included in this package.

Each speaker has an isolated repair profile containing only that speaker's last completed topic, last completed exchange, and optional paused thread. William's private Forge thread cannot be supplied to an external MajorMUD player, and one external player's paused conversation cannot appear in another player's prompt.

Failed, canceled, empty, and `[NO_REPLY]` turns do not commit repair history. The repair store does not authorize files, builds, tools, deployments, logins, or game commands, and it does not claim that an interrupted thread continued off-screen.

## Expected conversational improvements

- Borin stops talking over a correction or interruption.
- Short repair exchanges remain short instead of becoming essays.
- `Continue` returns to the right subject without a full restart.
- A new topic does not inherit irrelevant details from the parked thread.
- Partial messages receive patience rather than guessed answers.
- Conversational initiative is suppressed while repairing a misunderstanding.
- External-player repair state stays isolated and privacy-bounded.

## Suggested live checks

1. Ask a normal multi-turn question, interrupt with `Wait, different question`, then later say `Continue.`
2. Tell Borin `That isn't what I meant` without a replacement and confirm he asks only one clarification question.
3. Correct him with `No, I meant the Forge terminal` and confirm he uses the corrected premise without defending himself.
4. Send `Hold on, I'm not done...` and confirm he gives a one-sentence listening response.
5. Say `Never mind, drop it`, then begin another subject and confirm the old thread does not return.
6. Repeat the interruption/resume test with two different external player names and confirm their paused threads remain separate.
