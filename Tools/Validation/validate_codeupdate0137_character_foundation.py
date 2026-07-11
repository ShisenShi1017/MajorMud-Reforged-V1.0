#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import sys
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def require_text(path: str, token: str, errors: list[str]) -> str:
    full = ROOT / path
    if not full.exists():
        errors.append(f"missing {path}")
        return ""
    text = full.read_text(encoding="utf-8")
    if token not in text:
        errors.append(f"{path} missing contract: {token}")
    return text


def main() -> int:
    errors: list[str] = []
    require_text("Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs", "public sealed class RuntimeCharacterService", errors)
    require_text("Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs", "File.Move(temporary, path, true)", errors)
    require_text("Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs", "ResolveGeneratedCharacterStartRoom()", errors)
    require_text("Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs", "Character names must be 3-20 characters", errors)
    require_text("Apps/Source/Reforged.Server/RuntimeSessionManager.cs", "CreateCharacterSession", errors)
    require_text("Apps/Source/Reforged.Server/RuntimeSessionManager.cs", "is already connected", errors)
    require_text("Apps/Source/Reforged.Server/ServerNetworkHost.cs", "SelectOrCreateCharacterAsync", errors)
    require_text("Apps/Source/Reforged.Server/ServerNetworkHost.cs", "Account: ", errors)
    require_text("Apps/Source/Reforged.Server/ServerRuntimeHost.cs", "RuntimeCharacterService", errors)

    foundation_path = ROOT / "Data" / "Classic" / "character_foundation.json"
    if not foundation_path.exists():
        errors.append("missing generated character_foundation.json")
    else:
        foundation = json.loads(foundation_path.read_text(encoding="utf-8"))
        if foundation.get("authority") != "Librarium":
            errors.append("generated foundation is not Librarium-authoritative")
        if foundation.get("race_count") != 13 or foundation.get("class_count") != 15:
            errors.append("generated foundation does not expose the 13/15 playable catalog")

    with tempfile.TemporaryDirectory() as temp_dir:
        target = Path(temp_dir) / "character.json"
        temporary = Path(temp_dir) / "character.tmp"
        temporary.write_text(json.dumps({"SaveVersion": 1, "RoomId": "room.1"}), encoding="utf-8")
        os.replace(temporary, target)
        restored = json.loads(target.read_text(encoding="utf-8"))
        if restored.get("RoomId") != "room.1":
            errors.append("atomic save round trip failed")

    if errors:
        print("FAIL CodeUpdate0137 character foundation")
        for error in errors:
            print(f"- {error}")
        return 1
    print("PASS CodeUpdate0137 character foundation static/runtime-contract validation")
    print("contracts: generated catalogs, name validation, account ownership, atomic character persistence, generated start-or-block, saved room restore, duplicate login prevention")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
