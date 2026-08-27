namespace Reforged.App.Services;

public enum CustomWorldSimpleFieldKind
{
    Text,
    Multiline,
    Integer,
    Boolean,
    Choice,
    Lines
}

public sealed record CustomWorldSimpleOption(string Value, string Label);
public sealed record CustomWorldSimpleFamilyOption(string Value, string Label);

public sealed record CustomWorldSimpleFieldDefinition(
    string Path,
    string Label,
    CustomWorldSimpleFieldKind Kind,
    bool Required = false,
    string DefaultValue = "",
    string HelpText = "",
    IReadOnlyList<CustomWorldSimpleOption>? Options = null);

public sealed record CustomWorldSimpleFormDefinition(
    string Family,
    string SingularName,
    string IdPrefix,
    string HelpText,
    IReadOnlyList<CustomWorldSimpleFieldDefinition> Fields);

public static class CustomWorldSimpleFormCatalog
{
    private static CustomWorldSimpleOption O(string value, string label) => new(value, label);
    private static CustomWorldSimpleFieldDefinition T(string path, string label, bool required = false, string defaultValue = "", string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.Text, required, defaultValue, help);
    private static CustomWorldSimpleFieldDefinition M(string path, string label, bool required = false, string defaultValue = "", string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.Multiline, required, defaultValue, help);
    private static CustomWorldSimpleFieldDefinition I(string path, string label, bool required = false, string defaultValue = "0", string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.Integer, required, defaultValue, help);
    private static CustomWorldSimpleFieldDefinition B(string path, string label, bool defaultValue = false, string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.Boolean, false, defaultValue ? "true" : "false", help);
    private static CustomWorldSimpleFieldDefinition C(string path, string label, IReadOnlyList<CustomWorldSimpleOption> options, bool required = false, string defaultValue = "", string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.Choice, required, defaultValue, help, options);
    private static CustomWorldSimpleFieldDefinition L(string path, string label, bool required = false, string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.Lines, required, "", help);

    private static readonly IReadOnlyDictionary<string, CustomWorldSimpleFormDefinition> Forms =
        new Dictionary<string, CustomWorldSimpleFormDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["rooms"] = new("rooms", "Room", "room.custom", "Create or edit a room. The room ID is generated from map + room number when left blank.",
            [
                I("map_number", "Map number", true, "1", "Which map this room belongs to."),
                I("room_number", "Room number", true, "1", "Unique room number on that map."),
                T("name", "Room name", true, "New Room"),
                M("description", "Room description", false, "", "What the player sees when looking at the room."),
                I("environment.light_level", "Light level", false, "0"),
                I("environment.room_spell_id", "Room spell ID", false, "0", "0 means no ambient room spell."),
                I("environment.respawn_minutes", "Respawn minutes", false, "0"),
                I("environment.max_area_cap", "Maximum area population", false, "0")
            ]),

            ["exits"] = new("exits", "Exit", "exit.custom", "Connect two rooms. Pick a direction and destination; the editor builds the room references for you.",
            [
                I("from_map_number", "From map", true, "1"),
                I("from_room_number", "From room", true, "1"),
                C("direction", "Direction", [O("N","North"),O("NE","Northeast"),O("E","East"),O("SE","Southeast"),O("S","South"),O("SW","Southwest"),O("W","West"),O("NW","Northwest"),O("U","Up"),O("D","Down")], true, "N"),
                I("to_map_number", "To map", true, "1"),
                I("to_room_number", "To room", true, "1"),
                C("gate_kind", "Exit type", [O("open","Open"),O("door","Door"),O("key","Keyed door"),O("hidden","Hidden"),O("required_item","Requires item"),O("text_command","Command activated"),O("toll","Toll"),O("level_range","Level restricted"),O("spell_trap","Spell trap"),O("damage_trap","Damage trap")], true, "open"),
                T("qualifier", "Door / exit label", false, "", "Example: iron gate, trapdoor, archway."),
                T("prerequisites.required_item_id", "Required item ID", false, "", "Leave blank if no item is required."),
                I("prerequisites.toll_amount", "Toll amount", false, "0"),
                I("prerequisites.minimum_level", "Minimum level", false, "0"),
                I("prerequisites.maximum_level", "Maximum level", false, "0"),
                I("effects.damage", "Trap damage", false, "0"),
                I("effects.spell_id", "Trap spell ID", false, "0"),
                L("commands", "Accepted commands", false, "One command per line, for command-activated exits.")
            ]),

