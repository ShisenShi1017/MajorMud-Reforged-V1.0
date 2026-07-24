# Remaining Systems 0035 — Games, Arenas, Gambling, and Optional Activities

Implemented an evidence-gated runtime orchestration boundary for duels, arenas, tournaments, dice games, lotteries, raffles, contests, and scoreboards.

Classic activation is disabled unless evidence is explicitly confirmed. The service owns participation and lifecycle only. Economy owns charges and rewards; Combat and PvP own match rules; Rankings owns results; Communications owns announcements.

Executable validation: `--smoke-optional-activities`.

## Final native dependencies

The native phase must determine whether MajorMUD 1.11p implements any of these activities and recover exact commands, costs, rewards, matchmaking, persistence, death/item-loss behavior, and Worldgroup ownership.
