using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeRerollWarningSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeRerollWarningSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        var warningOn = ProbeCharacter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeClassicWarningAuthority.ConditionKey] = "1"
        });
        var warningOff = warningOn with
        {
            Conditions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeClassicWarningAuthority.ConditionKey] = "0"
            }
        };
        Check(RuntimeClassicWarningAuthority.ShouldBlockPositiveEvilPointAction(warningOn, 0, 10), "warning-on-blocks-positive-evil-points");
        Check(!RuntimeClassicWarningAuthority.ShouldBlockPositiveEvilPointAction(warningOff, 0, 10), "warning-off-allows-positive-evil-points");
        Check(!RuntimeClassicWarningAuthority.ShouldBlockPositiveEvilPointAction(warningOn, 10, 10), "warning-does-not-block-zero-evil-point-action");
        Check(RuntimeClassicDeathExperienceAuthority.FinalRerollConsumerConnected, "reroll-retained-experience-consumer-connected");

        var temp = Path.Combine(Path.GetTempPath(), "reforged-reroll-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var repository = new RuntimeCharacterRepository(temp);
            var account = repository.GetOrCreateAccount("reroll-smoke");
            var oldCharacter = ProbeCharacter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["legacy.quest"] = "complete",
                [RuntimeClassicWarningAuthority.ConditionKey] = "1"
            }) with
            {
                CharacterId = "classic:oldhero", AccountId = account.AccountId, DisplayName = "Oldhero",
                Inventory = new[] { "item.legacy" }, Experience = 123456
            };
            repository.CreateCharacter(account, oldCharacter);
            account = repository.TryLoadAccount(account.AccountId)!;
            var replacement = ProbeCharacter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)) with
            {
                CharacterId = "classic:newhero", AccountId = account.AccountId, DisplayName = "Newhero",
                Level = 1, Experience = 12345, Inventory = Array.Empty<string>()
            };
            var committed = repository.ReplaceCharacter(account, oldCharacter, replacement);
            var listed = repository.ListCharacters(account.AccountId);
            Check(repository.TryLoadCharacter(oldCharacter.CharacterId) is null, "reroll-old-character-save-removed");
            Check(repository.TryLoadCharacter(replacement.CharacterId)?.Experience == 12345, "reroll-replacement-save-written");
            Check(listed.Count == 1 && listed[0].CharacterId == committed.CharacterId, "reroll-account-slot-replaced");
            Check(listed[0].Inventory.Count == 0 && !listed[0].Conditions.ContainsKey("legacy.quest"), "reroll-old-character-state-not-copied");

            var powerRepository = new RuntimeLivePowerStateRepository(temp, "Classic");
            powerRepository.Save(new RuntimeLivePowerState(
                new RuntimeSpellState(
                    new[]
                    {
                        new RuntimeKnownSpell(oldCharacter.CharacterId, "spell.old", DateTimeOffset.UnixEpoch),
                        new RuntimeKnownSpell(replacement.CharacterId, "spell.new", DateTimeOffset.UnixEpoch)
                    },
                    new[]
                    {
                        new RuntimeActiveSpellEffect("effect.old", "spell.old", oldCharacter.CharacterId, new RuntimeSpellTarget("character", oldCharacter.CharacterId), 1, 1, 10, DateTimeOffset.UnixEpoch, null),
                        new RuntimeActiveSpellEffect("effect.new", "spell.new", replacement.CharacterId, new RuntimeSpellTarget("character", replacement.CharacterId), 1, 1, 10, DateTimeOffset.UnixEpoch, null)
                    },
                    new Dictionary<string, RuntimeSpellResources>(StringComparer.OrdinalIgnoreCase)
                    {
                        [oldCharacter.CharacterId] = new(10, 10),
                        [replacement.CharacterId] = new(20, 20)
                    }),
                new Dictionary<string, RuntimeActorAbilitySnapshot>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase)
                {
                    [oldCharacter.CharacterId + "\u001fspell.old"] = DateTimeOffset.UnixEpoch.AddMinutes(1),
                    [replacement.CharacterId + "\u001fspell.new"] = DateTimeOffset.UnixEpoch.AddMinutes(1)
                },
                DateTimeOffset.UnixEpoch));
            powerRepository.RemoveCharacter(oldCharacter.CharacterId);
            var powers = powerRepository.Load();
            Check(powers.Spells.KnownSpells.All(spell => !spell.CharacterId.Equals(oldCharacter.CharacterId, StringComparison.OrdinalIgnoreCase))
                  && powers.Spells.ActiveEffects.All(effect => !effect.CasterId.Equals(oldCharacter.CharacterId, StringComparison.OrdinalIgnoreCase)
                                                             && !effect.Target.Id.Equals(oldCharacter.CharacterId, StringComparison.OrdinalIgnoreCase))
                  && !powers.Spells.Resources.ContainsKey(oldCharacter.CharacterId)
                  && powers.SpellCooldowns.Keys.All(key => !key.StartsWith(oldCharacter.CharacterId + "\u001f", StringComparison.OrdinalIgnoreCase)),
                "reroll-old-live-power-state-purged");
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { }
        }

        return new(failures.Count == 0, checks, failures);
    }

    private static RuntimeCharacterRecord ProbeCharacter(IReadOnlyDictionary<string, string> conditions) => new(
        "classic:warning-probe", "account", "Warning Probe", "Classic", "race.1", "Human", "class.1", "Warrior",
        2, 1000, 100, 100, 0, 0, "room.map1.2140", 2140,
        new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
        new Dictionary<string, string?>(), conditions, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}
