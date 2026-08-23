using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reforged.App.Forge;

namespace Reforged.App.Brains;

public static class LocalGgufPerformancePresets
{
    public const string Auto = "Auto";
    public const string Fast = "Fast";
    public const string Balanced = "Balanced";
    public const string Quality = "Quality";

    public static IReadOnlyList<string> All { get; } = [Auto, Fast, Balanced, Quality];

    public static string NormalizeName(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "fast" => Fast,
        "balanced" => Balanced,
        "quality" => Quality,
        _ => Auto,
    };

    public static void Apply(LocalGgufBrainSettings settings, string preset, string? modelPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = NormalizeName(preset);
        var lastSafeGpuLayers = settings.LastSuccessfulGpuLayerCount > 0
            ? settings.LastSuccessfulGpuLayerCount
            : settings.GpuLayerCount > 0
                ? settings.GpuLayerCount
                : 20;

        switch (normalized)
        {
            case Fast:
                settings.ContextSize = 4096;
                settings.GpuLayerCount = -1;
                settings.MaxTokens = 256;
                settings.PrivateConversationMaxTokens = 224;
                settings.ExternalConversationMaxTokens = 96;
                settings.NaturalInitiativeMaxTokens = 96;
                settings.RepositoryResearchMaxTokens = 640;
                settings.EngineeringDraftMaxTokens = 640;
                settings.HistoryMessageLimit = 4;
                settings.PromptSafetyMarginTokens = 320;
                settings.StatusUpdateMilliseconds = 900;
                settings.Temperature = 0.60f;
                settings.TechnicalTemperature = 0.36f;
                break;

            case Quality:
                settings.ContextSize = 8192;
                settings.GpuLayerCount = lastSafeGpuLayers;
                settings.MaxTokens = 768;
                settings.PrivateConversationMaxTokens = 384;
                settings.ExternalConversationMaxTokens = 144;
                settings.NaturalInitiativeMaxTokens = 160;
                settings.RepositoryResearchMaxTokens = 900;
                settings.EngineeringDraftMaxTokens = 900;
                settings.HistoryMessageLimit = 12;
                settings.PromptSafetyMarginTokens = 384;
                settings.StatusUpdateMilliseconds = 650;
                settings.Temperature = 0.65f;
                settings.TechnicalTemperature = 0.42f;
                break;

            case Balanced:
                settings.ContextSize = 4096;
                settings.GpuLayerCount = lastSafeGpuLayers;
                settings.MaxTokens = 512;
                settings.PrivateConversationMaxTokens = 320;
                settings.ExternalConversationMaxTokens = 128;
                settings.NaturalInitiativeMaxTokens = 128;
                settings.RepositoryResearchMaxTokens = 768;
                settings.EngineeringDraftMaxTokens = 768;
                settings.HistoryMessageLimit = 6;
                settings.PromptSafetyMarginTokens = 320;
                settings.StatusUpdateMilliseconds = 750;
                settings.Temperature = 0.62f;
                settings.TechnicalTemperature = 0.38f;
                break;

            default:
                ApplyAutomatic(settings, modelPath, lastSafeGpuLayers);
                normalized = Auto;
                break;
        }

        settings.PerformancePreset = normalized;
        settings.Normalize();
    }

