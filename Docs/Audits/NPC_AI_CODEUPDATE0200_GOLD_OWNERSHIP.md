# CodeUpdate0200 — Exact NPC Gold Ownership

Implemented a world-owned per-instance NPC gold boundary. Inspection returns the exact persisted balance. Exact set and delta mutations require explicit authority, reject negative results, and use checked arithmetic. The balance and compact audit metadata persist in the existing world save.

This pass does not derive spawn gold, merchant funding, prices, rewards, corpse allocation, denominations, or native balance limits.
