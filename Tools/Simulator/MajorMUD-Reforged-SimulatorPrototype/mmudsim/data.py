from __future__ import annotations

from collections import defaultdict
from pathlib import Path
import json

from .util import norm, read_csv


class EvidenceMissingError(FileNotFoundError):
    pass


class EvidenceStore:
    """Loads the RuntimePrep CSV evidence used by the simulator.

    This is an expected-model loader. It does not create runtime evidence and it does
    not claim Worldgroup equivalence.
    """

    def __init__(self, root: Path | None = None):
        self.root = Path(root) if root else Path(__file__).resolve().parents[1]
        self.evidence = self.root / "Evidence"
        self.config_path = self.root / "config" / "simulator_preset.json"
        self.config = json.loads(self.config_path.read_text(encoding="utf-8")) if self.config_path.exists() else {}
        self._path_cache: dict[str, Path] = {}

    def find_file(self, pattern: str) -> Path:
        if pattern not in self._path_cache:
            matches = sorted(self.evidence.rglob(pattern))
            if not matches:
                raise EvidenceMissingError(f"Missing evidence file matching {pattern}")
            self._path_cache[pattern] = matches[0]
        return self._path_cache[pattern]

    def rows(self, pattern: str) -> list[dict[str, str]]:
        return read_csv(self.find_file(pattern))


class SimData:
    def __init__(self, store: EvidenceStore | None = None):
        self.store = store or EvidenceStore()
        self.config = self.store.config

        self.room_rows = self.store.rows("RoomPopulationMatrix_RuntimePrep0002.csv")
        self.nav_rows = self.store.rows("NavigationEdgeMatrix_RuntimePrep0003.csv")
        self.shop_rows = self.store.rows("ShopInventoryMatrix_RuntimePrep0004.csv")
        self.shop_catalog = self.store.rows("ShopCatalog_RuntimePrep0004.csv")
        self.monster_drops = self.store.rows("MonsterDropMatrix_RuntimePrep0005.csv")
        self.currency = self.store.rows("MonsterCurrencyDropMatrix_RuntimePrep0005.csv")
        self.npc_candidates = self.store.rows("DialogueScriptQuestCandidateMatrix_RuntimePrep0006.csv")
        self.monster_combat = self.store.rows("MonsterCombatExpectationMatrix_RuntimePrep0007.csv")
        self.spells = self.store.rows("SpellSystemExpectationMatrix_RuntimePrep0007.csv")
        self.skills = self.store.rows("SkillSystemExpectationMatrix_RuntimePrep0007.csv")
        self.classes = self.store.rows("ClassProgressionExpectationMatrix_RuntimePrep0007.csv")
        self.equipment = self.store.rows("EquipmentStatExpectationMatrix_RuntimePrep0007.csv")
        self.readiness = self.store.rows("FinalSimulatorReadinessCertification_RuntimePrep0009.csv")

        self.room_by_key = {norm(r.get("room_key")): r for r in self.room_rows if norm(r.get("room_key"))}
        self.rooms_by_map_num = {}
        for r in self.room_rows:
            k = (norm(r.get("map_id")).split(".")[0], norm(r.get("room_number")).split(".")[0])
            self.rooms_by_map_num[k] = r

        self.nav_from = defaultdict(list)
        self.nav_by_from_name = defaultdict(list)
        for r in self.nav_rows:
            code = norm(r.get("from_code")).upper()
            if code:
                self.nav_from[code].append(r)
            name = norm(r.get("from_name")).lower()
            if name:
                self.nav_by_from_name[name].append(r)

        self.shop_by_id = defaultdict(list)
        for r in self.shop_rows:
            self.shop_by_id[norm(r.get("shop_legacy_number")).split(".")[0]].append(r)
        self.shop_catalog_by_id = {norm(r.get("shop_legacy_number")).split(".")[0]: r for r in self.shop_catalog}

        self.combat_by_id = {norm(r.get("monster_id")): r for r in self.monster_combat if norm(r.get("monster_id"))}
        self.combat_by_name = {norm(r.get("name")).lower(): r for r in self.monster_combat if norm(r.get("name"))}
        self.drop_by_monster = defaultdict(list)
        for r in self.monster_drops:
            self.drop_by_monster[norm(r.get("monster_id"))].append(r)
        self.currency_by_monster = {norm(r.get("monster_id")): r for r in self.currency}

    def find_monster_id(self, ident: str) -> str:
        s = norm(ident)
        if s in self.combat_by_id:
            return s
        low = s.lower()
        if low in self.combat_by_name:
            return norm(self.combat_by_name[low].get("monster_id"))
        for mid, row in self.combat_by_id.items():
            if low in norm(row.get("name")).lower():
                return mid
        return s

    def resolve_room(self, key: str) -> dict[str, str] | None:
        s = norm(key)
        if s in self.room_by_key:
            return self.room_by_key[s]
        # Accept map:room or map-room number forms.
        if ":" in s:
            a, b = s.split(":", 1)
            return self.rooms_by_map_num.get((a, b))
        candidates = [r for r in self.room_rows if norm(r.get("room_number")).split(".")[0] == s or norm(r.get("room_name")).lower() == s.lower()]
        return candidates[0] if candidates else None

    def summary(self) -> dict[str, int | str]:
        return {
            "preset": self.config.get("preset", "unknown"),
            "runtime_boundary": self.config.get("runtime_boundary", "expected-model-only"),
            "rooms": len(self.room_rows),
            "navigation_edges": len(self.nav_rows),
            "shops": len(self.shop_catalog),
            "shop_inventory_rows": len(self.shop_rows),
            "monster_drop_rows": len(self.monster_drops),
            "npc_candidates": len(self.npc_candidates),
            "monster_combat_rows": len(self.monster_combat),
            "spells": len(self.spells),
            "skills": len(self.skills),
            "classes": len(self.classes),
            "equipment_rows": len(self.equipment),
        }
