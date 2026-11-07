# RealmGate Version

- Project Version: `0.1.0`
- Current Code Update: `0142`
- Current Database Version: `0010`
- Milestone: `CodeUpdate0142 - RealmGate Client and Character Integration`
- Classic Parity: `200 PASS / 0 STRONG / 0 UNKNOWN`
- Current-Health Validator: `29/29 PASS`
- RealmGate Client Smokes: `14/14 PASS`
- Cross-Platform Client Core: `62/62 PASS`
- Release Build Baseline: `RealmGate app Release build and full solution — 0 warnings / 0 errors` in the current local verification
- Release Identity: `RG icon and RealmGate metadata embedded; trusted Authenticode signing remains pending`
- CodeUpdate0142 Validator: `PASS — 0 issues / 0 warnings / 0 errors`
- CodeUpdate0142 Client Smokes: `14/14 PASS`
- Generated World Contract: `standalone and runtime-complete; live Classic fallback forbidden`
- Decode Exhaustion: `49/49 families; 2,332/2,332 fields; 0 unresolved`
- Post-Closure Runtime Hardening: `world/session fault containment; Host sessions/logs; realm-scoped character deletion; LIGHT equipment readiness`
- Latest Local Verification: `29/29 current-health validator; solution build 0 warnings / 0 errors; Classic room/dialogue regression 7,679/7,679; 81/81 client-core regression and 30/30 Creator-core regression passed. Latest Avalonia Release build includes protected Custom1 source import, schema-driven editing, validation, isolated Test Realm generation/session hosting, guarded publish/rollback, portable packages, room connections, shop inventory, NPC attack/ability collection editing, executable quest-stage/runtime-script editing, shared-runtime NPC/weapon simulation, visual map editing, read-only World Tools, guarded Creator maintenance, Custom1 world settings, realm-scoped character administration, Classic local policy/SYSOP administration, the read-only Librarium browser, merge-preserving Windows/XDG application defaults, and file-free platform credential selection. It passed with 0 errors, the 2 known SDK/analyzer-version warnings, and restricted-network NU1900 vulnerability-index warnings. The transitional WPF Release build passed with 0 errors and only restricted-network NU1900 warnings in this slice. The current Test Realm generated 44 families, 112,691 objects, and 60 files; all 60 passed read-only projection validation. Its headless lifecycle smoke verified isolated activation, paired listeners, Custom1 data authority, `Custom1-Test` save isolation, and graceful shutdown. Live Custom1 was not published, and administration verification changed no policy, character, or Custom1 data. Windows/Linux preview publishes predate this slice; native Linux GUI/keyring interaction, manual Test Realm gameplay, a real reviewed live publish, a real matching-source Creator import/runtime test, and a real external-board field test remain untested.`
- Avalonia Librarium: `PASS — lazy Classic category browsing, name-only lists, curated gameplay details, chunk-manifest support, string-backed weapon classification, and evidence/provenance filtering are covered by the 30/30 Creator Core regression. Final Avalonia Release build: 0 errors; the two known analyzer-version warnings and restricted-network NU1900 package-audit warnings were emitted.`
- Avalonia Application Defaults and Credentials: `PASS — default world/profile and Host bind/port/session limits round-trip through a shared Windows/XDG store; unknown WPF settings are preserved. Secure values route to Windows Credential Manager or Linux Secret Service with no file fallback. Client Core: 81/81. WPF and Avalonia Release builds: 0 errors; known analyzer-version and restricted-network package-audit warnings remain. Real Linux keyring interaction is unverified.`
- Avalonia Tester Packaging: `BETA.2 WINDOWS CANDIDATE PASS — from empty Data, the packaged application regenerated Classic and clean Custom1 at 44 families, 112,609 objects, and 60 files each, started paired listeners, and stopped cleanly before archive creation. The final one-root archive contains 269 entries, the 57-family/114,630-object Librarium, no generated Data, and no Borin/private dependency names. Published beta.1 predates this fix. Native-runner beta.2 Linux packaging and Linux GUI/Host/keyring testing remain unverified.`

## Numbering

Local complete Classic room/dialogue recovery verification passed on 2026-10-25. The accepted CodeUpdate0142 identity remains unchanged; this subsequent local work has not been pushed or checked by GitHub Actions.

`CodeUpdate0142` is the repository update number.

The `038x` ZIP names used during the client implementation/testing conversation are patch-package sequence numbers only. They do not replace or advance the repository CodeUpdate number.

## Remaining integration certification

The automated baseline is clean. Real-file certification remains for:

- an external non-RealmGate BBS MDB; and
- a Creator Studio custom-world MDB with its matching WCC/WGS source set.
