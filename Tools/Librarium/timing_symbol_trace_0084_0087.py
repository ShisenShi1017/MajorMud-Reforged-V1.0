#!/usr/bin/env python3
"""Static timing evidence extractor for CodeUpdate 0084-0087.
Scans checked-in imported WCC/WBB binaries via strings and source-tree text for timing symbols.
This tool does not modify runtime data.
"""
from __future__ import annotations
import json, re, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SYMBOLS = [
    "_begin_polling", "_rtkick", "_syscyc", "_my_rtkick",
    "_majormud_syscyc", "_background_fast", "_background_medium", "_background_slow",
    "_background_energy", "_background_inactivity", "_background_monster_create",
    "_background_other", "_background_save_buffers", "_background_update_exits",
    "_do_autocombat", "_do_autocombat_for_user", "_restart_autocombat",
    "_delay_done", "_add_delay", "_add_delayed_command",
    "_ljngame_stsrou", "_ljngame_sttrou",
]
TEXT_PATTERNS = ["RTKICK", "RTIHDLR", "TICKER", "BEGIN_POLLING", "ProcessRTKICK", "ProcessRTIHDLR", "ProcessSYSCYC", "ROUND_SECS", "CommandDelay", "CombatPulse", "TickSyncBuffer"]

def strings_for(path: Path) -> str:
    try:
        return subprocess.check_output(["strings", "-a", str(path)], text=True, errors="ignore")
    except Exception as exc:
        return f"ERROR: {exc}"

def main() -> None:
    outdir = ROOT / "Evidence" / "CodeUpdate0084_0087"
    outdir.mkdir(parents=True, exist_ok=True)
    binaries = [
        ROOT / "ImportedDatabases" / "WCCDefaultInstall" / "wcc default" / "wccmmud.dll",
        ROOT / "ImportedDatabases" / "WCCLabeledWBB" / "Extracted" / "wbbmmud.dll",
        ROOT / "ImportedDatabases" / "WCCDefaultInstall" / "wcc default" / "wccmmpls.dll",
        ROOT / "ImportedDatabases" / "WCCLabeledWBB" / "Extracted" / "wbbmmpls.dll",
    ]
    binary_hits = []
    for binpath in binaries:
        if not binpath.exists():
            continue
        text = strings_for(binpath)
        for sym in SYMBOLS:
            if sym.lower() in text.lower():
                binary_hits.append({"file": str(binpath.relative_to(ROOT)), "symbol": sym, "match_type": "strings"})
        # capture nearby timing/combat strings as corroborating context
        for line in text.splitlines():
            if re.search(r"combat|regen|round|delay|autocombat|background|syscyc|rtkick|polling", line, re.I):
                binary_hits.append({"file": str(binpath.relative_to(ROOT)), "symbol": line[:160], "match_type": "context_string"})
    (outdir / "wcc_wbb_binary_timing_symbol_hits_0085.json").write_text(json.dumps(binary_hits, indent=2))
    
    text_hits = []
    scan_roots = [ROOT / "Evidence", ROOT / "Librarium", ROOT / "Documentation", ROOT / "Reports", ROOT / "Validation"]
    for base in scan_roots:
        if not base.exists():
            continue
        for path in base.rglob("*"):
            if path.is_file() and path.suffix.lower() in {".json", ".md", ".txt", ".csv"}:
                try:
                    content = path.read_text(errors="ignore")
                except Exception:
                    continue
                for patt in TEXT_PATTERNS:
                    if patt.lower() in content.lower():
                        text_hits.append({"file": str(path.relative_to(ROOT)), "pattern": patt})
    (outdir / "repo_timing_text_hits_0084_0087.json").write_text(json.dumps(text_hits, indent=2))
    print(json.dumps({"binary_hits": len(binary_hits), "text_hits": len(text_hits)}, indent=2))

if __name__ == "__main__":
    main()
