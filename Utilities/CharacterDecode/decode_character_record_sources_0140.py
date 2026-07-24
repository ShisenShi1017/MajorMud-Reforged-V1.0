#!/usr/bin/env python3
"""Inventory native MajorMUD user/save sources without assigning unproven field meanings."""
from __future__ import annotations
import argparse, hashlib, json, math
from collections import Counter
from pathlib import Path

SOURCES = (
    ("wccusers_native", "ImportedDatabases/NativeRuntime/WCCUSERS.DAT"),
    ("wccuser2_default", "ImportedDatabases/WCCDefaultInstall/wcc default/wccuser2.dat"),
    ("wbbuser2_labeled", "ImportedDatabases/WCCLabeledWBB/Extracted/WBBUSER2.DAT"),
)

def entropy(chunk: bytes) -> float:
    if not chunk: return 0.0
    counts = Counter(chunk); n=len(chunk)
    return round(-sum((c/n)*math.log2(c/n) for c in counts.values()), 6)

def printable_runs(data: bytes, minimum: int = 4):
    out=[]; start=None
    for i,b in enumerate(data+b"\x00"):
        if 32 <= b <= 126:
            start = i if start is None else start
        elif start is not None:
            if i-start >= minimum:
                out.append({"offset":start,"length":i-start,"text":data[start:i].decode("ascii")})
            start=None
    return out

def analyze(root: Path, source_id: str, relative: str):
    path=root/relative; data=path.read_bytes(); block=1024
    return {
      "source_id":source_id,"path":relative,"size_bytes":len(data),
      "sha256":hashlib.sha256(data).hexdigest(),
      "header_hex":data[:64].hex(),
      "zero_bytes":data.count(0),"nonzero_bytes":len(data)-data.count(0),
      "printable_ascii_runs":printable_runs(data)[:200],
      "blocks":[{"offset":o,"length":len(c),"sha256":hashlib.sha256(c).hexdigest(),"entropy":entropy(c),"nonzero_bytes":len(c)-c.count(0)} for o in range(0,len(data),block) for c in [data[o:o+block]]],
      "semantic_status":"container_structure_only",
      "field_assignment_policy":"unknown bytes preserved; no runtime meaning assigned",
    }

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--root",default="."); ap.add_argument("--output",default="Data/NativeDecodeReports/CodeUpdate0140Character/character_record_source_inventory_0140.json")
    a=ap.parse_args(); root=Path(a.root).resolve(); output=root/a.output
    records=[analyze(root,*src) for src in SOURCES]
    report={
      "schema_version":1,"code_update":"0140","subsystem":"character","pass":"record_structure_source_inventory",
      "classification":"proven_source_inventory_with_unknown_payload_semantics",
      "sources":records,
      "comparisons":[{"left":records[i]["source_id"],"right":records[j]["source_id"],"same_size":records[i]["size_bytes"]==records[j]["size_bytes"],"same_sha256":records[i]["sha256"]==records[j]["sha256"]} for i in range(len(records)) for j in range(i+1,len(records))],
      "unresolved":[
        "native container header field meanings",
        "record boundaries and allocation tables",
        "character field offsets, flags, enumerations, padding, and version deltas",
        "save checksum or validation algorithm, if present",
      ],
      "runtime_promotion":"none; evidence-only until field semantics are proven or strongly correlated"
    }
    output.parent.mkdir(parents=True,exist_ok=True); output.write_text(json.dumps(report,indent=2)+"\n")
    print(output)
if __name__=="__main__": main()
