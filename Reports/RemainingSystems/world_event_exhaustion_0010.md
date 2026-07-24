# Remaining Systems 0010 — Events and Scheduled World Activity

Implemented an executable world-event coordinator with scheduled and administrative activation, participant capacity, deterministic start/end/cleanup dispatch, cancellation, required-action failure state, repeatability policy, persistence, restart restoration, and integration boundaries for Clock Scheduling, Triggers, Scripts, NPC AI, Economy, Communications, Global World State, and World Reset.

The service owns event lifecycle and state only. Trigger and Script runtimes retain execution semantics; NPC AI owns spawn behavior; Economy owns rewards; Communications owns announcements; Global World State owns flags; and reset coordination owns reset dispatch.

## Native dependencies

Exact Classic event record families, native start/end conditions, recurring and missed-event behavior, participant rules, reward rules, cancellation, cleanup order, restart recovery, sysop controls, remote-action event behavior, and exact messages remain assigned to the final native pass.
