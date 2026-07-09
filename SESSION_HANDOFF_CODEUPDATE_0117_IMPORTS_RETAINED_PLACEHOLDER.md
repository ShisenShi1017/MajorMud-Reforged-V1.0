# Session Handoff - CodeUpdate 0117

Current baseline: CodeUpdate 0117.

Important architecture rules:

1. Librarium is source of truth.
2. `/Data` is generated runtime output per world.
3. `/ImportedDatabases` must remain in the repo until the Librarium is 100% complete.
4. Imported evidence is used for formula/mechanic discovery and source traceback.
5. Do not remove MBBSEmu, MMUD-Explorer, NightmareRedux, or MajorMUD-PHP-Monster-Sim yet.
6. Host/server/editor builds worlds from Librarium branches.
7. Client reads only the active world view.

Next work: continue Librarium completion/enrichment using retained imported sources.
