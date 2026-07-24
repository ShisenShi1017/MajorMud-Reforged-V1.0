# Code Update 0197 - Classic Combat Controls

- Reserves `a` as the exact alias for `attack`.
- Existing characters missing combat fields receive derived attack, defense, dodge, and unarmed damage values at runtime.
- New characters persist initial combat values during creation.
- Direction commands automatically disengage/run from combat before movement; `flee` is not required.
- Keeps the cumulative Arena spawn and AI-state fixes from Updates 0186-0196.
