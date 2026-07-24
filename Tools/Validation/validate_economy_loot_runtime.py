#!/usr/bin/env python3
from __future__ import annotations
import json
import pathlib
import py_compile
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyLoot.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyLootSmokeReport.cs"
CURRENCY = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCurrency.cs"
COMMERCE = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCommerce.cs"
CATALOG = ROOT / "Librarium/Runtime/Economy/loot_runtime_catalog_0153.json"
EVIDENCE = ROOT / "Evidence/Economy/economy_loot_runtime_evidence_0153.json"
DOC = ROOT / "Docs/Economy/ECONOMY_LOOT_RUNTIME_PASS_0153.md"
MANIFEST = ROOT / "Manifests/economy_semantic_exhaustion_cumulative_manifest_0153.json"

errors: list[str] = []
for path in (RUNTIME, SMOKE, CURRENCY, COMMERCE, CATALOG, EVIDENCE, DOC, MANIFEST):
    if not path.is_file(): errors.append(f"missing:{path.relative_to(ROOT)}")
for path in (CATALOG, EVIDENCE, MANIFEST):
    if path.is_file():
        try: json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc: errors.append(f"json:{path.relative_to(ROOT)}:{exc}")
try: py_compile.compile(__file__, doraise=True)
except Exception as exc: errors.append(f"python:{exc}")
if RUNTIME.is_file():
    text = RUNTIME.read_text(encoding="utf-8")
    required = [
        "RuntimeLootTableDefinition", "RuntimeLootResolver", "RuntimeLootCorpse", "RuntimeLootClaimEngine", "RuntimeLootPersistence",
        "loot-access-denied", "loot-corpse-expired", "loot-currency-mismatch", "loot-cleanup-not-due",
        "corpse.RestoreUnsafe(corpseBefore)", "wallet.RestoreUnsafe(walletBefore)", "inventory.RestoreUnsafe(inventoryBefore)",
        "AppliedClaimIds", "CurrentSchemaVersion = 1", "checked(currency + chosen.CurrencyBaseUnits)", "OrderBy(x => x.Item1"
    ]
    for token in required:
        if token not in text: errors.append(f"runtime-token-missing:{token}")
if SMOKE.is_file():
    smoke = SMOKE.read_text(encoding="utf-8")
    for token in ("deterministic-weighted-resolution", "corpse-rights-enforced", "atomic-claim-all", "idempotent-claim-replay", "persistence-roundtrip"):
        if token not in smoke: errors.append(f"smoke-token-missing:{token}")
if EVIDENCE.is_file():
    evidence = json.loads(EVIDENCE.read_text(encoding="utf-8"))
    classes = {x.get("classification") for x in evidence.get("findings", [])}
    if not {"PROVEN", "STRONGLY CORRELATED", "UNKNOWN"}.issubset(classes): errors.append("evidence-classification-coverage")
if MANIFEST.is_file():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    files = manifest.get("files", [])
    if len(files) != len(set(files)): errors.append("manifest-duplicate-file")
    for rel in files:
        if not (ROOT / rel).is_file(): errors.append(f"manifest-file-missing:{rel}")
if errors:
    print(json.dumps({"valid": False, "errors": errors}, indent=2)); sys.exit(1)
print(json.dumps({"valid": True, "checks": ["json", "python", "csharp-structure", "runtime-references", "loot-ids", "corpse-ids", "item-ids", "currency-ids", "owner-rights", "deterministic-resolution", "claim-order", "rollback", "replay", "cleanup", "persistence", "manifest"]}, indent=2))
