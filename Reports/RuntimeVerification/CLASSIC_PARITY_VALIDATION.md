- CodeUpdate0363 Items & Inventory verification: **66/66 native/source/data PASS**; category 6/6 PASS with canonical 1,950-item generation, ordered ability slots, exact first-CastsSp no-target return, EAT/DRINK +0xCE/+0xD0 fallback state, signed Uses/retention, DROP gating, and APPRAISE delegation. No .NET SDK is available here, so no new local C# build/runtime claim is made.
# Classic Parity Validation
- CodeUpdate0362 Import / Export / Limits / Recovery verification: **86/86 native/data/static PASS**; category 4/4 PASS with exact capacity ownership, external native-file replacement, state-preserving rejected gameplay, owner-local failure boundaries, and record-local persistence semantics. No .NET SDK is available here, so no new local C# build/runtime claim is made.
- CodeUpdate0361 Host / Persistence / Worldgroup verification: **92/92 native/static PASS**; category 9/9 PASS, exact Player/Buffer save ownership, transaction/durability boundary, RNG host contract, repository inventory closure, and supplied-WCC exhaustion. No .NET SDK is available here, so no new local C# build/runtime claim is made.
- CodeUpdate0360 Economy & Services verification: **41/41 source/native-data PASS** under the cumulative 0361 ledger; category 16/16 PASS and 204/204 services classified.
- CodeUpdate0359 Data Reconstruction verification: **34/34 direct raw/static PASS**; 39/39 decode families, 2,096/2,096 connected/structural/explicit-empty scalar fields, and canonical 3,869-message WCCMSG2 authority independently corroborated by all 3,867 shared payloads in the supplied working install. No .NET SDK is available here, so no new C# build/runtime claim is made.
- CodeUpdate0358 working-install/native LVLDIFF verification: **14/14 direct PASS**, **124/124 static/integration PASS**; default 100, working 10, -1 disable sentinel, 0 same-level-only, lower-level FIEND exception; Worldgroup remains evidence-only.

The cumulative ledger now retains **167 PASS / 33 STRONG / 0 UNKNOWN** assertions. Native verifier results for character death, death-floor placement, session lifecycle, party coordination, gang communications, modal invitations, daily cleanup, and organization services are retained in one generic consolidated report.

The historical pass-specific validators and smoke command aliases are retired. `Tools/validator.py` is the single current-health entrypoint, and `--smoke-classic-parity-closure` is the retained runtime parity gate. Current build and smoke results are recorded in the repository manifests and continuity documents.

## Current verification record

- CodeUpdate0357 native stock-Accuracy verification: direct original-DLL/MegaMUD evidence closes the fighter +0x00 builder, including class combat, floor-sqrt(Level), Level/2, signed equipped Accuracy/pity, the living `<33%` encumbrance fold, STR/3 and AGI/6. Reforged removes the old approximation and weapon double-add. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0356 native raw-Dodge verification: direct original-DLL/MegaMUD evidence **40/40 PASS**; `_move_player_to_fighter` closes ability-34, Level/Charm/Agility, signed truncation, and the below-33-percent encumbrance bonus before `_calculate_attack` consumes raw Dodge. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0355 native physical-combat verification: direct original-DLL/MegaMUD evidence **45/45 PASS**; Backstab endpoint construction and general physical hit/critical/damage/DR/Bash-Smash/Dodge ordering are closed. Reforged's Bash/Smash post-DR multiplier was moved before Dodge to match the direct native branch. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0354 BUY/SELL Charm pricing verification: direct imported-native/MegaMUD evidence **29/29 PASS**; `_buy_item` and `_sell_item` close exact Charm factors, Charm-0 behavior, integer truncation/order, markup presence/absence, and ordinary-stream MegaMUD/Borin authority. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0353 GIVE capacity/presentation verification: direct imported-native/MegaMUD evidence **24/24 PASS**; item restriction ordering, RECEIVE-off refusal, 100-root-slot/encumbrance admission, capacity rejection, exact success presentation, and High-priority room delivery are reconciled. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0352 BREAK ownership verification: direct imported-native/MegaMUD evidence **21/21 PASS**; native BREAK is character-only, NPC/PvP opponent attack ownership remains independent, and Borin/client state separates incoming danger from local autocombat. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0351 BREAK/stock-extension verification: direct imported-native/MegaMUD evidence **27/27 PASS**; exact active BREAK player/observer presentation and display-before-teardown order are connected, while per-user PvP autocombat ownership remains STRONG. Native command/service censuses close stock TRADE-escrow and REPAIR absence. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0350 REST/MEDITATE verification: direct imported-native evidence **24/24 PASS** and repository static integration **96/96 PASS**; the native command branch, sickness/full-mana presentation, MegaMUD-visible strings, and Borin/shared client parser contract are reconciled. This environment has no .NET SDK, so no new local C# build/runtime claim is made.