            ["npcs"] = new("npcs", "NPC / Monster", "npc.custom", "Create a monster or NPC with normal gameplay stats. Advanced abilities can be added later without touching JSON.",
            [
                T("name", "Name", true, "New NPC"),
                I("legacy_id", "Legacy number", false, "0", "Optional. Use 0 for a Reforged-only NPC."),
                I("combat.level", "Level", true, "1"),
                I("combat.hp", "Hit points", true, "20"),
                I("combat.energy", "Energy", false, "1000"),
                I("combat.armor_class", "Armor class", false, "0"),
                I("combat.damage_resistance", "Damage resistance", false, "0"),
                I("combat.magic_resistance", "Magic resistance", false, "0"),
                I("combat.attack_count", "Number of attacks", false, "1"),
                C("behavior.alignment", "Alignment", [O("lawful_good","Lawful Good"),O("neutral_good","Neutral Good"),O("chaotic_good","Chaotic Good"),O("lawful_neutral","Lawful Neutral"),O("neutral","Neutral"),O("chaotic_neutral","Chaotic Neutral"),O("lawful_evil","Lawful Evil"),O("neutral_evil","Neutral Evil"),O("chaotic_evil","Chaotic Evil")], false, "neutral"),
                B("behavior.undead", "Undead", false),
                I("behavior.regen_time", "Regen time", false, "0"),
                I("behavior.respawn_hours", "Respawn hours", false, "0"),
                I("rewards.xp_base", "Experience reward", false, "0"),
                I("rewards.gold", "Gold reward", false, "0"),
                I("rewards.silver", "Silver reward", false, "0"),
                I("rewards.copper", "Copper reward", false, "0")
            ]),

            ["spawns"] = new("spawns", "Spawn", "spawn.custom", "Place an NPC in a room and control how it respawns.",
            [
                I("map_id", "Map number", true, "1"),
                I("room_number", "Room number", true, "1"),
                T("npc_name", "NPC name", true, "New NPC", "Use the exact NPC name."),
                I("npc_legacy_id", "NPC legacy number", false, "0"),
                I("spawn_model.respawn_minutes", "Respawn minutes", false, "5"),
                I("spawn_model.max_area_cap", "Maximum alive in area", false, "1"),
                I("spawn_model.linked_spawn_room", "Linked spawn room", false, "0")
            ]),

            ["items"] = new("items", "Item", "item.custom", "Create weapons, armor, containers, consumables, or ordinary items. Runtime fields are generated automatically.",
            [
                T("name", "Item name", true, "New Item"),
                I("legacy_id", "Legacy number", false, "0", "Optional. Use 0 for a Reforged-only item."),
                C("classification.type", "Item type", [O("misc","Miscellaneous"),O("weapon","Weapon"),O("armor","Armor"),O("container","Container"),O("consumable","Consumable"),O("food","Food"),O("drink","Drink")], true, "misc"),
                B("flags.gettable", "Player can pick it up", true),
                I("economy.weight", "Weight", false, "0"),
                I("public_reference_0139.fields.cost", "Value / cost", false, "0"),
                C("equipment.worn_on", "Equipment slot", [O("0","Not wearable"),O("2","Head"),O("3","Face"),O("4","Neck"),O("5","Back"),O("6","Torso"),O("7","Arms"),O("8","Hands"),O("9","Waist"),O("10","Legs"),O("11","Feet"),O("12","Wield"),O("13","Off hand"),O("14","Finger"),O("15","Wrist"),O("16","Shield"),O("19","Light")], false, "0"),
                I("equipment.str_requirement", "Minimum strength", false, "0"),
                I("public_reference_0139.fields.min_hit", "Minimum damage", false, "0"),
                I("public_reference_0139.fields.max_hit", "Maximum damage", false, "0"),
                I("combat.armor_class_bonus", "Armor class bonus", false, "0"),
                I("combat.damage_resistance_bonus", "Damage resistance bonus", false, "0"),
                I("combat.accuracy", "Accuracy", false, "0"),
                I("combat.speed", "Speed", false, "0"),
                I("public_reference_0139.fields.max_uses", "Maximum uses", false, "0"),
                I("public_reference_0139.fields.game_limit", "Game limit", false, "0")
            ]),

