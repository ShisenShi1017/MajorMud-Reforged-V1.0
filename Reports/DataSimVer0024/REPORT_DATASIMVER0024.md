# DataSimVer0024 Report — Quest/Boss Persistence and Occupancy Separation

## Objective
Evaluate the current cumulative package for evidence related to quest persistence, boss/unique respawn behavior, room occupancy rules during respawn, and cleanup-vs-respawn separation.

## Method
- Used `/Data` as the authoritative runtime source.
- Scanned cumulative data files for quest, respawn, cleanup, hidden item, and room/occupancy clues.
- Preserved all candidate behavior as hypothesis unless directly evidenced by data or executable logic.

## Findings
- Quest and boss/unique behavior remain candidate classifications only.
- No executable-backed rule was found proving quest monster permanence, server-wide uniqueness, or respawn blocking by player presence.
- Existing evidence continues to support separate documentation buckets for cleanup, global respawn, explicit monster timers, and quest persistence.
- The user-provided model remains documented: normal creatures use an approximate 15-minute global respawn cycle; explicit monster timers start on death and continue through reset.

## Statistics
- Data files scanned: 92
- Quest keyword hits: 3229
- Boss/unique keyword hits: 5448
- Respawn/timer keyword hits: 15875
- Occupancy/room keyword hits: 97292
- Cleanup/search keyword hits: 4110

## Promotion Decision
No `/Data` changes were made. No quest, boss, occupancy, or cleanup semantics were promoted.

## Remaining Unknowns
- Exact quest monster persistence flags.
- Whether unique/boss monsters are globally unique or room-local.
- Whether player room presence blocks respawn.
- Whether dead-monster timers are stored globally, per room, or per monster instance.
- Exact cleanup flag mapping for destroy vs hidden.
