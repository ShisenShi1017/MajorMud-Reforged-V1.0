"""Canonical /Data schema adapter loader for Runtime-0002."""
from __future__ import annotations
import json
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple

from Runtime.schemas import (
    SchemaIssue, RoomExitSchema, RoomSchema, ItemSchema,
    MonsterAttackSchema, MonsterSchema, SpellSchema, ShopInventorySchema,
    ShopSchema, LootEntrySchema, LootTableSchema, SchemaBundle,
)

def _load_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default
    with path.open('r', encoding='utf-8') as f:
        return json.load(f)

def _runtime_id(prefix: str, legacy: Optional[int], name: Optional[str] = None) -> str:
    if legacy is None:
        safe = ''.join(ch.lower() if ch.isalnum() else '_' for ch in (name or 'unknown')).strip('_')
        return f'{prefix}_{safe or "unknown"}'
    safe = ''.join(ch.lower() if ch.isalnum() else '_' for ch in (name or '')).strip('_')
    return f'{prefix}_{legacy}_{safe}' if safe else f'{prefix}_{legacy}'

def _legacy(record: Dict[str, Any]) -> Optional[int]:
    for key in ('legacyNumber', 'legacy_number', 'sourceId', 'roomNumber'):
        val = record.get(key)
        if isinstance(val, int):
            return val
    return None

def _as_list_spells(spells_data: Any) -> List[Dict[str, Any]]:
    if isinstance(spells_data, list):
        return [x for x in spells_data if isinstance(x, dict)]
    if isinstance(spells_data, dict):
        val = spells_data.get('spells', [])
        if isinstance(val, list):
            return [x for x in val if isinstance(x, dict)]
        if isinstance(val, dict):
            return [x for x in val.values() if isinstance(x, dict)]
    return []

def adapt_items(raw_items: Iterable[Dict[str, Any]]) -> Tuple[Dict[str, ItemSchema], Dict[int, str], List[SchemaIssue]]:
    items: Dict[str, ItemSchema] = {}
    by_legacy: Dict[int, str] = {}
    issues: List[SchemaIssue] = []
    for rec in raw_items:
        legacy = _legacy(rec)
        rid = rec.get('id') or _runtime_id('item', legacy, rec.get('name'))
        item = ItemSchema(
            runtime_id=rid,
            legacy_number=legacy,
            name=rec.get('name') or '(unnamed item)',
            item_type=rec.get('type') or 'unknown',
            combat=rec.get('combat') or {},
            defense=rec.get('defense') or {},
            economy=rec.get('economy') or {},
            restrictions=rec.get('restrictions') or {},
            properties=rec.get('properties') or {},
            uses=rec.get('uses') or {},
            raw=rec,
        )
        items[rid] = item
        if legacy is not None:
            by_legacy[legacy] = rid
        if not rec.get('name'):
            issues.append(SchemaIssue('warning','missing_name','item',rid,'Item has no name.'))
    return items, by_legacy, issues

def adapt_rooms(raw_rooms: Iterable[Dict[str, Any]]) -> Tuple[Dict[str, RoomSchema], Dict[int, str], List[SchemaIssue]]:
    rooms: Dict[str, RoomSchema] = {}
    by_number: Dict[int, str] = {}
    issues: List[SchemaIssue] = []
    raw_list = list(raw_rooms)
    for rec in raw_list:
        num = rec.get('roomNumber') if isinstance(rec.get('roomNumber'), int) else _legacy(rec)
        rid = rec.get('id') or _runtime_id('room', num, rec.get('name'))
        rooms[rid] = RoomSchema(rid, num, rec.get('mapId'), rec.get('name') or '(unnamed room)', rec.get('description') or '', [], rec)
        if isinstance(num, int):
            by_number[num] = rid
    for rec in raw_list:
        num = rec.get('roomNumber') if isinstance(rec.get('roomNumber'), int) else _legacy(rec)
        rid = rec.get('id') or _runtime_id('room', num, rec.get('name'))
        exits = []
        for ex in rec.get('exits') or []:
            target_num = ex.get('to_room') if isinstance(ex, dict) else None
            target_id = by_number.get(target_num) if isinstance(target_num, int) else None
            exits.append(RoomExitSchema(ex.get('dir') or ex.get('direction') or '?', target_num, target_id, ex.get('exit_type'), ex))
            if isinstance(target_num, int) and target_id is None:
                issues.append(SchemaIssue('warning','unresolved_exit','room',rid,f'Exit target room number {target_num} not found in rooms.json.'))
        rooms[rid].exits = exits
    return rooms, by_number, issues

def adapt_monsters(raw_monsters: Iterable[Dict[str, Any]]) -> Tuple[Dict[str, MonsterSchema], Dict[int, str], List[SchemaIssue]]:
    monsters: Dict[str, MonsterSchema] = {}
    by_legacy: Dict[int, str] = {}
    issues: List[SchemaIssue] = []
    for rec in raw_monsters:
        legacy = _legacy(rec)
        rid = rec.get('id') or _runtime_id('monster', legacy, rec.get('name'))
        attacks = []
        for atk in rec.get('attacks') or []:
            attacks.append(MonsterAttackSchema(atk.get('type'), int(atk.get('min_hit') or 0), int(atk.get('max_hit') or 0), int(atk.get('percent') or 0), int(atk.get('accuracy') or 0), atk.get('hit_spell'), int(atk.get('energy_cost') or 0), atk))
        monsters[rid] = MonsterSchema(rid, legacy, rec.get('name') or '(unnamed monster)', int(rec.get('level') or 0), rec.get('stats') or {}, rec.get('experience') or {}, rec.get('behavior') or {}, attacks, rec)
        if legacy is not None:
            by_legacy[legacy] = rid
        if not attacks:
            issues.append(SchemaIssue('info','no_attacks','monster',rid,'Monster has no attack records.'))
    return monsters, by_legacy, issues

