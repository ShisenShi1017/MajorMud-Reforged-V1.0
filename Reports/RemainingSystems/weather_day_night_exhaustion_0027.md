# Weather and Day/Night Exhaustion — Update0027

Implemented a Classic-safe world-condition boundary with explicit disabled, presentation-only, and simulated modes. Classic MajorMUD weather/day-night simulation remains inactive because the current repository does not prove it exists.

## Runtime ownership

`RuntimeWeatherDayNightService` owns typed condition state and routing only. Clock scheduling owns timing; Rooms own descriptions; NPC AI owns spawn/behavior changes; Spells owns spell changes; Environmental Hazards owns visibility and hazard consequences.

## Native dependencies

Exact existence, records, transitions, schedules, messages, and Worldgroup boundary require the final native pass.
