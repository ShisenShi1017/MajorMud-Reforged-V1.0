# Code Update 0209 - Currency Runtime and G Alias

- Forces exact `g` to resolve to `get` before generic abbreviation matching.
- Uses MMExplorer NPC wealth as authoritative drop value.
- Low-level NPCs drop copper/silver denominations rather than gold crowns.
- Aggregates legacy duplicate currency instances into one visible stack per denomination.
- Picking up a denomination collects all matching room stacks and credits carried wealth only after pickup.
