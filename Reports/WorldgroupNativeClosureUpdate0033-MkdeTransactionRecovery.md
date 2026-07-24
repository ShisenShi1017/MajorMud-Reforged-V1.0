# Worldgroup Native Closure Update0033 — MKDE Transaction and Recovery Lifecycle

## Proven startup ordering

`W32MKDE.EXE` contains a recovery entry at `0x0042C340`. The implementation constructs installation-relative paths for `BTRVLOG.RCV` and `BTRVLOG.TRN`, probing the recovery artifact first and entering separate open/read/validation branches for the two files.

The embedded source-component provenance places these paths in Btrieve 6.20 `RECOVERY.C` and `TRANSLOG.C`. `BTR_TMP.$??` is owned by `BKGROUND.C`, while `%8X.LOG` is a transaction-log filename template owned by `TRANSLOG.C`.

## Durability conclusion

The previously recovered low-level write path is write-back. Ordinary successful writes can increment pending state without immediately calling `FlushFileBuffers`. Therefore, a write that has not crossed one of the proven explicit or conditional flush boundaries is not statically proven power-loss durable.

Selected recovery/log serialization paths force explicit synchronous flushes at `0x00427BBF` and `0x00428336`. This proves durability barriers around selected log/recovery finalization, but not universal write-through or an exact transaction commit protocol.

## Bounded unknowns

Static evidence does not yet prove the exact TRN/RCV record layouts, whether records are undo preimages or redo images, the precise rename/delete cleanup sequence, or whether MajorMUD mutations are automatically grouped into host-owned transactions.
