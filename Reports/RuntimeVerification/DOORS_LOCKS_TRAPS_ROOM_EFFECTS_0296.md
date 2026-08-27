# Doors / Locks / Traps / Room Effects — Evidence Summary 0296

Recovered native/strong evidence used by Pass 25:

- MajorMUD help identifies BASH as usable against doors and gives `bash north` / `picklock north` examples.
- MajorMUD trap help identifies SEARCH-based discovery and `DISARM TRAP <direction>`.
- Nightmare Redux room metadata labels Door/Gate Para2 as the chance to picklock/bash, Para3 as five-minute open blocks, and Para4 as key required.
- The MME export path interprets Door/Gate Para2 as a picklocks/strength capability annotation, confirming that the field belongs to the explicit door-action boundary.
- The stock RoomType 0–24 family has no generic environmental-hazard type.

What remains unresolved is deliberately not executed as native parity: exact PICKLOCK/BASH success roll, exact trap detection/disarm roll, failed-disarm triggering, per-exit trap Para2 values and transition behavior, and exact native messages.
