# Worldgroup Native Closure Update0031 — MKDE Flush and Recovery Paths

## Result

The supplied `W32MKDE.EXE` does not flush every successful write immediately. Its common write routine at `0x00404400` performs `SetFilePointer` and `WriteFile` under an engine critical section. When file-object flag bit `0x04` is active, a complete successful write increments a 16-bit pending-write counter at file-object offset `+0x08`.

Two implementation-level flush paths are now proven:

- `0x004043D0` is an explicit serialized flush wrapper. It acquires the engine critical section, calls `FlushFileBuffers` on the file object's handle, releases the critical section, and returns zero.
- `0x00404D00` is a conditional dirty-state flush wrapper. It checks file-object flag bit `0x04`, acquires the same critical section, clears the pending-write counter, calls `FlushFileBuffers`, releases the section, and returns zero.

This proves that writes may remain in the engine/OS cache until an explicit or state-driven flush path runs. It does not prove that a completed Btrieve operation is immediately durable in the absence of that flush.

## Recovery startup

Routine `0x0042C340` builds installation-relative paths for `BTRVLOG.RCV` and `BTRVLOG.TRN`. It probes the recovery artifact first and follows separate open/read/validation paths depending on which artifact is present. Recovered metadata is stored into the active engine context and cleanup/close processing runs before return.

The existence, path construction, selection order, and separate processing paths are proven. The full binary record layouts and exact replay/rollback sequence remain bounded unknowns.

## Corrected durability boundary

Update0030 established that physical persistence belongs to `W32MKDE.EXE`. Update0031 closes the first internal durability layer:

1. Page/file writes are serialized.
2. Successful writes may mark pending dirty work instead of flushing immediately.
3. Explicit and dirty-state wrappers call `FlushFileBuffers` synchronously.
4. Startup recognizes both transaction and recovery log artifacts.
