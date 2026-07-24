# Combat & Spells Audit 0183

Implemented rollback-safe combat and spell snapshots with promoted field ownership, canonical references, deterministic cleanup, integrity hashes, and restart/crash validation. Active effects and timers may persist only through promoted policies. Combat instances, threat, pending actions, casting state, summons, and control effects remain fail-closed until production or native evidence proves their save behavior.
