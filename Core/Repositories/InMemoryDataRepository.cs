using MajorMud.Reforged.Core.Models;
using MajorMud.Reforged.Core.Validation;

namespace MajorMud.Reforged.Core.Repositories;

/// <summary>
/// Dependency-free repository useful for tests, importer staging, editor previews, and server bootstrapping.
/// </summary>
public sealed class InMemoryDataRepository<TRecord> : IDataRepository<TRecord>
    where TRecord : class, IReforgedDataRecord
{
    private readonly Dictionary<string, TRecord> _stableIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, TRecord> _legacyIds = new();

    public InMemoryDataRepository(string entityType)
    {
        EntityType = string.IsNullOrWhiteSpace(entityType) ? typeof(TRecord).Name : entityType.Trim();
    }

    public string EntityType { get; }

    public IReadOnlyCollection<TRecord> GetAll() => _stableIds.Values.OrderBy(r => r.StableId).ToArray();

    public TRecord? GetByStableId(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            return null;
        }

        return _stableIds.TryGetValue(stableId, out var record) ? record : null;
    }

    public TRecord? GetByLegacyId(int legacyId)
    {
        return _legacyIds.TryGetValue(legacyId, out var record) ? record : null;
    }

    public void AddOrReplace(TRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.StableId))
        {
            throw new InvalidOperationException("Cannot save a record with a blank StableId.");
        }

        _stableIds[record.StableId] = record;
        if (record.LegacyId.HasValue)
        {
            _legacyIds[record.LegacyId.Value] = record;
        }
    }

    public ValidationResult Validate()
    {
        var result = new ValidationResult();
        foreach (var record in _stableIds.Values)
        {
            if (!string.Equals(record.EntityType, EntityType, StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "EntityTypeMismatch",
                    Message = $"Repository {EntityType} contains record {record.StableId} with entity type {record.EntityType}."
                });
            }

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "BlankName",
                    Message = $"Record {record.StableId} has a blank name."
                });
            }
        }

        return result;
    }
}
