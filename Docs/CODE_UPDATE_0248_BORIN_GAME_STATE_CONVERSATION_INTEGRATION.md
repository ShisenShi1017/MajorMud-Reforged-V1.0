# Code Update 0248 — Borin Game-State Conversation Integration

## Purpose

Connect Borin's conversational brain to the verified state already observed by the Reforged Client. Borin must answer questions about his connection, login phase, room, combat posture, vitals, inventory, and equipment from current Client evidence instead of guessing from memory or asking William to provide evidence the application already has.

This update also establishes an absolute 270-character limit for every message Borin speaks inside MajorMUD.

## Added game-state snapshot

`BorinGameStateAwareness.cs` creates a point-in-time read-only snapshot from Borin's assigned `ClientSessionConnection` and `BorinLoginStatus`.

The snapshot distinguishes:

- disconnected;
- connecting or reconnecting;
- BBS login prompts;
- the BBS main menu;
- the MajorMUD menu;
- entering the realm or waiting for the game prompt;
- verified in-game state;
- faulted state.

A network connection is not treated as a MajorMUD login. Live room, combat, vital, inventory, equipment, level, experience, and currency values are supplied only when the Client is connected and Borin's login state is `Ready`.

## Live evidence boundaries

The current snapshot overrides old memories and prior dialogue for live state. If the Client cannot verify a value, Borin must call it unknown rather than reuse a stale room or inventory from an earlier session.

Captured inventory and equipment names remain Client observations. They do not prove hidden item effects, decoded rules, or server-side behavior.

The snapshot is read-only. It grants no permission to move, fight, buy, equip, trade, drop, send commands, or modify the server.

## Forge integration

The main Forge conversation path now supplies `IBorinPlayerResearch.CurrentGameState` to `BrainContext`. Borin can therefore answer questions such as:

- Are you connected?
- Are you actually logged into MajorMUD?
- Are you still at the BBS menu?
- What room are you in?
- Are you fighting?
- What is your health?
- What equipment has the Client captured?

He must distinguish a connected socket from a completed MajorMUD login and must not claim a live room when the realm state is unverified.

## In-game conversation integration

External player conversations receive the same verified snapshot through the direct GGUF path. Game state is used only when relevant and does not expose credentials, host details, private project rules, or other players' histories.

## Hard 270-character reply contract

All in-game speech now uses these rules:

- normal replies target 240 characters or fewer;
- busy public-room replies target 160 characters or fewer;
- the GGUF prompt is explicitly told never to exceed 270 characters;
- generated text is cleaned and shortened before command construction;
- a second pre-send check covers loop-guard and alternate reply paths;
- one character is reserved for an ellipsis when shortening at a word boundary;
- a final defensive slice guarantees the spoken text is never longer than 270 characters.

This also corrects the earlier off-by-one behavior where appending an ellipsis could produce 271 characters.

## Files changed

- `Apps/Source/Reforged.App/Brains/BorinGameStateAwareness.cs`
- `Apps/Source/Reforged.App/Brains/BrainContracts.cs`
- `Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs`
- `Apps/Source/Reforged.App/Brains/LocalGgufBrain.cs`
- `Apps/Source/Reforged.App/ViewModels/ForgeGuardianViewModel.cs`

## Runtime and data boundaries

This update does not include or replace:

- the `AI` folder;
- GGUF models;
- llama.cpp or LLamaSharp binaries;
- credentials;
- local-brain configuration;
- existing memory JSON;
- release binaries.

Game-state awareness is point-in-time and is not persisted as a memory file. The application continues to use direct llama.cpp/LLamaSharp inference.