    private static void ApplyAutomatic(LocalGgufBrainSettings settings, string? modelPath, int lastSafeGpuLayers)
    {
        var availableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (availableBytes <= 0)
            availableBytes = 16L * 1024 * 1024 * 1024;

        var modelBytes = LocalGgufModelLocator.GetModelFootprintBytes(modelPath);
        var availableGiB = availableBytes / (1024d * 1024d * 1024d);
        var modelGiB = modelBytes / (1024d * 1024d * 1024d);
        var memoryHeadroomGiB = availableGiB - modelGiB;

        if (availableGiB >= 48 && memoryHeadroomGiB >= 16)
        {
            settings.ContextSize = 8192;
            settings.MaxTokens = 512;
            settings.HistoryMessageLimit = 10;
        }
        else if (availableGiB >= 24 && memoryHeadroomGiB >= 8)
        {
            settings.ContextSize = 4096;
            settings.MaxTokens = 384;
            settings.HistoryMessageLimit = 6;
        }
        else
        {
            settings.ContextSize = 3072;
            settings.MaxTokens = 256;
            settings.HistoryMessageLimit = 4;
        }

        settings.GpuLayerCount = lastSafeGpuLayers;
        settings.PrivateConversationMaxTokens = settings.ContextSize >= 8192 ? 384 : settings.ContextSize >= 4096 ? 320 : 224;
        settings.ExternalConversationMaxTokens = settings.ContextSize >= 4096 ? 128 : 96;
        settings.NaturalInitiativeMaxTokens = settings.ContextSize >= 4096 ? 128 : 96;
        settings.RepositoryResearchMaxTokens = settings.ContextSize >= 8192 ? 900 : settings.ContextSize >= 4096 ? 768 : 640;
        settings.EngineeringDraftMaxTokens = settings.RepositoryResearchMaxTokens;
        settings.PromptSafetyMarginTokens = settings.ContextSize >= 8192 ? 384 : 320;
        settings.StatusUpdateMilliseconds = settings.ContextSize >= 8192 ? 650 : 750;
        settings.Temperature = 0.62f;
        settings.TechnicalTemperature = 0.38f;
    }
}

public sealed class LocalGgufBrainSettings
{
    public bool Enabled { get; set; } = true;
    public string ModelPath { get; set; } = "AI/Models/qwen-14b-instruct.gguf";
    public bool AutoDiscoverModel { get; set; } = true;
    public string PerformancePreset { get; set; } = LocalGgufPerformancePresets.Auto;
    public bool AutoTuneOnFirstLoad { get; set; } = true;
    public int ContextSize { get; set; } = 4096;
    public int GpuLayerCount { get; set; } = 20;
    public int LastSuccessfulGpuLayerCount { get; set; } = 20;
    public int MaxTokens { get; set; } = 384;
    public int PrivateConversationMaxTokens { get; set; } = 320;
    public int ExternalConversationMaxTokens { get; set; } = 128;
    public int NaturalInitiativeMaxTokens { get; set; } = 128;
    public int RepositoryResearchMaxTokens { get; set; } = 768;
    public int EngineeringDraftMaxTokens { get; set; } = 768;
    public int PromptSafetyMarginTokens { get; set; } = 320;
    public int StatusUpdateMilliseconds { get; set; } = 750;
    public float Temperature { get; set; } = 0.62f;
    public float TechnicalTemperature { get; set; } = 0.38f;
    public float TopP { get; set; } = 0.90f;
    public float RepeatPenalty { get; set; } = 1.10f;
    public int HistoryMessageLimit { get; set; } = 6;

    public void Normalize()
    {
        ModelPath = string.IsNullOrWhiteSpace(ModelPath)
            ? "AI/Models/qwen-14b-instruct.gguf"
            : ModelPath.Trim();
        PerformancePreset = LocalGgufPerformancePresets.NormalizeName(PerformancePreset);
        ContextSize = Math.Clamp(ContextSize, 1024, 131072);
        GpuLayerCount = Math.Clamp(GpuLayerCount, -1, 999);
        LastSuccessfulGpuLayerCount = Math.Clamp(LastSuccessfulGpuLayerCount, -1, 999);
        MaxTokens = Math.Clamp(MaxTokens, 64, 1000);
        PrivateConversationMaxTokens = Math.Clamp(PrivateConversationMaxTokens, 128, 384);
        ExternalConversationMaxTokens = Math.Clamp(ExternalConversationMaxTokens, 64, 160);
        NaturalInitiativeMaxTokens = Math.Clamp(NaturalInitiativeMaxTokens, 64, 192);
        RepositoryResearchMaxTokens = Math.Clamp(RepositoryResearchMaxTokens, 512, 1000);
        EngineeringDraftMaxTokens = Math.Clamp(EngineeringDraftMaxTokens, 512, 1000);
        PromptSafetyMarginTokens = Math.Clamp(PromptSafetyMarginTokens, 128, 1024);
        StatusUpdateMilliseconds = Math.Clamp(StatusUpdateMilliseconds, 250, 2500);
        Temperature = Math.Clamp(Temperature, 0.0f, 2.0f);
        TechnicalTemperature = Math.Clamp(TechnicalTemperature, 0.0f, 1.0f);
        TopP = Math.Clamp(TopP, 0.05f, 1.0f);
        RepeatPenalty = Math.Clamp(RepeatPenalty, 0.5f, 2.0f);
        HistoryMessageLimit = Math.Clamp(HistoryMessageLimit, 0, 40);
    }
}

