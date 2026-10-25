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

        long? nextLevelExperience = null;
        try
        {
            var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var schedule = RuntimeCharacterProgressionSchedule.Load(
                Path.Combine(_characters.ProjectRoot, "Data", character.WorldId));
            nextLevelExperience = schedule.Require(character, checked(character.Level + 1),
                foundation.RequireRace(character.RaceId), foundation.RequireClass(character.ClassId)).RequiredExperience;
        }
        catch (InvalidOperationException) { }

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
            NextLevelExperience = nextLevelExperience,
            character.BaseStats,
            character.Skills,
            Alignment = character.SemanticState?.Alignment?.Reputation ?? string.Empty,
            CapturedAtUtc = DateTimeOffset.UtcNow
        }));
    }
}
