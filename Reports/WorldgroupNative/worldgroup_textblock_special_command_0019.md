# Worldgroup Textblock Special-Command Execution — Update0019

Direct disassembly closes `wccmmud.dll!_perform_text_block_as_special_command` at `0x00471D55`. The cdecl wrapper takes a Worldgroup session index and a textblock identifier. It acquires a reusable 2,024-byte record buffer, fetches at most 50 records, requires the record identifier at `+0x10` to equal the requested textblock ID, and reads text beginning at `+0x18`.

Payloads are executed in record order and line order. The wrapper temporarily replaces each LF delimiter with NUL, calls the MajorMUD special-command line interpreter at `0x00470209`, and advances to the next line. A line result of `1` immediately terminates the scan and is returned as handled. A differing record ID, exhausted fetch, or the 50-fetch bound ends scanning. The record buffer is always returned to the local reuse pool.

No direct imported Worldgroup callback is invoked by this wrapper. Database access and host integration remain beneath the local record-fetch and line-interpreter helpers, which are the next tracing boundaries.
