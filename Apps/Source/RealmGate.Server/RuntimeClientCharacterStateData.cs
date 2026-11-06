using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private string RenderClientCharacterStateData(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            return ReforgedClientProtocol.Encode("CHARACTERSTATEERROR", "A persistent character is required.");

        var character = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        if (!character.WorldId.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Character and selected world identities do not match.");

        long? currentLevelExperience = null;
        long? nextLevelExperience = null;
        int? nextLevelCharacterPointAward = null;
        try
        {
            var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var schedule = RuntimeCharacterProgressionSchedule.Load(
                Path.Combine(_characters.ProjectRoot, "Data", character.WorldId));
            var race = foundation.RequireRace(character.RaceId);
            var characterClass = foundation.RequireClass(character.ClassId);
            currentLevelExperience = schedule.Require(character, character.Level, race, characterClass).RequiredExperience;
            try
            {
                var next = schedule.Require(character, checked(character.Level + 1), race, characterClass);
                nextLevelExperience = next.RequiredExperience;
                nextLevelCharacterPointAward = next.CharacterPointAward;
            }
            catch (InvalidOperationException) { }
        }
        catch (InvalidOperationException) { }

        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var progression = semantic.Progression ?? RuntimeCharacterProgressionState.Empty;
        var characterPoints = character.Conditions.TryGetValue("character_points", out var pointsText) &&
                              int.TryParse(pointsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var points)
            ? Math.Max(0, points)
            : 0;

        return ReforgedClientProtocol.Encode("CHARACTERSTATE", JsonSerializer.Serialize(new
        {
            character.WorldId,
            character.CharacterId,
            CharacterName = character.DisplayName,
            RoomId = session.CurrentRoom.Id,
            character.RaceName,
            character.ClassName,
            character.Sex,
            character.Level,
            character.Experience,
            CurrentLevelExperience = currentLevelExperience,
            NextLevelExperience = nextLevelExperience,
            NextLevelCharacterPointAward = nextLevelCharacterPointAward,
            CharacterPointsAvailable = characterPoints,
            Lives = RuntimeClassicCharacterProgressionFormula.ReadLives(character),
            MaximumLives = RuntimeClassicCharacterProgressionFormula.MaximumLives,
            progression.ClassLevels,
            progression.ClassExperience,
            character.BaseStats,
            character.Skills,
            semantic.DerivedStats,
            semantic.Resistances,
            semantic.SavingThrows,
            Alignment = character.SemanticState?.Alignment?.Reputation ?? string.Empty,
            CapturedAtUtc = DateTimeOffset.UtcNow
        }));
    }
}
