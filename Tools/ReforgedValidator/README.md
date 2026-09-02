# Reforged Validator

This directory contains the single current-health validation package for Reforged.

Run the complete gate from the repository root:

```powershell
& Tools/ReforgedValidator/Validate.ps1
```

Skip package restore when the current dependency cache is already valid:

```powershell
& Tools/ReforgedValidator/Validate.ps1 -SkipRestore
```

The launcher regenerates Classic data and executes the authoritative .NET audits, gameplay smokes, parity closure, MegaMUD contract, and Borin harnesses. Production gameplay authority remains in the C# server runtime; validation-only C# checks live in `DotNet/Checks`.
