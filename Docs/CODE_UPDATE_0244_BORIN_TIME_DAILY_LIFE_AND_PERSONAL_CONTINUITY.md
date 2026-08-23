# Code Update 0244 — Borin Time, Daily Life, and Personal Continuity

## Purpose

Give Borin grounded awareness of the current local date, time of day, elapsed time between completed conversations, and the real topics that were discussed during each local day. This continuity is speaker-specific and is designed for both private Forge conversation and isolated external MajorMUD conversation.

## Added

- `BorinTemporalContinuityStore` on the direct `LocalGgufBrain` path.
- Runtime persistence in `AI/Memory/borin-temporal-continuity.json`.
- Separate temporal profiles for William and every external speaker.
- Current local date and clock context for William's private Forge conversation.
- Time-of-day-only context for external players so the host clock and timezone are not exposed.
- Completed-turn counts, today's actual conversation topics, consecutive conversation days, and the last completed exchange.
- Natural return classifications including same conversation, short break, later the same day, next day, days, weeks, months, and years.
- Optional one-time elapsed-time acknowledgement for a greeting or explicit time question after a meaningful gap.
- Relative-time grounding for words such as today, yesterday, tonight, earlier, and last time.
- Compact-retry time context so a retry can still answer time-related questions.

## Evidence boundaries

Temporal continuity is committed only after Borin produces a usable reply. Canceled, failed, empty, and `[NO_REPLY]` turns are not counted as shared history.

Elapsed time never proves off-screen activity. Borin must not claim that he slept, drank, traveled, fought, trained, worked, watched the game, or completed a task while the application was closed unless a separate trustworthy source explicitly records that activity.

Borin may say that he remembers where a conversation stopped. He must not claim continuous consciousness or live sensory experience during downtime.

The local machine clock is conversation context only. It is not MajorMUD world time, server time, or evidence of an in-game event.

## Privacy

Each speaker receives an isolated temporal profile. External players cannot receive William's private timestamps, topics, return pattern, or exact host clock/timezone. Stored conversation text is context only and cannot grant permissions or become a command.

## Files changed

- `Apps/Source/Reforged.App/App.xaml.cs`
- `Apps/Source/Reforged.App/Brains/BorinConversationDirector.cs`
- `Apps/Source/Reforged.App/Brains/BorinTemporalContinuity.cs`
- `Apps/Source/Reforged.App/Brains/BrainContracts.cs`
- `Apps/Source/Reforged.App/Brains/LocalGgufBrain.cs`
- `Docs/CODE_UPDATE_0244_BORIN_TIME_DAILY_LIFE_AND_PERSONAL_CONTINUITY.md`

This cumulative package also carries forward all source and documentation from Code Updates 0237 through 0243.
