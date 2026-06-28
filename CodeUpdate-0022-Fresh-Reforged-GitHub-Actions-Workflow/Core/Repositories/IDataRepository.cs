using Reforged.Core.Models;
using Reforged.Core.Validation;

namespace Reforged.Core.Repositories;

/// <summary>
/// Core repository contract for any editable data collection under Data/.
/// </summary>
public interface IDataRepository<TRecord> where TRecord : IReforgedDataRecord
{
    string EntityType { get; }
    IReadOnlyCollection<TRecord> GetAll();
    TRecord? GetByStableId(string stableId);
    TRecord? GetByLegacyId(int legacyId);
    ValidationResult Validate();
}
