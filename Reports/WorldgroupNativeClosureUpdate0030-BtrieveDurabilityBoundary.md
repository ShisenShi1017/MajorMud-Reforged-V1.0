# Worldgroup Native Closure Update0030 — Btrieve Durability Boundary

## Result

`WBTRV32.DLL` is the Btrieve 6.20 API/link and transport layer, not the physical page engine. Its `BTRCALL` entry marshals requests into a client/session transport and can reach the local BMKDE engine through `\\.\PIPE\BMKDE\FUNCTION.PIP`; it also advertises `W32MKDE.EXE AUTOSTART`.

The supplied `W32MKDE.EXE` is the local physical engine. It directly imports file positioning, writing, flushing, locking, truncation, rename, deletion, mapping, and related APIs. Embedded implementation-domain names identify cache, transaction-log, recovery, coordinator, I/O, and background subsystems.

## Recovery artifacts

The engine contains the names `BTRVLOG.TRN`, `BTRVLOG.RCV`, `BTR_TMP.$??`, `MKDEWE.TRA`, and `MKDEMSG.LOG`. Their existence and ownership are proven. Transaction/recovery semantics are strongly correlated by the engine's `TRANSLOG.C` and `RECOVERY.C` implementation-domain strings, but exact binary formats and replay ordering remain unresolved.

## Corrected boundary

Update0029 correctly placed WGSERVER mutations at `WBTRV32!BTRCALL`. Update0030 moves the physical durability boundary one layer deeper: page cache, file writes, flushes, and crash recovery are owned by `W32MKDE.EXE` or an equivalent BMKDE server transport, not by `WBTRV32.DLL`.
