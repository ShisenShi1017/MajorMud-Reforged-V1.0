import importlib.util
import sys
from pathlib import Path

MODULE_PATH = Path(__file__).parents[1] / "decode_mmexplorer_rooms.py"
spec = importlib.util.spec_from_file_location("decoder", MODULE_PATH)
decoder = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = decoder
spec.loader.exec_module(decoder)


def test_open_exit():
    result = decoder.parse_exit("1/100", "n")
    assert result["to_map_number"] == 1
    assert result["to_room_number"] == 100
    assert result["gate_kind"] == "open"
    assert result["runtime_disposition"] == "traversable"


def test_door_exit():
    result = decoder.parse_exit("1/1381 (Door)", "e")
    assert result["gate_kind"] == "door"
    assert result["qualifier"] == "Door"


def test_text_exit():
    result = decoder.parse_exit("16/50 (Text: climb tree)", "u")
    assert result["gate_kind"] == "text_command"
    assert result["commands"] == ["climb tree"]


def test_suffix_is_preserved():
    result = decoder.parse_exit("10/16R", "s")
    assert result["to_room_number"] == 16
    assert result["reference_suffix"] == "R"
