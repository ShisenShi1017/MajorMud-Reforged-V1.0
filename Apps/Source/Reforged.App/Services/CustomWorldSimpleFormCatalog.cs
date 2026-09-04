namespace Reforged.App.Services;

public enum CustomWorldSimpleFieldKind
{
    Text,
    Multiline,
    Integer,
    Boolean,
    Choice,
    Lines,
    IntegerLines,
    Decimal,
    DecimalLines,
    BooleanLines,
    ObjectLines
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
    private static CustomWorldSimpleFieldDefinition IL(string path, string label, bool required = false, string help = "") =>
        new(path, label, CustomWorldSimpleFieldKind.IntegerLines, required, "", help);

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

            ["npcs"] = new("npcs", "NPC / Monster", "npc.custom", "Create a monster or NPC with normal fields. Attacks and abilities are edited visually in the dedicated Monster editor.",
            [
                T("name", "Name", true, "New NPC"),
                I("legacy_id", "Legacy number", false, "0", "Optional. Use 0 for a RealmGate-only NPC."),
                I("combat.level", "Level", true, "1"),
                I("combat.hp", "Hit points", true, "20"),
                I("combat.energy", "Energy", false, "1000"),
                I("combat.armor_class", "Armor class", false, "0"),
                I("combat.damage_resistance", "Damage resistance", false, "0"),
                I("combat.magic_resistance", "Magic resistance", false, "0"),
                I("combat.attack_count", "Number of attacks", false, "1"),
                I("combat.spell_ref_count", "Number of spells", false, "0"),
                C("behavior.alignment", "Alignment", [O("lawful_good","Lawful Good"),O("neutral_good","Neutral Good"),O("chaotic_good","Chaotic Good"),O("lawful_neutral","Lawful Neutral"),O("neutral","Neutral"),O("chaotic_neutral","Chaotic Neutral"),O("lawful_evil","Lawful Evil"),O("neutral_evil","Neutral Evil"),O("chaotic_evil","Chaotic Evil")], false, "neutral"),
                I("behavior.group_id", "Group / faction number", false, "0"),
                I("behavior.follow", "Follow value", false, "0"),
                I("behavior.charm_level", "Charm level", false, "0"),
                I("behavior.charm_resistance", "Charm resistance", false, "0"),
                B("behavior.undead", "Undead", false),
                I("behavior.regen_time", "Regen time", false, "0"),
                I("behavior.respawn_hours", "Respawn hours", false, "0"),
                I("public_reference_0139.fields.hp_regen", "HP regeneration", false, "0"),
                I("public_reference_0139.fields.backstab_defense", "Backstab defense", false, "0"),
                I("public_reference_0139.fields.game_limit", "Game limit", false, "0"),
                I("public_reference_0139.fields.gender", "Gender code", false, "0"),
                L("public_reference_0139.fields.descriptions", "Descriptions", false, "One description line per row."),
                I("rewards.xp_base", "Experience reward", false, "0"),
                I("rewards.gold", "Gold reward", false, "0"),
                I("rewards.silver", "Silver reward", false, "0"),
                I("rewards.copper", "Copper reward", false, "0"),
                I("rewards.platinum", "Platinum reward", false, "0"),
                I("rewards.runic", "Runic reward", false, "0")
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

            ["items"] = new("items", "Item", "item.custom", "Create weapons, armor, containers, consumables, or ordinary items. The dedicated Item editor separates equipment, combat, effects, restrictions, and economy.",
            [
                T("name", "Item name", true, "New Item"),
                I("legacy_id", "Legacy number", false, "0", "Optional. Use 0 for a RealmGate-only item."),
                C("classification.type", "Item type", [O("misc","Miscellaneous"),O("weapon","Weapon"),O("armor","Armor"),O("container","Container"),O("consumable","Consumable"),O("food","Food"),O("drink","Drink")], true, "misc"),
                B("flags.gettable", "Player can pick it up", true),
                I("economy.weight", "Weight", false, "0"),
                I("economy.cost", "Value / cost", false, "0"),
                C("equipment.worn_on", "Equipment slot", [O("0","Not wearable"),O("2","Head"),O("3","Face"),O("4","Neck"),O("5","Back"),O("6","Torso"),O("7","Arms"),O("8","Hands"),O("9","Waist"),O("10","Legs"),O("11","Feet"),O("12","Wield"),O("13","Off hand"),O("14","Finger"),O("15","Wrist"),O("16","Shield"),O("19","Light")], false, "0"),
                I("equipment.str_requirement", "Minimum strength", false, "0"),
                I("combat.minimum_damage", "Minimum damage", false, "0"),
                I("combat.maximum_damage", "Maximum damage", false, "0"),
                I("combat.armor_class_bonus", "Armor class bonus", false, "0"),
                I("combat.damage_resistance_bonus", "Damage resistance bonus", false, "0"),
                I("combat.accuracy", "Accuracy", false, "0"),
                I("combat.speed", "Speed", false, "0"),
                I("public_reference_0139.fields.max_uses", "Maximum uses", false, "-1", "-1 means unlimited / not use-limited."),
                B("public_reference_0139.fields.uses_consumed_on_use", "Consumes a use when activated", false),
                B("public_reference_0139.fields.retain_after_uses", "Keep item when uses reach zero", false),
                B("public_reference_0139.fields.destroy_on_death", "Destroy on death", true),
                B("public_reference_0139.fields.not_droppable", "Cannot be dropped", false),
                I("public_reference_0139.fields.game_limit", "Game limit", false, "0"),
                L("public_reference_0139.fields.class_restrictions", "Class restrictions", false, "One class number/name per line."),
                L("public_reference_0139.fields.race_restrictions", "Race restrictions", false, "One race number/name per line."),
                L("public_reference_0139.fields.descriptions", "Descriptions", false, "One description line per row.")
            ]),

            ["spells"] = new("spells", "Spell", "spell.custom", "Create or fully tune a spell with the dedicated Spell editor. Targeting, resistance, scaling, messages, and recovered gameplay controls stay available without JSON.",
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
                I("effect.duration", "Duration", false, "0"),
                I("public_reference_0139.fields.resist_type", "Resistance type", false, "0"),
                I("public_reference_0139.fields.resist_ability", "Resistance ability", false, "0"),
                I("public_reference_0139.fields.difficulty", "Difficulty", false, "0"),
                I("public_reference_0139.fields.magery_requirement_a", "Magery requirement A", false, "0"),
                I("public_reference_0139.fields.magery_requirement_b", "Magery requirement B", false, "0"),
                I("public_reference_0139.fields.level_cap", "Level cap", false, "0"),
                I("public_reference_0139.fields.levels_for_max_increase", "Levels per maximum-effect increase", false, "0"),
                I("public_reference_0139.fields.max_increase", "Maximum-effect increase", false, "0"),
                I("public_reference_0139.fields.levels_for_min_increase", "Levels per minimum-effect increase", false, "0"),
                I("public_reference_0139.fields.min_increase", "Minimum-effect increase", false, "0"),
                I("public_reference_0139.fields.levels_for_duration_increase", "Levels per duration increase", false, "0"),
                I("public_reference_0139.fields.duration_increase", "Duration increase", false, "0"),
                L("public_reference_0139.fields.descriptions", "Descriptions", false, "One description line per row."),
                T("public_reference_0139.fields.message_style", "Message style", false, ""),
                L("public_reference_0139.fields.abilities_named", "Applied abilities", false, "One named ability per line.")
            ]),

            ["abilities"] = new("abilities", "Ability", "ability.custom", "Create or fully tune an ability with the dedicated Ability editor. Description, display color, implementation controls, relationships, and every decoded gameplay field stay available without JSON.",
            [
                T("name", "Ability name", true, "New Ability"),
                I("legacy_id", "Legacy number", false, "0"),
                M("public_reference_0139.fields.description", "Description", false, ""),
                I("public_reference_0139.fields.color_red", "Display red", false, "200"),
                I("public_reference_0139.fields.color_green", "Display green", false, "200"),
                I("public_reference_0139.fields.color_blue", "Display blue", false, "200"),
                T("public_reference_0139.fields.impl_status", "Implementation status", false, "implemented"),
                M("public_reference_0139.fields.dll_notes", "Implementation notes", false, ""),
                T("category", "Category", false, "custom")
            ]),

            ["native_message_payloads"] = new("native_message_payloads", "Message", "native_message_payload", "Create or edit player-facing MajorMUD message slots with a dedicated Message editor. Custom1 can override or add messages without changing Classic.",
            [
                I("message_id", "Message number", true, "1", "Use an unused number for a new Custom1 message."),
                M("player_payload", "Player message", false, "", "Message shown to the acting player."),
                M("secondary_payload", "Secondary message", false, "", "Alternate/observer message slot used by decoded native message calls."),
                M("tertiary_payload", "Tertiary message", false, "", "Third decoded message slot when the native message provides one."),
                T("encoding", "Encoding", false, "cp437")
            ]),

            ["native_textblock_payloads"] = new("native_textblock_payloads", "Textblock", "native_textblock_payload", "Create or edit long-form MajorMUD textblocks with a dedicated Textblock editor. Enter normal text; the editor handles the Custom1 record.",
            [
                I("textblock_id", "Textblock number", true, "1", "Use an unused number for a new Custom1 textblock."),
                M("payload", "Textblock text", true, "", "The complete text shown or consumed by scripts/quests."),
                T("encoding", "Encoding", false, "cp437")
            ]),

            ["room_interactions"] = new("room_interactions", "Action / Room Interaction", "room_interaction.custom", "Create room actions, remote actions, traps, switches, command interactions, and other decoded room behaviors with a dedicated Action editor.",
            [
                T("name", "Action name", true, "New Action"),
                T("interaction_kind", "Action type", true, "custom_command"),
                T("relationships.from_room", "Source room", true, "", "Choose the room where this action exists. Use Find Rooms so you do not need to know the ID."),
                T("relationships.to_room", "Destination room", false, "", "Optional room destination or target."),
                T("direction", "Direction", false, "", "Optional direction when the action belongs to an exit."),
                L("commands", "Accepted commands", false, "One command or phrase per line."),
                T("command_semantics.required_item_id", "Required item", false, ""),
                I("command_semantics.minimum_level", "Minimum level", false, "0"),
                I("command_semantics.maximum_level", "Maximum level", false, "0"),
                I("command_semantics.native_textblock_id", "Textblock number", false, "0"),
                IL("command_semantics.success_message_ids", "Success message numbers", false, "One message number per line."),
                T("command_semantics.return_room_id", "Return / fallback room", false, ""),
                T("runtime_disposition", "Runtime status", false, "active_runtime")
            ]),

            ["classes"] = new("classes", "Class", "class.custom", "Create or edit a class in the dedicated Class editor. Every currently decoded class gameplay field is grouped into readable tabs, and newly decoded fields appear automatically.",
            [
                T("name", "Class name", true, "New Class"),
                M("description", "Description", false, ""),
                T("classification", "Class category", false, "custom_playable_class")
            ]),

            ["races"] = new("races", "Race", "race.custom", "Create or edit a race in the dedicated Race editor. Every currently decoded racial gameplay field is exposed without requiring JSON.",
            [
                T("name", "Race name", true, "New Race"),
                M("description", "Description", false, ""),
                T("classification", "Race category", false, "custom_playable_race")
            ]),

            ["shops"] = new("shops", "Shop / Service", "shop.custom", "Create a merchant, trainer, spell shop, bank, healer, or other service. The dedicated Shop editor includes a normal stock-item editor with quantity and regeneration controls.",
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
                M("public_reference_0139.fields.descriptions.description_a", "Description A", false, ""),
                M("public_reference_0139.fields.descriptions.description_b", "Description B", false, ""),
                M("public_reference_0139.fields.descriptions.description_c", "Description C", false, ""),
                L("@inventory_items", "Inventory item IDs", false, "One item ID per line. The dedicated Inventory tab provides full stock quantities and regeneration controls.")
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
        ["native_message_payloads"] = "Messages",
        ["native_textblock_payloads"] = "Textblocks",
        ["room_interactions"] = "Actions / Room Interactions",
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

        var normalized = (family ?? string.Empty).Trim();
        if (normalized.Length == 0) throw new KeyNotFoundException("Editor family is required.");
        var singular = normalized.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && normalized.Length > 3
            ? normalized[..^3] + "y"
            : normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1
                ? normalized[..^1]
                : normalized;
        var display = Humanize(normalized);
        var singularDisplay = Humanize(singular);
        return new CustomWorldSimpleFormDefinition(
            normalized,
            singularDisplay,
            singular + ".custom",
            $"Create or edit {display.ToLowerInvariant()} using the fields discovered from the Librarium. System identity fields remain protected by the editor.",
            []);
    }

    public static string LabelForFamily(string family)
        => FamilyLabels.TryGetValue(family, out var label) ? label : Humanize(family);

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var words = value.Replace('-', '_').Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", words.Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