            ["spells"] = new("spells", "Spell", "spell.custom", "Create a spell by entering its level, costs, target type, and effect range.",
            [
                T("name", "Spell name", true, "New Spell"),
                T("short_name", "Short name", false, ""),
                I("legacy_id", "Legacy number", false, "0"),
                I("classification.spell_level", "Spell level", true, "1"),
                T("classification.spell_type", "Spell type", false, "0"),
                T("classification.target_type", "Target type", false, "0"),
                T("classification.attack_type", "Attack type", false, "0"),
                I("costs.mana_cost", "Mana cost", false, "0"),
                I("costs.energy_cost", "Energy cost", false, "1000"),
                I("effect.min_effect", "Minimum effect", false, "0"),
                I("effect.max_effect", "Maximum effect", false, "0"),
                I("effect.duration", "Duration", false, "0")
            ]),

            ["abilities"] = new("abilities", "Ability", "ability.custom", "Create an ability identity and description. It can then be referenced by other Custom1 content.",
            [
                T("name", "Ability name", true, "New Ability"),
                I("legacy_id", "Legacy number", false, "0"),
                M("public_reference_0139.fields.description", "Description", false, ""),
                T("category", "Category", false, "custom")
            ]),

            ["classes"] = new("classes", "Class", "class.custom", "Create a playable or NPC class identity for Custom1.",
            [
                T("name", "Class name", true, "New Class"),
                M("description", "Description", false, ""),
                T("classification", "Class category", false, "custom_playable_class")
            ]),

            ["races"] = new("races", "Race", "race.custom", "Create a race identity for Custom1.",
            [
                T("name", "Race name", true, "New Race"),
                M("description", "Description", false, ""),
                T("classification", "Race category", false, "custom_playable_race")
            ]),

            ["shops"] = new("shops", "Shop / Service", "shop.custom", "Create a merchant, trainer, spell shop, or other room service. Enter item IDs one per line to stock a merchant.",
            [
                T("name", "Shop name", true, "New Shop"),
                I("legacy_id", "Legacy number", false, "0"),
                T("room_ref", "Room ID", true, "room.map1.1", "Example: room.map1.250"),
                C("service_kind", "Service type", [O("shop","General shop"),O("weapon_shop","Weapon shop"),O("armor_shop","Armor shop"),O("spell_shop","Spell shop"),O("trainer","Trainer"),O("healer","Healer"),O("bank","Bank"),O("custom","Custom service")], true, "shop"),
                I("service_type", "Service code", false, "0"),
                I("level_range.min", "Minimum level", false, "1"),
                I("level_range.max", "Maximum level", false, "99"),
                I("class_limit", "Class limit", false, "0"),
                I("markup_percent", "Price percent", false, "100"),
                L("@inventory_items", "Inventory item IDs", false, "One item ID per line. Default stock and regen values are filled in automatically.")
            ]),

            ["quests"] = new("quests", "Quest", "quest.custom", "Create a Custom1 quest definition without hand-writing structured data.",
            [
                T("name", "Quest name", true, "New Quest"),
                T("classification", "Quest type", false, "custom_quest"),
                M("description", "Quest description", false, ""),
                T("relationships.start_room", "Start room ID", false, ""),
                T("relationships.required_item", "Required item ID", false, ""),
                T("relationships.reward_item", "Reward item ID", false, ""),
                I("reward_xp", "Experience reward", false, "0")
            ]),

