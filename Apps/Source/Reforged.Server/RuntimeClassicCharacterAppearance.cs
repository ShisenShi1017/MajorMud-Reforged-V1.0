using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicAppearanceInput(
    string Name,
    string Sex,
    string Race,
    string CharacterClass,
    string HairLength,
    string HairColor,
    string EyeColor,
    int Strength,
    int Agility,
    int Intellect,
    int Willpower,
    int Health,
    int Charm,
    int HitPoints,
    int MaximumHitPoints,
    bool Hidden,
    bool Sneaking,
    bool Invisible,
    bool Disguised,
    bool Transformed);

public sealed record RuntimeClassicAppearanceViewer(bool SeeHidden, bool DetectInvisible, bool IsSelf);


public static class RuntimeClassicCharacterLookTargeting
{
    public static bool Matches(string actorId, string displayName, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        var value = query.Trim();
        return actorId.Equals(value, StringComparison.OrdinalIgnoreCase)
            || displayName.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }
}

public static class RuntimeClassicCharacterAppearance
{
    private static readonly int[] Thresholds = [30, 40, 50, 60, 70, 80, 90, 100];
    private static readonly string[] Strength = ["puny", "weak", "slightly built", "moderately built", "well built", "muscular", "powerfully built", "heroically proportioned", "physically Godlike"];
    private static readonly string[] Agility = ["slowly", "clumsily", "sluggishly", "cautiously", "gracefully", "very swiftly", "with uncanny speed", "with catlike agility", "blindingly fast"];
    private static readonly string[] Health = ["sickly", "frail", "thin", "healthy", "stout", "solid", "massive", "gigantic", "colossal"];
    private static readonly string[] Charm =
    [
        "openly hostile and quite revolting",
        "hostile and rather unappealing",
        "quite unfriendly and aloof",
        "likable in an unassuming sort of way",
        "quite attractive and pleasant to be around",
        "charismatic and outgoing. You can't help but like %object%",
        "extremely likeable, and fairly radiates charisma",
        "incredibly charismatic. You are almost overpowered by %possessive% strong personality",
        "overwhelmingly charismatic. You almost drop to your knees in wonder at the sight of %object%!"
    ];
    private static readonly string[] Intellect = ["utterly moronic", "quite stupid", "slightly dull", "intelligent", "bright", "extremely clever", "brilliant", "a genius", "all-knowing"];
    private static readonly string[] Willpower =
    [
        "seems selfish and hot-tempered",
        "seems sullen and impulsive",
        "seems a little naive",
        "looks fairly knowledgeable",
        "looks quite experienced and wise",
        "has a worldly air about %object%",
        "seems to possess a wisdom beyond %possessive% years",
        "seems to be in an enlightened state of mind",
        "looks like %subject% is one with the Gods"
    ];
    private static readonly string[] Wounds = ["mortally wounded", "critically wounded", "severely wounded", "heavily wounded", "moderately wounded", "slightly wounded", "unwounded"];

    public static bool CanView(RuntimeClassicAppearanceViewer viewer, RuntimeClassicAppearanceInput target)
    {
        if (viewer.IsSelf) return true;
        if (target.Invisible && !viewer.DetectInvisible) return false;
        return ClassicVisibilityPolicy.CanDetect(viewer.SeeHidden, target.Hidden, target.Sneaking);
    }

    public const int ShadowAbilityId = 9;
    public const string ShadowLookPayload = "A shadowy figure";

    public static bool TryResolveEquippedShadowLookOverride(
        RuntimeCharacterRecord character,
        RuntimeItemCatalog catalog,
        bool isSelf,
        out string payload)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(catalog);
        payload = string.Empty;
        if (isSelf || character.ItemState is null) return false;

