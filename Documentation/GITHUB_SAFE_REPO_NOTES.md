# GitHub-Safe Repository Notes

This repository is organized so it can be pushed to GitHub without using Git LFS.

## Important rule

The Librarium is canonical. Large compiled graph files are local generated artifacts.

Do commit:

- `Librarium/Records/`
- `Librarium/Relationships/`
- `Librarium/Evidence/`
- `Librarium/Assertions/`
- `Librarium/Schemas/`
- `Librarium/Compiler/`
- source data manifests, validation reports, documentation, tools, and handoff docs

Do not commit:

- `Librarium/Graph/*.json`
- `Librarium/FormulaValidation/formula_assertions_*.json`
- `.local-generated/`
- generated runtime/export artifacts over GitHub limits

## Why

GitHub blocks normal Git pushes containing files over 100 MB. The compiled graph files can be rebuilt locally from Librarium source records and should not be treated as hand-edited source.
