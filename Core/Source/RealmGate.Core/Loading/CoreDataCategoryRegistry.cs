namespace RealmGate.Core.Loading;

/// <summary>
/// Registry for every current canonical Data/ category used by Milestone 1 loaders.
/// </summary>
public static class CoreDataCategoryRegistry
{
    public static IReadOnlyList<DataCategoryDescriptor> Current { get; } = new[]
    {
        new DataCategoryDescriptor("classes", "classes.json"),
        new DataCategoryDescriptor("items", "items.json"),
        new DataCategoryDescriptor("loot_tables", "loot_tables.json"),
        new DataCategoryDescriptor("monsters", "monsters.json"),
        new DataCategoryDescriptor("quests", "quests.json"),
        new DataCategoryDescriptor("races", "races.json"),
        new DataCategoryDescriptor("rooms", "rooms.json"),
        new DataCategoryDescriptor("shops", "shops.json"),
        new DataCategoryDescriptor("trainers", "trainers.json"),
        new DataCategoryDescriptor("messages", "messages.json", "messages"),
        new DataCategoryDescriptor("skills", "skills.json", "skills"),
        new DataCategoryDescriptor("spells", "spells.json", "spells"),
        new DataCategoryDescriptor("world_rooms", "world.json", "rooms")
    };
}
