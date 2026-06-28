using MajorMud.Reforged.Core.Models;
using MajorMud.Reforged.Core.Validation;

namespace MajorMud.Reforged.Core.Repositories;

public interface IDataRepository<TRecord> where TRecord : GameEntity
{
    string EntityType { get; }
    IReadOnlyCollection<TRecord> GetAll();
    TRecord? GetByStableId(string stableId);
    TRecord? GetByLegacyId(int legacyId);
    ValidationResult Validate();
}
