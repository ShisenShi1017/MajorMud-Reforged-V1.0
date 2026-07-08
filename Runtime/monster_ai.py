from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from Runtime.data_runtime import DataRuntime


@dataclass(frozen=True)
class MonsterAITraceStep:
    code: str
    detail: str
    data: Dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class MonsterProfile:
    monster_id: str
    legacy_number: int
    name: str
    level: int
    group_id: int
    monster_type: int
    follow_score: int
    undead: bool
    charm_level: int
    charm_resistance: int
    game_limit: int
    behavior: Dict[str, Any]


@dataclass(frozen=True)
class MonsterDecision:
    action: str
    status: str
    reason: str
    trace: List[MonsterAITraceStep]


class MonsterAIRuntime:
    """Monster AI runtime foundation.

    Runtime-0008 converts canonical monster behavior fields into deterministic AI
    profiles and exposes conservative decision scaffolds for aggro, follow, assist,
    and pack behavior. Exact classic probabilities, target switching, adjacent-room
    pursuit, and combat turn selection remain UNKNOWN until verified.
    """

    def __init__(self, data_runtime: DataRuntime, mode: str = "classic_unverified") -> None:
        self.data_runtime = data_runtime
        self.mode = mode
        self.monsters_by_id: Dict[str, Dict[str, Any]] = {}
        self.monsters_by_legacy: Dict[int, Dict[str, Any]] = {}
        self.profiles_by_id: Dict[str, MonsterProfile] = {}
        self.profiles_by_legacy: Dict[int, MonsterProfile] = {}
        self._build_indexes()

    @staticmethod
    def _as_int(value: Any, default: int = 0) -> int:
        if isinstance(value, bool):
            return int(value)
        if isinstance(value, int):
            return value
        if isinstance(value, str):
            try:
                return int(value.strip())
            except ValueError:
                return default
        return default

    @staticmethod
    def _as_bool(value: Any) -> bool:
        if isinstance(value, bool):
            return value
        if isinstance(value, int):
            return value != 0
        if isinstance(value, str):
            return value.strip().lower() in {"1", "true", "yes", "y"}
        return False

    def _build_indexes(self) -> None:
        for monster in self.data_runtime.get_records("monsters"):
            monster_id = monster.get("id")
            legacy = self._as_int(monster.get("legacyNumber") or monster.get("sourceId"), 0)
            if isinstance(monster_id, str):
                self.monsters_by_id[monster_id] = monster
            if legacy > 0:
                self.monsters_by_legacy[legacy] = monster
            profile = self._profile_for_monster(monster)
            if profile is not None:
                self.profiles_by_id[profile.monster_id] = profile
                if profile.legacy_number > 0:
                    self.profiles_by_legacy[profile.legacy_number] = profile

    def _profile_for_monster(self, monster: Dict[str, Any]) -> Optional[MonsterProfile]:
        monster_id = monster.get("id")
        if not isinstance(monster_id, str):
            return None
        behavior = monster.get("behavior") if isinstance(monster.get("behavior"), dict) else {}
        return MonsterProfile(
            monster_id=monster_id,
            legacy_number=self._as_int(monster.get("legacyNumber") or monster.get("sourceId"), 0),
            name=str(monster.get("name", monster_id)),
            level=self._as_int(monster.get("level"), 0),
            group_id=self._as_int(monster.get("groupId"), 0),
            monster_type=self._as_int(monster.get("monsterType"), 0),
            follow_score=self._as_int(behavior.get("follow"), 0),
            undead=self._as_bool(behavior.get("undead")),
            charm_level=self._as_int(behavior.get("charmLevel"), 0),
            charm_resistance=self._as_int(behavior.get("charmResistance"), 0),
            game_limit=self._as_int(behavior.get("gameLimit"), 0),
            behavior=dict(behavior),
        )

    def get_profile(self, monster_ref: Any) -> Optional[MonsterProfile]:
        if isinstance(monster_ref, int):
            return self.profiles_by_legacy.get(monster_ref)
        if isinstance(monster_ref, str):
            return self.profiles_by_id.get(monster_ref)
        return None

    def classify_follow(self, monster_ref: Any) -> MonsterDecision:
        profile = self.get_profile(monster_ref)
        trace: List[MonsterAITraceStep] = []
        if profile is None:
            return MonsterDecision("NO_DECISION", "UNKNOWN", "Monster reference not found.", trace)
        trace.append(MonsterAITraceStep("FOLLOW_FIELD_READ", "Canonical behavior.follow field read.", {"follow": profile.follow_score}))
        if profile.follow_score <= 0:
            return MonsterDecision("DO_NOT_FOLLOW", "PASS", "Follow score is zero or negative.", trace)
        trace.append(MonsterAITraceStep("UNKNOWN_FOLLOW_PROBABILITY", "Exact classic follow roll/timing is not promoted in Runtime-0008.", {"follow": profile.follow_score}))
        return MonsterDecision("FOLLOW_CANDIDATE", "UNKNOWN", "Monster has follow data, but exact classic follow behavior remains unverified.", trace)

    def classify_aggro(self, monster_ref: Any) -> MonsterDecision:
        profile = self.get_profile(monster_ref)
        trace: List[MonsterAITraceStep] = []
        if profile is None:
            return MonsterDecision("NO_DECISION", "UNKNOWN", "Monster reference not found.", trace)
        trace.append(MonsterAITraceStep("BEHAVIOR_PROFILE_READ", "Canonical monster behavior profile read.", {"monster_type": profile.monster_type, "group_id": profile.group_id, "level": profile.level}))
        trace.append(MonsterAITraceStep("UNKNOWN_AGGRO_RULE", "Aggro trigger/probability is not proven from current evidence.", {}))
        return MonsterDecision("AGGRO_UNRESOLVED", "UNKNOWN", "Aggro semantics are preserved as an AI decision surface but not claimed classic-verified.", trace)

    def classify_assist(self, attacker_ref: Any, defender_ref: Any) -> MonsterDecision:
        attacker = self.get_profile(attacker_ref)
        defender = self.get_profile(defender_ref)
        trace: List[MonsterAITraceStep] = []
        if attacker is None or defender is None:
            return MonsterDecision("NO_DECISION", "UNKNOWN", "One or both monster references were not found.", trace)
        same_group = attacker.group_id > 0 and attacker.group_id == defender.group_id
        trace.append(MonsterAITraceStep("GROUP_COMPARE", "Compared canonical monster groupId values.", {"attacker_group": attacker.group_id, "defender_group": defender.group_id, "same_group": same_group}))
        if same_group:
            trace.append(MonsterAITraceStep("UNKNOWN_ASSIST_RULE", "Same group may indicate assist/pack relationship, but exact classic behavior is not verified.", {}))
            return MonsterDecision("ASSIST_CANDIDATE", "UNKNOWN", "Same-group monsters are preserved as assist candidates only.", trace)
        return MonsterDecision("NO_ASSIST_BY_GROUP", "PASS", "Group IDs do not match, so this scaffold does not mark an assist candidate.", trace)

    def classify_pack(self, monster_ref: Any) -> MonsterDecision:
        profile = self.get_profile(monster_ref)
        trace: List[MonsterAITraceStep] = []
        if profile is None:
            return MonsterDecision("NO_DECISION", "UNKNOWN", "Monster reference not found.", trace)
        group_size = sum(1 for p in self.profiles_by_id.values() if p.group_id == profile.group_id and p.group_id > 0)
        trace.append(MonsterAITraceStep("GROUP_SIZE_INDEXED", "Counted monsters sharing canonical groupId.", {"group_id": profile.group_id, "group_size": group_size}))
        if group_size > 1:
            return MonsterDecision("PACK_CANDIDATE", "UNKNOWN", "Group membership suggests a pack/assist surface, but exact behavior remains unverified.", trace)
        return MonsterDecision("SOLO_GROUP", "PASS", "No same-group pack candidates found in canonical monster data.", trace)

    def summary(self) -> Dict[str, Any]:
        follow_candidates = sum(1 for p in self.profiles_by_id.values() if p.follow_score > 0)
        no_follow = sum(1 for p in self.profiles_by_id.values() if p.follow_score <= 0)
        undead = sum(1 for p in self.profiles_by_id.values() if p.undead)
        group_ids = {p.group_id for p in self.profiles_by_id.values() if p.group_id > 0}
        grouped = sum(1 for p in self.profiles_by_id.values() if p.group_id > 0)
        limited = sum(1 for p in self.profiles_by_id.values() if p.game_limit > 0)
        return {
            "monsters_indexed": len(self.monsters_by_id),
            "legacy_indexed": len(self.monsters_by_legacy),
            "ai_profiles": len(self.profiles_by_id),
            "follow_candidates": follow_candidates,
            "no_follow_profiles": no_follow,
            "undead_profiles": undead,
            "group_ids": len(group_ids),
            "grouped_profiles": grouped,
            "game_limited_profiles": limited,
            "mode": self.mode,
        }
