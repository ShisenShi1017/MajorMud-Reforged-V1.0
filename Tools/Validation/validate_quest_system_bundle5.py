from __future__ import annotations
import json
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
errors: list[str] = []

def require(value: bool, message: str) -> None:
    if not value:
        errors.append(message)

source_dir = root / "Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142"
source = source_dir / "quest_flows_decoded.json"
manifest_path = source_dir / "decode_manifest.json"
require(source.is_file(), "decoded 0142 quest-flow source missing")
require(manifest_path.is_file(), "decoded 0142 manifest missing")

if source.is_file() and manifest_path.is_file():
    flows = json.loads(source.read_text(encoding="utf-8"))
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    require(len(flows) == 83, f"expected 83 decoded quest flows, found {len(flows)}")
    require(manifest.get("quest_flows") == 83, "0142 manifest quest-flow count is not 83")
    require(manifest.get("unknown_opcodes") == [], "0142 decoder still reports unknown opcodes")
    require(manifest.get("unclassified_tokens") == 0, "0142 decoder still reports unclassified tokens")
    require(manifest.get("source_directives") == 0, "0142 decoder still reports unresolved source directives")
    ids = {flow.get("id") for flow in flows}
    require(len(ids) == 83 and None not in ids, "decoded quest-flow IDs are missing or duplicated")
    require(all(isinstance(flow.get("stages"), list) and flow["stages"] for flow in flows),
            "decoded flows require ordered stages")
    for flow in flows:
        for stage in flow.get("stages", []):
            for operation in stage.get("operations", []):
                require(operation.get("status") == "decoded",
                        f"{flow.get('id')} contains non-decoded operation {operation.get('raw')}")

records_path = root / "Librarium/Objects/quests/quest_records_0123.json"
require(records_path.is_file(), "canonical quest records missing")
if records_path.is_file():
    records = json.loads(records_path.read_text(encoding="utf-8")).get("records", [])
    promoted = [r for r in records if r.get("classification") == "npc_textblock_quest_flow"]
    require(len(promoted) == 83, f"expected 83 promoted canonical flows, found {len(promoted)}")

compiler = (root / "Apps/Compiler/src/LibrariumCatalogReader.cs").read_text(encoding="utf-8")
for marker in ["TextblocksDecoded*", '"classification"] = "npc_textblock_quest_flow"', '"status"] = "executable"', "authoritative_script_graph"]:
    require(marker in compiler, f"compiler quest promotion marker missing: {marker}")

runtime = (root / "Apps/Source/Reforged.Server/RuntimeInteractionNarrative.cs").read_text(encoding="utf-8")
for marker in ["TryGetExecutable", "RuntimeGeneratedQuestPolicy", "RuntimeQuestJournalPersistence", "unknown-or-non-executable-quest"]:
    require(marker in runtime, f"runtime marker missing: {marker}")

smoke = (root / "Apps/Source/Reforged.Server/RuntimeInteractionNarrativeSmokeReport.cs").read_text(encoding="utf-8")
for marker in ["decoded-executable-quests", "pending-evidence-blocked", "persistence", "rollback", "abandon"]:
    require(marker in smoke, f"quest smoke marker missing: {marker}")

if errors:
    for error in errors:
        print(f"FAIL: {error}")
    sys.exit(1)
print("PASS: Bundle 5 uses 83 authoritative decoded quest flows with zero unknown operations.")
