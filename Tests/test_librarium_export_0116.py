import json
import subprocess
from pathlib import Path


def test_librarium_classic_export_generates_data():
    root = Path(__file__).resolve().parents[1]
    result = subprocess.run([
        "python", str(root / "Tools" / "Librarium" / "export_librarium_to_data.py"),
        "--world", "classic", "--clean"
    ], cwd=root, text=True, capture_output=True)
    assert result.returncode == 0, result.stderr + result.stdout
    out = root / "Data" / "Classic"
    assert (out / "world.json").exists()
    assert (out / "mechanics.json").exists()
    mechanics = json.loads((out / "mechanics.json").read_text())
    assert len(mechanics.get("mechanics", [])) >= 1
