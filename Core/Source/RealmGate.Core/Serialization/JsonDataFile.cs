using System.Text.Json;
using RealmGate.Core.Models;

namespace RealmGate.Core.Serialization;

/// <summary>
/// Shared JSON loader/saver for Reforged data records.
/// </summary>
public sealed class JsonDataFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public IReadOnlyList<TRecord> LoadRecords<TRecord>(string path) where TRecord : DataRecord
    {
        if (!File.Exists(path))
        {
            return Array.Empty<TRecord>();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<TRecord>>(json, Options) ?? new List<TRecord>();
    }

    public void SaveRecords<TRecord>(string path, IEnumerable<TRecord> records) where TRecord : DataRecord
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(records, Options));
    }
}
