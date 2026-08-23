# Code Update 0260 - Borin Active Perception Refresh

Cumulative Pass 0018G.

## Purpose

When William asks Borin for current room, exits, or HP information and that perception is not freshly verified, Borin now uses the real canonical MajorMUD terminal to issue `LOOK` before answering. The language model is not asked to guess missing game state.

## Behavior

- Questions such as `what are the current exits?`, `what room are you in?`, or `do you see the HP prompt?` first inspect the verified Client snapshot.
- If the requested perception is missing while Borin is connected and logged into MajorMUD, Borin sends a real `LOOK` through his own canonical session.
- The refresh waits for a newer HP prompt, room title, and visible-exit list from the server.
- The refresh does not overwrite the saved Assignment window text and does not use the GGUF model to fabricate terminal output.
- If the terminal still does not provide enough evidence, Borin keeps the answer unknown instead of guessing.
- Existing autonomous-play, server-identity, command-ownership, privacy, NO_REPLY, and 270-character safeguards remain unchanged.
