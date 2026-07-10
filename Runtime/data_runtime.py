from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

CANONICAL_FILES = {
    "world": "world.json",
    "rooms": "rooms.json",
    "room_graph": "room_graph.json",
    "items": "items.json",
    "monsters": "npcs.json",
    "spells": "spells.json",
    "shops": "shops.json",
    "loot_tables": "loot_tables.json",
    "classes": "classes.json",
    "races": "races.json",
    "skills": "skills.json",
    "abilities": "skills.json",
    "derived_stats": "derived_stats.json",
    "progression": "progression.json",
    "leveling": "leveling.json",
    "messages": "messages.json",
    "message_text_catalogs": "message_text_catalogs.json",
    "quests": "quests.json",
    "trainers": "trainers.json",
}

@dataclass
class LoadIssue:
    severity: str
    code: str
    message: str
    source: Optional[str] = None

@dataclass
class DataRuntime:
    data_dir: Path
    tables: Dict[str, Any] = field(default_factory=dict)
    issues: List[LoadIssue] = field(default_factory=list)

    @classmethod
    def from_repo_root(cls, repo_root: str | Path = ".") -> "DataRuntime":
        return cls(Path(repo_root) / "Data" / "Classic")

    def load(self) -> "DataRuntime":
        self.tables.clear()
        self.issues.clear()
        if not self.data_dir.exists():
            self.issues.append(LoadIssue("FAIL", "DATA_DIR_MISSING", f"Missing Data directory: {self.data_dir}"))
            return self
        for table_name, rel_path in CANONICAL_FILES.items():
            path = self.data_dir / rel_path
            if not path.exists():
                self.issues.append(LoadIssue("WARN", "CANONICAL_FILE_MISSING", f"Missing canonical file {rel_path}", rel_path))
                continue
            try:
                self.tables[table_name] = json.loads(path.read_text(encoding="utf-8-sig"))
            except Exception as exc:
                self.issues.append(LoadIssue("FAIL", "JSON_LOAD_FAILED", f"Could not load {rel_path}: {exc}", rel_path))
        return self

    def count(self, table_name: str) -> int:
        data = self.tables.get(table_name)
        if data is None:
            return 0
        if isinstance(data, list):
            return len(data)
        if isinstance(data, dict):
            for key in ("records", "items", "rooms", "monsters", "spells", "shops", "data"):
                value = data.get(key)
                if isinstance(value, list):
                    return len(value)
            return len(data)
        return 1

    def summary(self) -> Dict[str, Any]:
        return {
            "data_dir": str(self.data_dir),
            "loaded_tables": sorted(self.tables.keys()),
            "counts": {name: self.count(name) for name in sorted(self.tables.keys())},
            "issues": [issue.__dict__ for issue in self.issues],
        }

    def get_records(self, table_name: str) -> List[Dict[str, Any]]:
        data = self.tables.get(table_name)
        if isinstance(data, list):
            return [x for x in data if isinstance(x, dict)]
        if isinstance(data, dict):
            for key in ("records", "items", "rooms", "monsters", "spells", "shops", "data"):
                value = data.get(key)
                if isinstance(value, list):
                    return [x for x in value if isinstance(x, dict)]
        return []

    def find_id_field(self, records: List[Dict[str, Any]]) -> Optional[str]:
        candidates = ("id", "ID", "number", "Number", "room", "Room", "item", "Item", "monster", "Monster", "spell", "Spell")
        for c in candidates:
            if any(c in r for r in records[:50]):
                return c
        return None