        foreach (var item in character.ItemState.Items)
        {
            if (item.OwnerId is null
                || !item.OwnerId.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase)
                || item.EquippedSlot is null) continue;
            RuntimeItemDefinition definition;
            try { definition = catalog.Require(item.CanonicalItemId); }
            catch { continue; }
            if (definition.EquipmentAbilityModifiers.GetValueOrDefault(ShadowAbilityId) <= 0) continue;
            payload = ShadowLookPayload;
            return true;
        }
        return false;
    }

    public static bool TryResolveShadowformOverride(
        RuntimeCharacterRecord character,
        RuntimeClassicNativeTextblockPayloadAuthority native,
        out string payload)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(native);
        var spellSelector = RuntimeClassicStatusProjection.Numeric(character, "shadowform_textblock");
        var equipmentSelector = character.SemanticState?.DerivedStats.GetValueOrDefault("shadowform_textblock") ?? 0;
        var selector = spellSelector > 0 ? spellSelector : equipmentSelector;
        if (selector > 0 && native.TryResolveTextblock(selector, out payload)) return true;
        payload = string.Empty;
        return false;
    }

    public static string Compose(RuntimeClassicAppearanceViewer viewer, RuntimeClassicAppearanceInput target)
    {
        if (!CanView(viewer, target)) throw new InvalidOperationException("The target is not visible to this viewer.");

        var shownName = target.Disguised ? "A disguised figure" : target.Transformed ? "A transformed figure" : target.Name;
        var pronoun = target.Sex.Equals("Female", StringComparison.OrdinalIgnoreCase) ? "She" : "He";
        var objectPronoun = target.Sex.Equals("Female", StringComparison.OrdinalIgnoreCase) ? "her" : "him";
        var possessive = target.Sex.Equals("Female", StringComparison.OrdinalIgnoreCase) ? "her" : "his";
        string FormatPronouns(string phrase) => phrase
            .Replace("%subject%", pronoun.ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("%object%", objectPronoun, StringComparison.Ordinal)
            .Replace("%possessive%", possessive, StringComparison.Ordinal);

        var identity = string.IsNullOrWhiteSpace(target.CharacterClass)
            ? target.Race
            : $"{target.Race} {target.CharacterClass}";
        var physicalDetails = PhysicalDetails(target.HairLength, target.HairColor, target.EyeColor);
        var wound = Wound(target.HitPoints, target.MaximumHitPoints);
        return string.Join(" ",
            $"{shownName} is a {Phrase(Health, target.Health)}, {Phrase(Strength, target.Strength)} {identity}{physicalDetails}.",
            $"{pronoun} moves {Phrase(Agility, target.Agility)}, and is {FormatPronouns(Phrase(Charm, target.Charm))}.",
            $"{shownName} appears to be {FormatPronouns(Phrase(Intellect, target.Intellect))} and {FormatPronouns(Phrase(Willpower, target.Willpower))}.",
            $"{pronoun} is {wound}.");
    }

    public static string PhraseFor(string stat, int value) => stat.Trim().ToLowerInvariant() switch
    {
        "strength" or "str" => Phrase(Strength, value),
        "agility" or "agi" or "agl" => Phrase(Agility, value),
        "intellect" or "intelligence" or "int" => Phrase(Intellect, value),
        "willpower" or "wil" => Phrase(Willpower, value),
        "health" or "hea" => Phrase(Health, value),
        "charm" or "cha" => Phrase(Charm, value),
        _ => throw new KeyNotFoundException(stat)
    };


    private static string PhysicalDetails(string hairLength, string hairColor, string eyeColor)
    {
        static string Clean(string value)
        {
            var cleaned = value?.Trim() ?? string.Empty;
            return cleaned.Equals("unspecified", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("none", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("<none>", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : cleaned;
        }

        var length = Clean(hairLength);
        var color = Clean(hairColor);
        var eyes = Clean(eyeColor);
        string hair;
        if (length.Equals("bald", StringComparison.OrdinalIgnoreCase))
            hair = "with no hair";
        else if (length.Length > 0 && color.Length > 0)
            hair = $"with {length} {color} hair";
        else if (length.Length > 0)
            hair = $"with {length} hair";
        else if (color.Length > 0)
            hair = $"with {color} hair";
        else
            hair = string.Empty;

        var eyeDescription = eyes.Length > 0 ? $"{eyes} eyes" : string.Empty;
        if (hair.Length > 0 && eyeDescription.Length > 0) return $" {hair} and {eyeDescription}";
        if (hair.Length > 0) return $" {hair}";
        if (eyeDescription.Length > 0) return $" with {eyeDescription}";
        return string.Empty;
    }

    private static string Phrase(string[] table, int value)
    {
        var index = 0;
        while (index < Thresholds.Length && value >= Thresholds[index]) index++;
        return table[Math.Min(index, table.Length - 1)];
    }

    private static string Wound(int hitPoints, int maximumHitPoints)
    {
        if (maximumHitPoints <= 0 || hitPoints >= maximumHitPoints) return Wounds[6];
        var percent = hitPoints * 100 / maximumHitPoints;
        return percent < 10 ? Wounds[0]
            : percent < 20 ? Wounds[1]
            : percent < 35 ? Wounds[2]
            : percent < 50 ? Wounds[3]
            : percent < 75 ? Wounds[4]
            : Wounds[5];
    }
}

public sealed record RuntimeBundleFPhaseF2ADynamicAuditReport(
    bool Passed,
    int ThresholdCount,
    int PhraseCount,
    int DisconnectCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF2ADynamicAudit
{
    public static RuntimeBundleFPhaseF2ADynamicAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool passed, string name)
        {
            if (passed) checks.Add(name);
            else failures.Add(name);
        }

        foreach (var stat in new[] { "strength", "agility", "intellect", "willpower", "health", "charm" })
        {
            Check(RuntimeClassicCharacterAppearance.PhraseFor(stat, 29).Length > 0, stat + "-low");
            Check(RuntimeClassicCharacterAppearance.PhraseFor(stat, 100).Length > 0, stat + "-high");
        }
        Check(RuntimeClassicCharacterAppearance.PhraseFor("agl", 50) == "cautiously", "classic-agl-alias");

        const string targetName = "Shisenshi";
        for (var length = 1; length <= targetName.Length; length++)
            Check(RuntimeClassicCharacterLookTargeting.Matches("character.shisenshi", targetName, targetName[..length]), $"character-prefix-length-{length}");
        Check(!RuntimeClassicCharacterLookTargeting.Matches("character.shisenshi", targetName, "his"), "character-name-must-be-leading-prefix");

        var source = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeLiveWorld.cs"));
        Check(source.Contains("RuntimeClassicCharacterAppearance.Compose", StringComparison.Ordinal), "look-examine-live-consumer");
        Check(source.Contains("RuntimeClassicCharacterAppearance.CanView", StringComparison.Ordinal), "visibility-before-composition");
        Check(!source.Contains("TryRaceDescription", StringComparison.Ordinal), "normal-character-look-does-not-render-race-lore");
        Check(source.Contains("Stat(50, \"stat_strength\", \"strength\", \"str\")", StringComparison.Ordinal), "current-full-strength-precedes-compact-racial-key");
        Check(source.Contains("Stat(50, \"stat_agility\", \"agility\", \"agi\", \"agl\")", StringComparison.Ordinal), "current-full-agility-precedes-compact-racial-key");
        Check(source.Contains("RuntimeClassicCharacterLookTargeting.Matches", StringComparison.Ordinal), "character-name-prefix-abbreviation-consumer");
        Check(source.Contains("includeSelf: false", StringComparison.Ordinal), "direction-conflict-prefers-other-character-abbreviation");
        foreach (var alias in new[] { "\"str\"", "\"agl\"", "\"int\"", "\"wil\"", "\"hea\"", "\"cha\"" })
            Check(source.Contains(alias, StringComparison.Ordinal), "classic-stat-key-consumer:" + alias.Trim('"'));

        var hidden = new RuntimeClassicAppearanceInput("Target", "Male", "Human", "Warrior", "short", "brown", "blue", 50, 50, 50, 50, 50, 50, 100, 100, true, false, false, false, false);
        Check(!RuntimeClassicCharacterAppearance.CanView(new(false, false, false), hidden), "hidden-filter");
        Check(RuntimeClassicCharacterAppearance.CanView(new(true, false, false), hidden), "see-hidden-filter");
        Check(RuntimeClassicCharacterAppearance.Compose(new(false, false, true), hidden).Contains("healthy, moderately built Human Warrior with short brown hair and blue eyes", StringComparison.Ordinal), "decoded-threshold-composition");

        var heroic = hidden with { Name = "Shisenshi", Race = "Half-Ogre", CharacterClass = "Paladin", HairLength = "bald", EyeColor = "black", Strength = 90, Health = 100 };
        var heroicDescription = RuntimeClassicCharacterAppearance.Compose(new(false, false, true), heroic);
        Check(heroicDescription.StartsWith("Shisenshi is a colossal, heroically proportioned Half-Ogre Paladin with no hair and black eyes.", StringComparison.Ordinal), "stat-based-race-class-headline");
        Check(!heroicDescription.Contains(Environment.NewLine, StringComparison.Ordinal), "classic-look-is-one-description-paragraph");

        return new(failures.Count == 0, 8, 54, failures.Count, checks, failures);
    }
}
