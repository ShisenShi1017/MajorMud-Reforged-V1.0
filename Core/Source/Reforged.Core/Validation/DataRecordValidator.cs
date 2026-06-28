using Reforged.Core.Models;

namespace Reforged.Core.Validation;

/// <summary>
/// Shared data-record validation used by importer staging, repository loaders, editor saves, and server startup checks.
/// </summary>
public sealed class DataRecordValidator<TRecord> where TRecord : DataRecord
{
    public ValidationResult Validate(IEnumerable<TRecord> records, string sourceName)
    {
        var result = new ValidationResult();
        var stableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legacyIds = new HashSet<int>();

        foreach (var record in records)
        {
            if (record.LegacyId <= 0)
            {
                result.Add(ValidationSeverity.Warning, "RECORD_LEGACY_ID_MISSING", "Record LegacyId should be positive.", sourceName, record.Name);
            }

            if (string.IsNullOrWhiteSpace(record.StableId))
            {
                result.Add(ValidationSeverity.Error, "RECORD_STABLE_ID_MISSING", "Record StableId is required.", sourceName, record.Name);
            }
            else if (!stableIds.Add(record.StableId))
            {
                result.Add(ValidationSeverity.Error, "RECORD_STABLE_ID_DUPLICATE", $"Duplicate StableId: {record.StableId}", sourceName, record.StableId);
            }

            if (record.LegacyId > 0 && !legacyIds.Add(record.LegacyId))
            {
                result.Add(ValidationSeverity.Error, "RECORD_LEGACY_ID_DUPLICATE", $"Duplicate LegacyId: {record.LegacyId}", sourceName, record.LegacyId.ToString());
            }

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                result.Add(ValidationSeverity.Warning, "RECORD_NAME_MISSING", "Record Name is empty.", sourceName, record.StableId);
            }
        }

        return result;
    }
}
