using RealmGate.Core.Models;

namespace RealmGate.Core.Repositories;

public interface IReadOnlyDataRepository<TRecord> where TRecord : DataRecord
{
    IReadOnlyList<TRecord> All();
    TRecord? FindByLegacyId(int legacyId);
    TRecord? FindByStableId(string stableId);
}

public interface IDataRepository<TRecord> : IReadOnlyDataRepository<TRecord> where TRecord : DataRecord
{
    void Upsert(TRecord record);
    bool RemoveByStableId(string stableId);
}
