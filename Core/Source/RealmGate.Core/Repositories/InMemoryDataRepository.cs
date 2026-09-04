using RealmGate.Core.Models;

namespace RealmGate.Core.Repositories;

/// <summary>
/// Dependency-free repository implementation for tests, importer staging, and server bootstrap smoke checks.
/// </summary>
public sealed class InMemoryDataRepository<TRecord> : IDataRepository<TRecord> where TRecord : DataRecord
{
    private readonly Dictionary<string, TRecord> byStableId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TRecord> All() => byStableId.Values.OrderBy(r => r.LegacyId).ToList();

    public TRecord? FindByLegacyId(int legacyId) => byStableId.Values.FirstOrDefault(r => r.LegacyId == legacyId);

    public TRecord? FindByStableId(string stableId)
        => byStableId.TryGetValue(stableId, out var record) ? record : null;

    public void Upsert(TRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.StableId))
        {
            throw new ArgumentException("Record StableId is required.", nameof(record));
        }

        byStableId[record.StableId] = record;
    }

    public bool RemoveByStableId(string stableId) => byStableId.Remove(stableId);
}
