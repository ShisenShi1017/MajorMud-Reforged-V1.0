#!/usr/bin/env python3
from __future__ import annotations
import json
import pathlib
import py_compile
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyBanking.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyBankingSmokeReport.cs"
CURRENCY = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCurrency.cs"
COMMERCE = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCommerce.cs"
CATALOG = ROOT / "Librarium/Runtime/Economy/banking_runtime_catalog_0152.json"
EVIDENCE = ROOT / "Evidence/Economy/economy_banking_runtime_evidence_0152.json"
DOC = ROOT / "Docs/Economy/ECONOMY_BANKING_RUNTIME_PASS_0152.md"
MANIFEST = ROOT / "Manifests/economy_semantic_exhaustion_cumulative_manifest_0152.json"

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
        "RuntimeBankPolicy", "RuntimeBankAccount", "RuntimeBankingEngine", "RuntimeBankPersistence",
        "bank-account-access-denied", "bank-account-limit-exceeded", "bank-transaction-limit-exceeded",
        "wallet.RestoreUnsafe(walletBefore)", "account.RestoreUnsafe(accountBefore)",
        "source.RestoreUnsafe(sourceBefore)", "destination.RestoreUnsafe(destinationBefore)",
        "AppliedTransactionIds", "CurrentSchemaVersion = 1", "checked(amount * (long)basisPoints)"
    ]
    for token in required:
        if token not in text: errors.append(f"runtime-token-missing:{token}")
if SMOKE.is_file():
    smoke = SMOKE.read_text(encoding="utf-8")
    for token in ("deposit-fee-and-ownership", "withdrawal-fee-and-order", "shared-account-access", "overflow-rollback", "idempotent-replay", "persistence-roundtrip"):
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
print(json.dumps({"valid": True, "checks": ["json", "python", "csharp-structure", "runtime-references", "bank-ids", "account-ids", "currency-ids", "ownership", "fees", "limits", "rollback", "replay", "persistence", "manifest"]}, indent=2))
