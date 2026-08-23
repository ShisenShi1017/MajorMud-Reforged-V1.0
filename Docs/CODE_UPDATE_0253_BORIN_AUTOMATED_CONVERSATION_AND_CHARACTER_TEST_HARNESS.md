# Code Update 0253 — Borin Automated Conversation and Character Test Harness

Cumulative Pass 0017 adds a deterministic test harness for Borin's model-independent character and conversation systems. It does not load the GGUF model, connect to a BBS, use credentials, or read/write Borin's real `AI\Memory` files.

## What is tested

The harness currently runs twelve repeatable scenarios:

1. Every in-game reply remains at or below the hard 270-character boundary.
2. `[NO_REPLY]`, speech prefixes, line breaks, and structured speech wrappers are cleaned correctly.
3. Greetings, distress, thread follow-ups, and self-knowledge questions receive the correct conversation mode.
4. William can explicitly confirm a private character preference while an external player cannot rewrite Borin's global identity.
5. Recently used catchphrases are placed on cooldown.
6. Social facts remain isolated per speaker.
7. Meaningful episodic memories commit only after a usable completed reply.
8. Temporal continuity advances only after a usable completed reply.
9. Interrupted subjects can be parked and later resumed.
10. Direct telepaths permit a reply while speech directed to someone else makes Borin yield.
11. Combat and pending direct questions suppress unsolicited initiative.
12. Self-knowledge uses verified live state while external prompts exclude repository paths, model names, and private runtime details.

## Production/test identity

`BorinDialogueSafety.CleanInGameReply` is now the shared production implementation for the in-game speech boundary. `BorinPlayerResearch` calls it immediately before `SAY` or `TELEPATH`, and the automated harness calls the same method. The test is therefore checking the actual cleanup path rather than a copied approximation.

## Test project

The update adds:

- `Apps\Source\Borin.CharacterTests\Borin.CharacterTests.csproj`
- `Apps\Source\Borin.CharacterTests\Program.cs`
- `Apps\Source\Reforged.App\Brains\BorinCharacterTestHarness.cs`
- `Apps\Source\Reforged.App\Brains\BorinDialogueSafety.cs`

The runner returns exit code `0` only when every scenario passes and writes a JSON report under `Release\Logs`.

Manual command from the repository root:

```powershell
dotnet run --project Apps\Source\Borin.CharacterTests\Borin.CharacterTests.csproj -c Release -- --repository .
```

The cumulative installer restores and builds the real application, runs this harness, records the full JSON report path, and fails the installation if a deterministic Borin contract fails.

## Safety and scope

- Tests use a disposable temporary repository root.
- No real character memory is opened or changed.
- No local model is loaded.
- No network or gameplay command is sent.
- No AI folder, GGUF file, configuration, credentials, binaries, or saved runtime memory is included in the payload.
- Direct llama.cpp/LLamaSharp inference remains unchanged.
