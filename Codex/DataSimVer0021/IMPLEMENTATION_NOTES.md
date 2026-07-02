# DataSimVer0021 Implementation Notes

Simulator model should support two monster respawn schedulers:

1. `GlobalRespawnSweep(interval ~= 15 minutes)` for ordinary creatures.
2. `DeathTriggeredRespawnTimer(monster_id, delay)` for explicitly timed monsters.

Daily cleanup/reset should not clear, restart, or extend active explicit monster respawn timers unless direct engine evidence later proves otherwise.
