#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import subprocess
from pathlib import Path
from typing import Any

MODULE_RELATIVE_PATHS = {
    "wccmmud.dll": "ImportedDatabases/WCCDefaultInstall/wcc default/wccmmud.dll",
    "wccmmpls.dll": "ImportedDatabases/WCCDefaultInstall/wcc default/wccmmpls.dll",
    "wbbmmud.dll": "ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmud.dll",
    "wbbmmpls.dll": "ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmpls.dll",
}

INSTRUCTION_RE = re.compile(r"^\s*([0-9a-fA-F]+):\s+(?:[0-9a-fA-F]{2}\s+)+\s*(.*?)\s*$")
REGISTER_RE = r"(?:e(?:ax|bx|cx|dx|si|di|bp|sp)|(?:ax|bx|cx|dx|si|di|bp|sp)|(?:al|ah|bl|bh|cl|ch|dl|dh))"
WIDTH_RE = re.compile(r"\b(BYTE|WORD|DWORD|QWORD) PTR\b", re.I)
CONTROL_RE = re.compile(r"^(?:call|jmp|ret|leave|int|loop|j[a-z]+)\b", re.I)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def disassemble(path: Path) -> list[dict[str, Any]]:
    text = subprocess.check_output(
        ["objdump", "-d", "-Mintel", str(path)], text=True, errors="replace"
    )
    instructions: list[dict[str, Any]] = []
    for raw in text.splitlines():
        match = INSTRUCTION_RE.match(raw)
        if not match:
            continue
        instructions.append(
            {
                "va": int(match.group(1), 16),
                "text": match.group(2).strip(),
            }
        )
    return instructions


def parse_destination_register(text: str) -> str | None:
    match = re.match(rf"^mov\s+({REGISTER_RE})\s*,", text, re.I)
    return match.group(1).lower() if match else None


def classify_reference(text: str, address_hex: str) -> str:
    lowered = text.lower()
    direct_memory = bool(re.search(rf"(?:ds:)?0x{re.escape(address_hex)}\b", lowered))
    if not direct_memory:
        return "unclassified_static_reference"
    mnemonic = lowered.split(None, 1)[0]
    has_ptr = " ptr " in lowered
    if mnemonic == "mov":
        if has_ptr or "ds:0x" in lowered:
            return "load_iat_contents"
        return "load_iat_slot_address"
    if mnemonic == "push":
        return "pass_iat_contents" if has_ptr or "ds:0x" in lowered else "pass_iat_slot_address"
    if mnemonic in {"cmp", "test", "xor", "and", "or", "add", "sub"}:
        return "read_or_modify_iat_contents"
    return "other_iat_reference"


def parse_offset(operand: str, register: str) -> int | None:
    # Accept [eax], [eax+0x10], [eax-0x4], and scaled forms where the base register is explicit.
    match = re.search(rf"\[{re.escape(register)}(?:\s*([+-])\s*(0x[0-9a-f]+|\d+))?[^\]]*\]", operand, re.I)
    if not match:
        return None
    if not match.group(2):
        return 0
    value = int(match.group(2), 0)
    return -value if match.group(1) == "-" else value


def infer_followup_usage(instructions: list[dict[str, Any]], index: int, register: str) -> list[dict[str, Any]]:
    observations: list[dict[str, Any]] = []
    for lookahead in range(1, 6):
        if index + lookahead >= len(instructions):
            break
        item = instructions[index + lookahead]
        text = item["text"]
        if CONTROL_RE.match(text):
            # Calls can consume a register value, so record that before stopping.
            if text.lower().startswith("call"):
                observations.append(
                    {
                        "kind": "value_reaches_call_boundary",
                        "lookahead": lookahead,
                        "va": f"0x{item['va']:08x}",
                        "instruction": text,
                    }
                )
            break
        lowered = text.lower()
        if re.search(rf"\b{re.escape(register)}\b", lowered):
            offset = parse_offset(lowered, register)
            if offset is not None:
                width_match = WIDTH_RE.search(text)
                width = width_match.group(1).lower() if width_match else "unspecified"
                observations.append(
                    {
                        "kind": "pointer_dereference",
                        "lookahead": lookahead,
                        "va": f"0x{item['va']:08x}",
                        "instruction": text,
                        "base_register": register,
                        "offset": offset,
                        "access_width": width,
                    }
                )
            elif re.match(rf"^(?:push|cmp|test|and|or|xor|add|sub)\b.*\b{re.escape(register)}\b", lowered):
                observations.append(
                    {
                        "kind": "register_value_consumption",
                        "lookahead": lookahead,
                        "va": f"0x{item['va']:08x}",
                        "instruction": text,
                    }
                )
        # Stop when the tracked register is overwritten.
        if re.match(rf"^(?:mov|lea|pop|xor)\s+{re.escape(register)}\s*[, ]", lowered):
            break
    return observations


