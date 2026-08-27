# Code Update 0286 — Borin Memory Sync Validation stderr Repair

## Purpose

Repair a false-negative in `Tools/Borin/run_memory_sync_validation.ps1` on Windows PowerShell.

## Proven failure

`git push --dry-run` can write a normal success/status message such as `Everything up-to-date`
to stderr. With `$ErrorActionPreference = "Stop"`, Windows PowerShell may surface that stderr line
as a terminating `NativeCommandError` even though Git returned exit code 0.

## Change

The push dry-run now temporarily uses non-terminating native-command handling, captures both
stdout and stderr, records `$LASTEXITCODE`, restores the prior PowerShell error preference, and
uses the Git exit code as the authoritative pass/fail signal.

A successful status message is retained in the JSON report as `pushDryRunDetail` rather than
being treated as a validation failure.

## Safety

The validation still uses `git push --dry-run`; it does not modify the private Borin-Memory
repository. A non-zero Git exit code still fails validation.

## Cumulative packaging

This package retains every file created or updated by Borin Code Updates 0271 through 0285,
updates the memory-sync validation script in place, and adds this 0286 documentation once.
