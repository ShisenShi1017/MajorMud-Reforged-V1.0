namespace Reforged.Server;

public interface IRuntimeSessionActor
{
    string Id { get; }
    string DisplayName { get; }
    string Kind { get; }
    bool IsPersistentCharacter { get; }
}

public sealed record RuntimeCharacterActor(
    string Id,
    string DisplayName,
    string Race,
    string CharacterClass,
    int Level) : IRuntimeSessionActor
{
    public string Kind => "character";
    public bool IsPersistentCharacter => true;
}

public sealed record RuntimeDeveloperAvatar(
    string Id,
    string DisplayName) : IRuntimeSessionActor
{
    public string Kind => "developer-avatar";
    public bool IsPersistentCharacter => false;
}
