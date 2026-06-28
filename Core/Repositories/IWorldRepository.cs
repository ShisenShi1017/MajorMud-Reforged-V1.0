using Reforged.Core.Models;
using Reforged.Core.Validation;

namespace Reforged.Core.Repositories;

public interface IWorldRepository
{
    IReadOnlyCollection<GameEntity> Entities { get; }
    GameEntity? Find(string entityType, string stableId);
    ValidationResult ValidateWorld();
}
