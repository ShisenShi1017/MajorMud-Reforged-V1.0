# Code Update 0282 — MajorMUD Economy / Transactions / Services Parity

Pass 12 corrects Classic merchandise and trainer pricing from recovered stock MMUD Explorer source while keeping unresolved native service ordering fail-closed.

## Activated STRONG Classic behavior

- Five decoded item currency types convert to copper at `1, 10, 100, 10,000, 1,000,000`.
- Shop `Markup%` is additive: `0 = retail`, `100 = +100%`, `500 = +500%`.
- Buy order is currency conversion -> additive shop markup with `Fix` -> stock Charm buy multiplier -> retained 32-bit overflow behavior -> VB midpoint-to-even final rounding.
- Sell price starts from base item copper value, ignores shop markup, then applies the stock sell-Charm percentage.
- Trainer money cost is `Fix((Level * 5) * (Markup + 100) / 100) * 10`; live `TRAIN` now debits the recovered copper cost in the same character save as promotion.
- LIST and BUY now share the same quote authority so displayed and charged prices cannot drift.
- BUY/SELL retain the persistent shop-stock transaction boundary and character debit/credit settlement.

## Important correction to CodeUpdate0162

CodeUpdate0162 treated raw shop markup as a direct multiplier and normalized `100` to retail. The retained stock pricing source disproves that interpretation. The 0162 catalog and documentation are corrected in place so the repository no longer contains two conflicting markup definitions.

## Still unresolved / gated

- Final `wccmmud.dll` callsite proof for the recovered commerce/trainer formulas.
- Exact equipment/permanent-stat ordering used to produce effective Charm at shop time.
- Repair, spell-service, transport and other service-specific price ordering/minimums/refunds.
- Exact native give/trade/auction escrow and disconnect semantics.

Those remain STRONG or UNKNOWN; no inferred service formula was activated.
