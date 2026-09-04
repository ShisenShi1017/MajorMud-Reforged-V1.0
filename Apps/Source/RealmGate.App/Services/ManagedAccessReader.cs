using System.Data;
using JackcessDotNet;

// Temporary compatibility surface while the two existing MDB consumers are migrated.
// This namespace intentionally matches the former package so no mapping/import logic changes are required.
// The implementation is JackcessDotNet only; JetDatabaseReader is no longer a package dependency.
namespace JetDatabaseReader;

public sealed class AccessReader : IDisposable
{
    private readonly Database _database;

    private AccessReader(Database database)
    {
        _database = database;
    }

    public static AccessReader Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An MDB/ACCDB path is required.", nameof(path));

        return new AccessReader(Database.Open(Path.GetFullPath(path)));
    }

    public IReadOnlyList<string> ListTables() =>
        _database.ListTables()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

    public DataTable ReadTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("A table name is required.", nameof(tableName));

        return _database.ExportToDataTable(tableName);
    }

    public void Dispose() => _database.Dispose();
}
