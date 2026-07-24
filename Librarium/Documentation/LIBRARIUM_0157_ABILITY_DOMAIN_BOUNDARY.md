# CodeUpdate 0157 — Ability Domain Boundary

The runtime contains two proven, disjoint ability domains.

* `abilities.json` contains 188 numeric modifier/quest-flag definitions. Textblock `ifability`, `ifnotability`, `giveability`, and `addability` operate exclusively on these values persisted as `quest.ability.<legacy-id>` in character conditions.
* `skills.json` contains 13 learned active skills. `RuntimeActorAbilityState` owns knowledge and cooldown state for these canonical `skill.*` identifiers.

No implicit conversion exists between the domains. A numeric quest flag does not teach an active skill; learning a skill does not satisfy a numeric textblock check. Unknown identifiers fail closed. Cooldowns belong only to learned skills. This boundary prevents textblock state from accidentally granting live powers and prevents learned powers from mutating quest progression.