            ["triggers"] = new("triggers", "Trigger", "trigger.custom", "Create a condition/command trigger. Custom triggers are kept in the Custom1 runtime projection.",
            [
                T("name", "Trigger name", true, "New Trigger"),
                C("trigger_type", "Trigger type", [O("custom_command","Command"),O("room_or_exit_interaction","Room / exit interaction"),O("npc_quest_entry","NPC quest entry"),O("room_entry","Room entry"),O("item_use","Item use")], true, "custom_command"),
                T("interaction_kind", "Interaction kind", false, "custom"),
                T("relationships.from_room", "Room ID", false, ""),
                L("commands", "Commands", false, "One command per line."),
                T("linked_script_id", "Script ID", false, "")
            ]),

            ["scripts"] = new("scripts", "Script", "script.custom", "Create a Custom1 script using plain operation lines. No JSON is required.",
            [
                T("name", "Script name", true, "New Script"),
                C("script_type", "Script type", [O("custom_script","Custom script"),O("mmexplorer_textblock_stage","Textblock stage"),O("npc_hit_spell_textblock_chain","NPC hit-spell chain")], true, "custom_script"),
                M("description", "Description", false, ""),
                M("source_script", "Source / command text", false, ""),
                L("operations", "Operations", false, "One operation per line, in execution order.")
            ]),

            ["skills"] = new("skills", "Skill", "skill.custom", "Create a skill identity for Custom1.",
            [
                T("name", "Skill name", true, "New Skill"),
                M("description", "Description", false, ""),
                T("classification", "Skill category", false, "custom_skill")
            ]),

            ["formulas"] = new("formulas", "Formula", "formula.custom", "Create a formula definition or named rule for Custom1.",
            [
                T("name", "Formula name", true, "New Formula"),
                T("classification", "Formula category", false, "custom_formula"),
                M("definition", "Formula / rule", true, "0", "Enter the formula or rule in normal text."),
                L("inputs", "Inputs", false, "One input name per line.")
            ]),

            ["commands"] = new("commands", "Command", "command.custom", "Create a command identity and connect it to a Custom1 script or trigger.",
            [
                T("name", "Command word", true, "newcommand", "The word the player types."),
                M("description", "Help / description", false, ""),
                L("aliases", "Aliases", false, "One alternate command per line."),
                T("relationships.script", "Script ID", false, ""),
                T("relationships.trigger", "Trigger ID", false, ""),
                T("status", "Status", false, "runtime_authoritative_custom")
            ])
        };

    private static readonly IReadOnlyDictionary<string, string> FamilyLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rooms"] = "Rooms",
        ["exits"] = "Exits / Doors",
        ["npcs"] = "NPCs / Monsters",
        ["spawns"] = "NPC Spawns",
        ["items"] = "Items / Weapons / Armor",
        ["spells"] = "Spells",
        ["abilities"] = "Abilities",
        ["classes"] = "Classes",
        ["races"] = "Races",
        ["shops"] = "Shops / Services",
        ["quests"] = "Quests",
        ["triggers"] = "Triggers",
        ["scripts"] = "Scripts",
        ["skills"] = "Skills",
        ["formulas"] = "Formulas / Rules",
        ["commands"] = "Commands"
    };

    public static IReadOnlyList<CustomWorldSimpleFamilyOption> Families => Forms.Keys
        .Select(value => new CustomWorldSimpleFamilyOption(value, FamilyLabels.TryGetValue(value, out var label) ? label : value))
        .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static CustomWorldSimpleFormDefinition Get(string family)
    {
        if (Forms.TryGetValue(family, out var form)) return form;
        throw new KeyNotFoundException($"No simple editor form is registered for family '{family}'.");
    }
}
