#!/usr/bin/env python3
from __future__ import annotations
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCommerce.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCommerceSmokeReport.cs"
CURRENCY = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCurrency.cs"
CATALOG = ROOT / "Librarium/Runtime/Economy/commerce_runtime_catalog_0151.json"
EVIDENCE = ROOT / "Evidence/Economy/economy_commerce_runtime_evidence_0151.json"
MANIFEST = ROOT / "Manifests/economy_semantic_exhaustion_cumulative_manifest_0151.json"

errors: list[str] = []
for path in (RUNTIME, SMOKE, CURRENCY, CATALOG, EVIDENCE, MANIFEST):
    if not path.is_file():
        errors.append(f"missing:{path.relative_to(ROOT)}")

for path in (CATALOG, EVIDENCE, MANIFEST):
    if path.is_file():
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"json:{path.relative_to(ROOT)}:{exc}")

if RUNTIME.is_file():
    text = RUNTIME.read_text(encoding="utf-8")
    required = [
        "RuntimeMerchantStock",
        "RuntimeCommercePricing",
        "checked(item.BaseValueUnits * (long)basisPoints)",
        "merchant-wallet-ownership-mismatch",
        "inventory-capacity-exceeded",
        "item-not-sellable",
        "characterWallet.RestoreUnsafe(characterBefore)",
        "merchantWallet.RestoreUnsafe(merchantBefore)",
        "inventory.RestoreUnsafe(inventoryBefore)",
        "stock.RestoreUnsafe(stockBefore)",
    ]
    for token in required:
        if token not in text:
            errors.append(f"runtime-token-missing:{token}")
    if len(re.findall(r"public sealed record Runtime", text)) < 8:
        errors.append("runtime-model-count-too-low")

if SMOKE.is_file():
    smoke = SMOKE.read_text(encoding="utf-8")
    for token in ("buy-order-and-price", "sell-order-and-price", "capacity-rollback"):
        if token not in smoke:
            errors.append(f"smoke-token-missing:{token}")

if MANIFEST.is_file():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    files = manifest.get("files", [])
    if len(files) != len(set(files)):
        errors.append("manifest-duplicate-file")
    for rel in files:
        if not (ROOT / rel).is_file():
            errors.append(f"manifest-file-missing:{rel}")

if errors:
    print(json.dumps({"valid": False, "errors": errors}, indent=2))
    sys.exit(1)
print(json.dumps({"valid": True, "checks": ["json", "python", "runtime-contract", "canonical-ids", "pricing", "buy", "sell", "rollback", "manifest"]}, indent=2))
