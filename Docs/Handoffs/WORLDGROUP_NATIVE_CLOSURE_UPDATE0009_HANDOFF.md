# Worldgroup Native Closure Update0009 Handoff

Update0009 corrects the command-dispatch ownership boundary using the actual WCCMMUD PE import table and disassembly. `0x004789CA` is `WGSERVER!_injacr`; `0x0049D4D8` and `0x0049D48C` are the `_globs` and `_nglobs` imported host globals. The active callback loop is therefore the Worldgroup global-command chain.

Next pass: decode the WCCMMUD session-state jump table beginning at `0x00405221/0x004052AA`, identify the active gameplay parser branches, and follow proven branches toward textblock and `remoteaction` execution.
