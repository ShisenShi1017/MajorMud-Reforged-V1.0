namespace Reforged.Core.Serialization;

/// <summary>
/// Shared JSON file helper so applications do not duplicate file serialization behavior.
/// </summary>
public sealed class JsonFileStore
{
    private readonly IReforgedSerializer _serializer;

    public JsonFileStore(IReforgedSerializer serializer)
    {
        _serializer = serializer;
    }

    public T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return _serializer.Deserialize<T>(File.ReadAllText(path));
    }

    public void Write<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, _serializer.Serialize(value));
    }
}
