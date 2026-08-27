# Code Update 0288 — GIVE / Shops / Transportation Closure

## Scope

Pass 18 narrows the Classic economy/transaction parity surface to behavior actually owned by MajorMUD. It does not invent a formal TRADE escrow system or a stock REPAIR service.

## Classic GIVE

- Stock syntax is `GIVE <item> <player>`; the word `to` is accepted only as compatibility input, not required.
- `SET RECEIVE ON|OFF` is persisted per character. RECEIVE OFF blocks incoming GIVE transfers.
- Recipient carried weight is checked against the recovered MajorMUD encumbrance capacity before commit.
- The transfer routes through `RuntimeItemEngine.Give` so item-specific `no_give`, bound-owner, quest-owner, and owner-quantity restrictions are honored.
- Limited status by itself is not a blanket transfer prohibition.
- Exact native overweight/refusal wording remains STRONG rather than PASS.

## No native formal TRADE / REPAIR

Official MajorMUD help exposes direct GIVE and describes AUCTION as a communication channel. The decoded Classic service catalog contains no repair service family. Reforged `trade` and generated `repair` remain available for non-Classic worlds but are blocked and hidden from fallback help in Classic.

## Shops

The existing Classic BUY transaction keeps the evidence-backed order: verify persistent stock, quote price, debit authoritative copper value, create the item, then commit stock mutation. Insufficient funds are rejected by the currency authority. Exact native failure wording remains unresolved.

## Transportation

Transportation is retained as MajorMUD-owned. This pass does not promote generic Reforged transport assumptions to native behavior. Exact stock transport records, fares, schedules, boarding windows, capacity/party behavior, disconnect behavior, messages, and restart persistence remain UNKNOWN.

## Map 12 / Room 2248 rug exit

The final Hidden/Unknown rug exit is no longer treated as an unknown behavior family. `move rug` and `pull rug` temporarily reveal the passage and the passage resets under the normal MajorMUD door-timer authority. The exact timer duration and failure message are still not recovered and remain under timer/message closure.

## Score

- PASS: 71
- STRONG: 68
- UNKNOWN: 45
- Total: 184
- Strict: 38.6%
- Evidence-backed: 75.5%

The denominator increases because broad transaction questions were split into narrower auditable assertions; the UNKNOWN count falls from 47 to 45.
