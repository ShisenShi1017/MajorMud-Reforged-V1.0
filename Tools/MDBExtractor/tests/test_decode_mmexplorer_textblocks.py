import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142"

def load(name):
    return json.loads((OUT / name).read_text(encoding="utf-8"))

def test_complete_decode():
    manifest = load("decode_manifest.json")
    assert manifest["textblock_rows"] == 2279
    assert manifest["decoded_textblocks"] == 2279
    assert manifest["decode_errors"] == 0

def test_orfeo_command_map():
    records = {r["textblock_id"]: r for r in load("textblocks_decoded.json")}
    root = records[929]
    assert root["owner"]["kind"] == "monster"
    assert root["owner"]["references"][0]["number"] == 384
    routes = {c["normalized_keyword"]: c["target_textblock_id"] for c in root["commands"]}
    assert routes["phoenix"] == 1429
    assert routes["dao"] == 2706
    assert routes["dao lord"] == 2706

def test_orfeo_phoenix_and_dao_scripts():
    records = {r["textblock_id"]: r for r in load("textblocks_decoded.json")}
    assert records[1431]["script"] == "checkability 133 3:giveability 133 4:text 1432\n"
    dao = records[2707]["script"]
    assert "checkability 134 11" in dao
    assert "giveability 134 12" in dao
    assert "giveitem 1180" in dao
    assert "failitem 1180" in dao

def test_orfeo_quest_flows_are_scoped():
    flows = [f for f in load("quest_flows_decoded.json") if f["npc_id"] == 384]
    by_name = {f["quest_flag_name"]: f for f in flows}
    assert set(by_name) >= {"PhoenixQuest", "DaoLordQuest"}
    assert by_name["PhoenixQuest"]["item_actions"] == []
    assert any(a["item_id"] == 1180 for a in by_name["DaoLordQuest"]["item_actions"])


def test_no_unknown_or_unclassified_tokens():
    manifest = load("decode_manifest.json")
    assert manifest["unknown_opcodes"] == []
    assert manifest["unclassified_tokens"] == 0
    assert manifest["source_directives"] == 0

def test_source_anomalies_are_corpus_repaired():
    records = {r["textblock_id"]: r for r in load("textblocks_decoded.json")}
    route = next(op for op in records[237]["operations"] if op["opcode"] == "route")
    assert route["target_textblock_id"] == 0
    portal = [op for op in records[9578]["operations"] if op.get("source_repair", {}).get("kind") == "omitted_opcode"]
    assert len(portal) == 1
    assert portal[0]["opcode"] == "teleport"
    assert portal[0]["room_ref"] == "room.map1.2683"
