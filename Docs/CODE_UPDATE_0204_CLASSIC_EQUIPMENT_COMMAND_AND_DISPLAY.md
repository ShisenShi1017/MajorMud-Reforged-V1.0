# Code Update 0204 - Classic Equipment Command and Display

## Corrected behavior

- `eq` renders the combined carried and equipped item list in Classic format.
- `eq <item name>` equips a matching carried item.
- Unique target abbreviations such as `eq cl` resolve `club`.
- Equipped items show their friendly generated names and display slots in parentheses.
- `remove <item>` unequips and persists the item.
- The screen reports keys, wealth, and encumbrance.

## Persistence

The updated item-engine snapshot is saved immediately after an equipment action.