public static class LocalGgufBrainSettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static string GetPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Config", "borin-brain.json");

    public static async Task<LocalGgufBrainSettings> LoadAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        var settings = new LocalGgufBrainSettings();
        var path = GetPath(repositoryRoot);
        if (File.Exists(path))
        {
            await using var stream = File.OpenRead(path);
            settings = await JsonSerializer.DeserializeAsync<LocalGgufBrainSettings>(stream, Json, token)
                ?? settings;
        }

        settings.Normalize();
        return settings;
    }

    public static async Task SaveAsync(
        string repositoryRoot,
        LocalGgufBrainSettings settings,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();
        var path = GetPath(repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        var json = JsonSerializer.Serialize(settings, Json);
        await File.WriteAllTextAsync(temporaryPath, json, token);
        File.Move(temporaryPath, path, overwrite: true);
    }
}

public static class LocalGgufModelLocator
{
    public static string ResolveRepositoryRoot(BrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = new[]
        {
            context.Conversation.State.Configuration.RepositoryPath,
            context.Conversation.RepositorySnapshot?.RootPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        };

        foreach (var candidate in candidates)
        {
            var root = FindRepositoryRoot(candidate);
            if (root is not null)
                return root;
        }

        return Path.GetFullPath(Directory.GetCurrentDirectory());
    }

    public static string? ResolveModelPath(string repositoryRoot, LocalGgufBrainSettings settings)
    {
        var configured = Environment.ExpandEnvironmentVariables(settings.ModelPath);
        var configuredPath = Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(repositoryRoot, configured));

        if (File.Exists(configuredPath) && configuredPath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            return configuredPath;

        if (!settings.AutoDiscoverModel)
            return null;

        var modelDirectory = Path.Combine(repositoryRoot, "AI", "Models");
        if (!Directory.Exists(modelDirectory))
            return null;

        return Directory.EnumerateFiles(modelDirectory, "*.gguf", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.Length)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    public static long GetModelFootprintBytes(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            return 0;

        var file = new FileInfo(modelPath);
        var name = file.Name;
        var shardMarker = name.IndexOf("-00001-of-", StringComparison.OrdinalIgnoreCase);
        if (shardMarker < 0)
            return file.Length;

        var ofMarker = name.IndexOf("-of-", shardMarker, StringComparison.OrdinalIgnoreCase);
        if (ofMarker < 0)
            return file.Length;

        var suffix = name[ofMarker..];
        var prefix = name[..shardMarker];
        return Directory.EnumerateFiles(file.DirectoryName!, "*.gguf", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(candidate => candidate.Name.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase) &&
                                candidate.Name.Contains(suffix, StringComparison.OrdinalIgnoreCase))
            .Sum(candidate => candidate.Length);
    }

    private static string? FindRepositoryRoot(string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
            return null;

        var full = Path.GetFullPath(start);
        var directory = File.Exists(full) ? Path.GetDirectoryName(full) : full;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var marker = Path.Combine(directory, "Apps", "Source", "Reforged.App", "Reforged.App.csproj");
            if (File.Exists(marker))
                return directory;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
