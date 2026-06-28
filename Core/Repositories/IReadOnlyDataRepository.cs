using MajorMud.Reforged.Core.Models;

namespace MajorMud.Reforged.Core.Repositories;

/// <summary>
/// Read-only repository view used by server startup, validation, editor previews, and import audits.
/// </summary>
public interface IReadOnlyDataRepository<out TRecord> where TRecord : IReforgedDataRecord
{
    string EntityType { get; }
    IReadOnlyCollection<TRecord> GetAll();
    TRecord? GetByStableId(string stableId);
    TRecord? GetByLegacyId(int legacyId);
}