def adapt_spells(raw_spells_data: Any) -> Tuple[Dict[str, SpellSchema], Dict[int, str], List[SchemaIssue]]:
    spells: Dict[str, SpellSchema] = {}
    by_legacy: Dict[int, str] = {}
    issues: List[SchemaIssue] = []
    for rec in _as_list_spells(raw_spells_data):
        legacy = _legacy(rec)
        rid = rec.get('id') or _runtime_id('spell', legacy, rec.get('name'))
        spell = SpellSchema(rid, legacy, rec.get('name') or '(unnamed spell)', rec.get('level'), rec.get('type') or rec.get('spellType'), rec.get('cost') or rec.get('manaCost'), rec)
        spells[rid] = spell
        if legacy is not None:
            by_legacy[legacy] = rid
    if not spells:
        issues.append(SchemaIssue('warning','empty_spells','spell','spells.json','No spell records could be adapted from spells.json.'))
    return spells, by_legacy, issues

def adapt_shops(raw_shops: Iterable[Dict[str, Any]], item_by_legacy: Dict[int, str]) -> Tuple[Dict[str, ShopSchema], List[SchemaIssue]]:
    shops: Dict[str, ShopSchema] = {}
    issues: List[SchemaIssue] = []
    for rec in raw_shops:
        legacy = _legacy(rec)
        rid = rec.get('id') or _runtime_id('shop', legacy, rec.get('name'))
        inventory = []
        for inv in rec.get('inventory') or []:
            item_num = inv.get('item_id') if isinstance(inv.get('item_id'), int) else None
            item_id = item_by_legacy.get(item_num) if item_num is not None else None
            inventory.append(ShopInventorySchema(item_num, item_id, inv.get('max_qty'), inv.get('regen_qty'), inv.get('normal_qty'), inv.get('regen_time'), inv.get('regen_percent'), inv))
            if item_num is not None and item_id is None:
                issues.append(SchemaIssue('warning','unresolved_shop_item','shop',rid,f'Shop inventory references missing item legacy number {item_num}.'))
        shops[rid] = ShopSchema(rid, legacy, rec.get('name') or '(unnamed shop)', rec.get('shopType'), rec.get('markupPercent'), inventory, rec)
    return shops, issues

def adapt_loot(raw_loot: Iterable[Dict[str, Any]], item_by_legacy: Dict[int, str], monster_by_legacy: Dict[int, str]) -> Tuple[Dict[str, LootTableSchema], List[SchemaIssue]]:
    loot_tables: Dict[str, LootTableSchema] = {}
    issues: List[SchemaIssue] = []
    for rec in raw_loot:
        rid = rec.get('id') or _runtime_id('loot', rec.get('monsterLegacyNumber'), rec.get('monsterId'))
        monster_num = rec.get('monsterLegacyNumber') if isinstance(rec.get('monsterLegacyNumber'), int) else None
        monster_id = rec.get('monsterId') or (monster_by_legacy.get(monster_num) if monster_num is not None else None)
        entries = []
        for ent in rec.get('entries') or []:
            item_num = ent.get('item_id') if isinstance(ent.get('item_id'), int) else None
            item_id = item_by_legacy.get(item_num) if item_num is not None else None
            entries.append(LootEntrySchema(ent.get('name'), item_num, item_id, ent.get('drop_percent'), ent))
            if item_num is not None and item_id is None:
                issues.append(SchemaIssue('warning','unresolved_loot_item','loot_table',rid,f'Loot entry references missing item legacy number {item_num}.'))
        loot_tables[rid] = LootTableSchema(rid, monster_num, monster_id, entries, rec)
    return loot_tables, issues

def load_schema_bundle(data_dir: str | Path = 'Data') -> SchemaBundle:
    data_dir = Path(data_dir)
    issues: List[SchemaIssue] = []
    raw_rooms = _load_json(data_dir / 'rooms.json', [])
    raw_items = _load_json(data_dir / 'items.json', [])
    raw_monsters = _load_json(data_dir / 'monsters.json', [])
    raw_spells = _load_json(data_dir / 'spells.json', {})
    raw_shops = _load_json(data_dir / 'shops.json', [])
    raw_loot = _load_json(data_dir / 'loot_tables.json', [])

    rooms, room_by_number, room_issues = adapt_rooms(raw_rooms if isinstance(raw_rooms, list) else [])
    items, item_by_legacy, item_issues = adapt_items(raw_items if isinstance(raw_items, list) else [])
    monsters, monster_by_legacy, monster_issues = adapt_monsters(raw_monsters if isinstance(raw_monsters, list) else [])
    spells, spell_by_legacy, spell_issues = adapt_spells(raw_spells)
    shops, shop_issues = adapt_shops(raw_shops if isinstance(raw_shops, list) else [], item_by_legacy)
    loot_tables, loot_issues = adapt_loot(raw_loot if isinstance(raw_loot, list) else [], item_by_legacy, monster_by_legacy)
    issues.extend(room_issues + item_issues + monster_issues + spell_issues + shop_issues + loot_issues)

    return SchemaBundle(rooms, items, monsters, spells, shops, loot_tables, issues)
