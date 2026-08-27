# Pass 15 Validation — Update 0285

Pass 15 validates the Death / Law / PvP / Character Lifecycle closure.

Expected gates:

- graceful EXIT delay = 5 seconds and final session save remains connected;
- REST focus activation = 20 seconds with interruption wiring;
- Lawful attack multiplier = x3;
- law consequence ladder and 300+ EP attack lockout are centralized;
- PvP maximum-level-difference is persisted, exposed to SYSOP configuration, and enforced before combat starts;
- 11 decoded `Arena Practice Room` records are present in retained Classic room data;
- practice-arena PvP suppresses attack Evil Points and bypasses normal life/corpse death transition;
- missing arena-entry HP fails closed;
- raw reconnect/socket takeover is `EXCLUDED_WORLDGROUP`;
- exact normal-death XP loss remains UNKNOWN and no guessed XP-loss arithmetic is introduced;
- reconciled scorecard = 71 PASS / 58 STRONG / 47 UNKNOWN / 176 total.
