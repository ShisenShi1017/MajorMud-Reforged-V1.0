using MajorMud.Reforged.Core.Models;
using MajorMud.Reforged.Core.Validation;

namespace MajorMud.Reforged.Core.Repositories;

public interface IWorldRepository
{
    IReadOnlyCollection<GameEntity> Entities { get; }
    GameEntity? Find(string entityType, string stableId);
    ValidationResult ValidateWorld();
}
