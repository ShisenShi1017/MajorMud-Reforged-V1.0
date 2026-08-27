# Economy CodeUpdate0162 — Native Markup Pass (Corrected by Pass 12 / 0282)

> **Superseded semantic interpretation:** CodeUpdate0162 correctly bound the 178 WCC shop markup fields, but incorrectly treated the raw field as a direct multiplier with `100 = 1.00x`. Pass 12 recovered the current stock MMUD Explorer pricing functions and corrected that interpretation.

## Corrected Classic interpretation

- The raw WCC/MDB `Markup%` field is an **additive percentage**.
- `0` = no markup / retail base.
- `100` = +100% = 2.00x before Charm.
- `500` = +500% = 6.00x before Charm.
- Merchandise uses `baseCopper + Fix(baseCopper * markup / 100)`.
- Trainer money uses `Fix((Level * 5) * (Markup + 100) / 100) * 10`.
- Merchandise buy Charm is applied after markup.
- Merchandise sell value is based on the item's base copper value and does **not** use shop markup.

## Evidence boundary

The retained current MMUD Explorer source strongly corroborates these formulas and reproduces stock v1.11p behavior. This is sufficient to activate the recovered Classic arithmetic as **STRONG**, but final native executable callsite proof is still required before promoting those pricing requirements to PASS. Generic repair/spell/other service-cost ordering also remains unresolved.
