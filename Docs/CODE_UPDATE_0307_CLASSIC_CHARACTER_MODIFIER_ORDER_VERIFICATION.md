# CodeUpdate0307 / Pass 36 — Classic Character Modifier-Order Verification Boundary

Pass 36 verifies the operator-supplied character modifier-order document against retained native MajorMUD evidence instead of copying it into runtime.

## Result

The document contains useful hypotheses but is not internally authoritative for Classic parity. Native evidence independently confirms several **formula-specific** ordering boundaries while rejecting or leaving unproven several document claims.

Confirmed/retained boundaries include:

- Classic level-1 primary stats start from the selected **race minimum statistics**.
- `wccmmud.dll calculate_secondary_stats @0x0041a424` provides exact formula-specific MaxHP/MaxMana operand ordering and is re-run after stat/equipment changes.
- Decoded primary-stat abilities are signed parameter-value deltas.
- Direct decoded equipment AC/DR/accuracy/speed fields aggregate additively.
- Raw Dodge has an explicit encumbrance placement before the proven Dodge-vs-Accuracy resolver.
- Native physical combat resolves AC-bearing hit chance before the later independent Dodge gate.
- Periodic spell/status processing uses the already-PASS **3-second** spell/system round.

Claims not adopted include the generic class-base/per-level creation equation, a universal flat-then-percent equipment stage, disease/curse/injury placement before an unproven equipment-percent stage, a universal final -50% blindness Accuracy override, sleep/paralysis forcing AC to zero, and a universal 10-second poison tick.

## Runtime boundary

Pass 36 adds an evidence-only registry and smoke route. **No gameplay formula or modifier caller is changed.** The registry is expressly non-authoritative for unresolved formula execution.

`character.cross-category-modifier-order` moves from UNKNOWN to STRONG because multiple independently recovered category boundaries now constrain the order substantially. The single universal cross-category order remains unresolved, so PASS is not claimed.

## Parity

**97 PASS / 77 STRONG / 26 UNKNOWN = 200**

- Strict parity: **48.5%**
- Evidence-backed: **87.0%**