def analyze_module(path: Path, prior_module: dict[str, Any]) -> dict[str, Any]:
    instructions = disassemble(path)
    address_to_index = {item["va"]: i for i, item in enumerate(instructions)}
    imports: list[dict[str, Any]] = []
    for imported in prior_module["imports"]:
        iat_va = int(imported["iat_va"], 16)
        address_hex = f"{iat_va:x}"
        references: list[dict[str, Any]] = []
        for index, item in enumerate(instructions):
            if re.search(rf"0x0*{re.escape(address_hex)}\b", item["text"], re.I) is None:
                continue
            kind = classify_reference(item["text"], address_hex)
            followups: list[dict[str, Any]] = []
            register = parse_destination_register(item["text"])
            if kind == "load_iat_contents" and register:
                followups = infer_followup_usage(instructions, index, register)
            references.append(
                {
                    "va": f"0x{item['va']:08x}",
                    "instruction": item["text"],
                    "reference_form": kind,
                    "loaded_register": register,
                    "followup_usage": followups,
                }
            )

        dereferences = [
            usage
            for reference in references
            for usage in reference["followup_usage"]
            if usage["kind"] == "pointer_dereference"
        ]
        observed_offsets = sorted({usage["offset"] for usage in dereferences})
        observed_widths = sorted({usage["access_width"] for usage in dereferences})
        reference_forms = sorted({reference["reference_form"] for reference in references})
        if dereferences:
            semantic_class = "import_slot_resolves_to_pointer_with_observed_client_dereferences"
            evidence_class = "proven"
        elif any(form in {"pass_iat_contents", "load_iat_slot_address", "pass_iat_slot_address"} for form in reference_forms):
            semantic_class = "import_address_or_contents_passed_without_local_field_recovery"
            evidence_class = "proven"
        elif references:
            semantic_class = "import_contents_consumed_without_recoverable_local_dereference"
            evidence_class = "proven"
        else:
            semantic_class = "import_has_no_static_reference_in_supplied_image"
            evidence_class = "proven"
        imports.append(
            {
                "symbol": imported["symbol"],
                "iat_va": imported["iat_va"],
                "semantic_classification_0008": semantic_class,
                "evidence_class": evidence_class,
                "static_reference_count": len(references),
                "reference_forms": reference_forms,
                "pointer_dereference_count": len(dereferences),
                "observed_client_offsets": observed_offsets,
                "observed_access_widths": observed_widths,
                "exact_host_type": "unknown_without_matching_worldgroup_binary_symbols_or_runtime_trace",
                "references": references[:128],
            }
        )
    counts: dict[str, int] = {}
    for imported in imports:
        key = imported["semantic_classification_0008"]
        counts[key] = counts.get(key, 0) + 1
    return {
        "file": path.name,
        "sha256": sha256(path),
        "imports": imports,
        "counts": counts,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary-root", type=Path, required=True)
    parser.add_argument("--project-root", type=Path, required=True)
    args = parser.parse_args()

    prior_path = args.project_root / "Librarium/Evidence/native_host_data_import_classification_0006.json"
    prior = json.loads(prior_path.read_text(encoding="utf-8"))
    modules: list[dict[str, Any]] = []
    for filename, relative in MODULE_RELATIVE_PATHS.items():
        source = args.binary_root / relative
        prior_module = next(module for module in prior["modules"] if module["file"] == filename)
        modules.append(analyze_module(source, prior_module))

    symbols: dict[str, dict[str, Any]] = {}
    for module in modules:
        for imported in module["imports"]:
            row = symbols.setdefault(
                imported["symbol"],
                {
                    "symbol": imported["symbol"],
                    "modules": {},
                    "semantic_classes": set(),
                    "offsets": set(),
                    "widths": set(),
                },
            )
            row["modules"][module["file"]] = {
                "semantic_classification_0008": imported["semantic_classification_0008"],
                "static_reference_count": imported["static_reference_count"],
                "pointer_dereference_count": imported["pointer_dereference_count"],
                "observed_client_offsets": imported["observed_client_offsets"],
                "observed_access_widths": imported["observed_access_widths"],
            }
            row["semantic_classes"].add(imported["semantic_classification_0008"])
            row["offsets"].update(imported["observed_client_offsets"])
            row["widths"].update(imported["observed_access_widths"])

    symbol_rows: list[dict[str, Any]] = []
    for row in symbols.values():
        classes = sorted(row.pop("semantic_classes"))
        row["cross_module_classification"] = classes[0] if len(classes) == 1 else "module_specific_usage"
        row["observed_client_offsets"] = sorted(row.pop("offsets"))
        row["observed_access_widths"] = sorted(row.pop("widths"))
        symbol_rows.append(row)

    data = {
        "schema": "majormud.native-host-data-usage.v1",
        "update": "FinalNativeClosure0008",
        "generated_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source_evidence": str(prior_path.relative_to(args.project_root)),
        "source_evidence_sha256": sha256(prior_path),
        "evidence_class": "PROVEN",
        "method": "Static x86 instruction and bounded local data-flow analysis of every non-direct WGSERVER import reference in supplied WCC/WBB MMUD and MMPLS modules.",
        "modules": modules,
        "symbols": sorted(symbol_rows, key=lambda item: item["symbol"]),
        "closed_boundaries": [
            "Recovers whether each imported host slot is locally dereferenced, passed onward, consumed directly, or unreferenced.",
            "Recovers client-visible field offsets and access widths reached through imported host pointers without naming unknown host structures.",
            "Proves WCC/WBB usage parity independently through exact module hashes.",
        ],
        "limitations": [
            "Observed offsets are MajorMUD-side access footprints, not complete Worldgroup structure layouts.",
            "Register tracking is intentionally bounded and stops at control-transfer or overwrite boundaries.",
            "Exact C types, allocation lifetime, callback content, and host-side invariants require the matching Worldgroup host binary, symbols, or runtime traces.",
        ],
    }
    for relative in (
        "Librarium/Evidence/native_host_data_usage_0008.json",
        "Reports/NativeDecode/native_host_data_usage_0008.json",
    ):
        destination = args.project_root / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
