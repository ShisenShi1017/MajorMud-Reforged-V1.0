# Movement Follow Runtime Evidence Notes

Runtime logs show player party-follow movement with directional text and next room banners. This pass extracted those events and compared the observed direction plus next room name against canonical `/Data/rooms.json` exits.

The method is intentionally conservative. A PASS requires at least one canonical room with the observed previous room name to have an exit in the observed direction whose destination room has the observed next room name. Rows that cannot be safely matched are REVIEW, not FAIL, because repeated room names are common and room text alone is often insufficient.

No runtime-log evidence was promoted into `/Data` during this pass.
