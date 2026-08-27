# MajorMUD Parity Scorecard 0289

- **PASS:** 71
- **STRONG:** 69
- **UNKNOWN:** 44
- **Scored requirements:** 184
- **Strict proven parity:** 38.6%
- **Evidence-backed parity:** 76.1%

## By subsystem

- **Administration & Host** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Administration / Security / Builder** — PASS 0, STRONG 0, UNKNOWN 2; strict 0.0%, evidence-backed 0.0%
- **Character & Status** — PASS 20, STRONG 15, UNKNOWN 7; strict 47.6%, evidence-backed 83.3%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 1, STRONG 13, UNKNOWN 2; strict 6.2%, evidence-backed 87.5%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 1, UNKNOWN 6; strict 0.0%, evidence-backed 14.3%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 1, STRONG 10, UNKNOWN 2; strict 7.7%, evidence-backed 84.6%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Parties / Communication / Organizations** — PASS 1, STRONG 2, UNKNOWN 5; strict 12.5%, evidence-backed 37.5%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 1; strict 91.7%, evidence-backed 91.7%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 11, STRONG 8, UNKNOWN 5; strict 45.8%, evidence-backed 79.2%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 3, STRONG 4, UNKNOWN 4; strict 27.3%, evidence-backed 63.6%

## Pass 19 Commands / Communication / Party Coordination notes

- The command dispatcher now applies the project-wide Reforged rule in Classic, Custom, and AI: a uniquely resolved valid command/action executes; anything unrecognized or ambiguous becomes SAY using the entire original prompt text. Native speech-prefix actions are resolved before that fallback.
- Native communication help establishes `.` room talk, `"` YELL, `/Username <TEXT>` one-recipient telepath, SAY, global GOSSIP/AUCTION channel behavior, and FORGET telepath blocking. YELL reaches the current room plus adjacent rooms. Exact rendering/failure text, WHISPER behavior, and a native TELL command identity remain unresolved.
- Native command evidence corrects `BR` to BROADCAST; `BS` remains BACKSTAB and `BG` remains BROADGANG. The rest of the exact minimum-abbreviation/ambiguity table remains evidence-gated.
- Classic HELP now uses the retained WCCMMHLP catalog as authority and fails closed when a native topic cannot be uniquely resolved. It no longer substitutes synthetic Reforged help for unresolved Classic topics; Custom/AI may retain extension help.
- `JOIN <username>` and `FOLLOW <username>` both accept/use a party invitation and immediately establish party-following. `TRACK <username>` is the separate tracking-skill command. `LEAVE` exits following/party membership for the proven follower case.
- Party coordination enforces the documented rank invariant that Backrank cannot outnumber Frontrank. The exact Frontrank capacity formula, initial/default rank edge cases, multi-member leader LEAVE behavior, leader rank-changing behavior, and unexpected disconnect/linkdead behavior remain UNKNOWN.
- The Classic party shell no longer invents a two-minute invitation timeout, six-member cap, automatic leader succession, or leader-must-remain-Frontrank rule. Generic multiplayer smoke-model assumptions are explicitly excluded from Classic parity evidence.
- `SET FOLLOW BLIND|NORMAL` is implemented at STRONG confidence: BLIND suppresses room descriptions while moving with the party; NORMAL restores normal room descriptions. Exact default/persistence and any additional suppression remain unresolved.
- Native `SHARE <amount> <currency> [WITH <ROOM/PARTY>]` behavior is wired: PARTY is the default scope, the sender is excluded, hidden/RECEIVE-blocked recipients are excluded, integer division is used, and the sender retains the remainder. Exact native messages and the unresolved RECEIVE polarity conflict remain evidence-gated.
- Party XP/loot/assist/follow coordination is only credited where evidence supports it. No generic equal-split XP rule or speculative disconnect/loot behavior is promoted to Classic parity.
- Transportation is narrowed to immediate paid room transfers: source room + recognized command + payment validation + immediate destination. Vehicle state, boarding, schedules, travel timers, arrival waits, and transport persistence are excluded from MajorMUD parity.
- `native.communications.classic-range-rendering` moved UNKNOWN → STRONG. No PASS promotion was made, so the score remains PASS 71 / STRONG 69 / UNKNOWN 44: 38.6% strict and 76.1% evidence-backed.
