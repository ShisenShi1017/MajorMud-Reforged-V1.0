using Reforged.Core.Models;

namespace Reforged.Core.World;

public interface IWorldModel
{
    string WorldName { get; }
    string DatabaseVersion { get; }
    IReadOnlyCollection<GameEntity> Entities { get; }
    GameEntity? Resolve(string entityType, string stableId);
}