- CodeUpdate0348 host-scope/builder static verification: see `codeupdate0348_host_scope_builder_static_verification.json`; Worldgroup host PAGE/telegram plumbing is out of Reforged parity scope, while player-visible SET MESSAGES filtering remains tracked.

- CodeUpdate0347 native/static TALK/RESPONSE/PAGE verification: see `codeupdate0347_native_talk_page_static_verification.json`; this environment has no .NET SDK, so no new local C# build/runtime claim is made.
- CodeUpdate0345 native/static communication verification: **PASS 59/59**, 0 failures, against the retained original WCCMMUD.DLL and imported MegaMUD runtime log; see `codeupdate0345_communication_native_static_verification.json`. The analysis environment did not contain the .NET 8 SDK, so this line does **not** claim a new local C# build/runtime execution.
- Retained 0338 package native death-floor fingerprint/callsite/source result: **PASS 88/88**, 0 failures.

- Retained 0337 package native character death/lifecycle fingerprint/callsite/source result: **PASS 60/60**, 0 failures.
- Retained 0336 package native scheduler/regeneration fingerprint/callsite/source result: **PASS 69/69**, 0 failures.
- Retained 0335 package native NPC combat/RNG result: **PASS 49/49**, 0 failures.
- Retained 0334 package native DLL formula/callsite result: **PASS 66/66**, 0 failures.
- Assertion ledger/scorecard reconciliation: **PASS**, 167 PASS / 33 STRONG / 0 UNKNOWN.
- Consolidated native/direct-static verification results: **PASS**, 2,529 checks across 26 subsystems, 0 failures.
- Reforged.Server, Reforged.App, Reforged.Client, and core/server test Release builds with warnings as errors: **PASS**, 0 warnings / 0 errors.
- Clean Classic generation: **PASS**, 103,330 objects across 36 families and 26,668/26,668 room descriptions.
- Bundle D trap audit: **PASS**, 295/295 traps with 293 active and 2 hidden native states.
- Combat completion, combat runtime, full live-combat round, clock scheduling, and Classic parity closure smokes: **PASS**, 0 failures.
- Numbered one-off native validators were not restored. Their supplied results are retained in `native_runtime_verification.json` and are not presented as a new local execution.
- Broader item smoke: **FAIL**, two pre-existing corpse-resurrection equipment assertions remain open; all newly affected focused gates pass.
- GitHub Actions/build validation is owned by the user or Codex after they choose to push; this parity pass performs no GitHub write.

- CodeUpdate0346 static suicide/trap verification: see `codeupdate0346_suicide_traps_static_verification.json`; this environment has no .NET SDK, so no new local build claim is made.
## CodeUpdate0364 NPC & AI closure

- CodeUpdate0364 NPC & AI closure: 13/13 PASS; direct native/source/static verification 32/32 PASS; overall parity 167 PASS / 33 STRONG / 0 UNKNOWN (83.5% strict). MonsGuards is ordered same-room spell-target substitution; native fast/medium/slow generation ownership is connected; generic threat/perception/flee/patrol remain non-Classic Reforged extensions.
- Evidence: `Librarium/Parity/majormud_npc_ai_complete_evidence_0364.json`.
- Verification: `Reports/RuntimeVerification/classic_npc_ai_verification.json`.
- Target runtime gate: `--smoke-classic-npc-ai`; full gate: `--smoke-classic-parity-closure`.
