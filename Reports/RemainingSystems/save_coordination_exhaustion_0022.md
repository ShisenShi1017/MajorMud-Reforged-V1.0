# Remaining Systems Update0022 / Pass 51 Boundary — Save Coordination and World Persistence

## Current classification

Pass 51 / update 0322 supersedes the old generic-only classification. `native.save-coordination` is now **STRONG** at an evidence-backed Classic storage boundary.

## Proven native boundary

- WCCMMUD retained configuration proves **60 seconds** between saving Player data and **300 seconds** between saving Buffer data.
- MajorMUD uses retained Worldgroup DFA context with distinct insert/update/delete mutation calls.
- WGSERVER exposes `_dfaBegTrans`, `_dfaEndTrans`, `_dfaAbtTrans`, and `_dfaUnlock`; the recovered MajorMUD import family does not directly prove how save operations are grouped through those host exports.
- WGSERVER stages variable insert/update payloads through shared buffer `0x00467CA4`, stores synchronous BTRCALL status at `0x00467CA0`, and delegates storage through `WBTRV32.DLL!BTRCALL`.
- W32MKDE owns the physical cache/write/flush boundary. Ordinary successful writes can remain pending; selected native boundaries call `FlushFileBuffers` synchronously.

## Reforged coordination shell

`RuntimeSaveCoordinationService` remains useful Reforged safety infrastructure, but its configured participant ordering, prepare/commit phases, reverse rollback, generation counters, and JSON snapshot replacement are **not** MajorMUD parity evidence. `RuntimeClassicSaveCoordinationAuthority` now owns the Classic evidence gate.

## Still unresolved

- exact Player data versus world/Buffer data participant ordering;
- exact native record families represented by `Buffer data`;
- exact MajorMUD transaction grouping through WGSERVER begin/end/abort;
- exact ordinary-save flush/durability point;
- exact shutdown-save ordering;
- exact MajorMUD partial-save semantics.

Crash-log byte formats and complete replay semantics remain tracked under the native recovery boundary rather than being invented here.
