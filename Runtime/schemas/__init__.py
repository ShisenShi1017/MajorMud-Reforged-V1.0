"""Runtime schema dataclasses for MajorMUD Reforged Runtime-0002."""
from .canonical import (
    SchemaIssue,
    RoomExitSchema,
    RoomSchema,
    ItemSchema,
    MonsterAttackSchema,
    MonsterSchema,
    SpellSchema,
    ShopInventorySchema,
    ShopSchema,
    LootEntrySchema,
    LootTableSchema,
    SchemaBundle,
)

__all__ = [
    "SchemaIssue", "RoomExitSchema", "RoomSchema", "ItemSchema",
    "MonsterAttackSchema", "MonsterSchema", "SpellSchema", "ShopInventorySchema",
    "ShopSchema", "LootEntrySchema", "LootTableSchema", "SchemaBundle",
]
