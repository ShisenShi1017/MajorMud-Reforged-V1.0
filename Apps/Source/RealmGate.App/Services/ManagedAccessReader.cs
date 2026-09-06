using System.Data;
using System.Diagnostics;
using JackcessDotNet;

namespace RealmGate.App.Services;

public sealed class ManagedAccessReader : IDisposable
{
    private static readonly object DiagnosticLock = new();
    private static readonly string DiagnosticPath = Path.Combine(AppContext.BaseDirectory, "mdb-reader-diagnostics.log");

    private readonly Database _database;

    private ManagedAccessReader(Database database)
    {
        _database = database;
    }

    public static ManagedAccessReader Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An MDB/ACCDB path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var stopwatch = Stopwatch.StartNew();
        WriteDiagnostic($"OPEN START | {fullPath}");
        try
        {
            var database = Database.Open(fullPath);
            stopwatch.Stop();
            WriteDiagnostic($"OPEN DONE  | {stopwatch.Elapsed} | {fullPath}");
            return new ManagedAccessReader(database);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            WriteDiagnostic($"OPEN FAIL  | {stopwatch.Elapsed} | {exception.GetType().Name}: {exception.Message}");
            throw;
        }
    }

    public IReadOnlyList<string> ListTables()
    {
        var stopwatch = Stopwatch.StartNew();
        WriteDiagnostic("LIST TABLES START");
        try
        {
            var tables = _database.ListTables()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            stopwatch.Stop();
            WriteDiagnostic($"LIST TABLES DONE | {stopwatch.Elapsed} | {tables.Length} table(s)");
            return tables;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            WriteDiagnostic($"LIST TABLES FAIL | {stopwatch.Elapsed} | {exception.GetType().Name}: {exception.Message}");
            throw;
        }
    }

    public DataTable ReadTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("A table name is required.", nameof(tableName));

        var stopwatch = Stopwatch.StartNew();
        WriteDiagnostic($"READ START | {tableName}");
        try
        {
            var table = _database.ExportToDataTable(tableName);
            stopwatch.Stop();
            WriteDiagnostic($"READ DONE  | {stopwatch.Elapsed} | {tableName} | {table.Rows.Count} row(s) | {table.Columns.Count} column(s)");
            return table;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            WriteDiagnostic($"READ FAIL  | {stopwatch.Elapsed} | {tableName} | {exception.GetType().Name}: {exception.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        WriteDiagnostic("CLOSE DATABASE");
        _database.Dispose();
    }

    private static void WriteDiagnostic(string message)
    {
        try
        {
            lock (DiagnosticLock)
            {
                File.AppendAllText(
                    DiagnosticPath,
                    $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never interfere with MDB reading.
        }
    }
}

