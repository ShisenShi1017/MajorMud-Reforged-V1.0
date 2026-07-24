# Worldgroup Native Closure Update0032 — MKDE Flush Caller Ordering

## Result

All direct `W32MKDE.EXE` callsites to the two previously recovered flush wrappers were enumerated and classified. The executable contains two direct calls to explicit wrapper `0x004043D0` and thirteen direct calls to conditional dirty-state wrapper `0x00404D00`.

## Explicit flush boundaries

`0x00427BBF` and `0x00428336` occur after bounded staged-write sequences in the recovery/log serialization region. The first call's 16-bit result is checked and can fail the enclosing operation. The second is the terminal flush before temporary-state cleanup and diagnostics; its immediate result is not consumed.

These calls prove that selected recovery/log serialization workflows force a synchronous flush rather than relying on later cache eviction. They do not, by themselves, prove the semantic format of the records being serialized.

## Conditional dirty-state boundaries

Seven calls are in page/cache transfer and finalization routines. Several callers add an independent nonzero pending-state test before entering `0x00404D00`; the wrapper then applies its own file-object flag test and clears the pending counter while flushing.

Three calls at `0x00435BFE`, `0x00435C1C`, and `0x00435C3A` form a grouped teardown sequence. They independently guard and flush three file objects at owner offsets `+0x1CC`, `+0x1C0`, and `+0x1B4`, using pending counters at `+0x1D4`, `+0x1C8`, and `+0x1BC`, before linked cache records are detached.

Three calls at `0x0043B1E5`, `0x0043B28F`, and `0x0043B345` occur only after successful metadata/index mutations. They establish explicit durability boundaries both for an existing extent and for a newly allocated extent.

## Durability conclusion

W32MKDE is write-back rather than universally write-through. A successful low-level write may remain pending, but synchronous serialized flushes are forced at selected recovery/log finalization, page/cache transition, grouped-close, and metadata/index consistency points.

## Bounded unknowns

This pass does not assign semantic names to the recovery/log records, claim that every transaction end reaches a direct wrapper call, or infer the complete `BTRVLOG.TRN`/`BTRVLOG.RCV` replay format.
