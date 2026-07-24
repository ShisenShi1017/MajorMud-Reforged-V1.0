namespace Reforged.Server;

public enum RuntimeWorldMode { Classic, Custom, Ai }
public enum RuntimeExtensionSurface { Runtime, Save, Catalog, Command, Builder, Client, Librarium, Editor }
public sealed record RuntimeExtensionDefinition(string ExtensionId, RuntimeWorldMode MinimumMode, int Version, IReadOnlySet<RuntimeExtensionSurface> Surfaces, bool EnabledByDefault=false, bool ClassicCompatible=false);
public sealed record RuntimeExtensionContext(RuntimeWorldMode WorldMode, bool HostAuthorized, int SaveVersion, IReadOnlySet<string> EnabledExtensions);
public sealed record RuntimeExtensionDecision(bool Allowed, string Code, int RequiredVersion=0, bool RequiresMigration=false);

public sealed class RuntimeExtensionBoundaryService
{
    private readonly Dictionary<string,RuntimeExtensionDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    public RuntimeExtensionBoundaryService(IEnumerable<RuntimeExtensionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.ExtensionId)) throw new ArgumentException("extension-id-required");
            if (definition.Version < 1) throw new ArgumentException($"extension-version-invalid:{definition.ExtensionId}");
            if (definition.Surfaces.Count == 0) throw new ArgumentException($"extension-surfaces-required:{definition.ExtensionId}");
            if (definition.MinimumMode == RuntimeWorldMode.Classic && !definition.ClassicCompatible) throw new ArgumentException($"classic-extension-must-be-compatible:{definition.ExtensionId}");
            if (!_definitions.TryAdd(definition.ExtensionId, definition)) throw new ArgumentException($"extension-duplicate:{definition.ExtensionId}");
        }
    }

    public RuntimeExtensionDecision Authorize(string extensionId, RuntimeExtensionSurface surface, RuntimeExtensionContext context, bool mutation)
    {
        if (!_definitions.TryGetValue(extensionId, out var definition)) return new(false,"extension-unknown");
        if (!definition.Surfaces.Contains(surface)) return new(false,"extension-surface-denied");
        if (context.WorldMode < definition.MinimumMode) return new(false,"extension-world-mode-denied");
        if (context.WorldMode == RuntimeWorldMode.Classic && !definition.ClassicCompatible) return new(false,"extension-classic-isolation");
        if (mutation && !context.HostAuthorized) return new(false,"extension-host-authorization-required");
        if (!context.EnabledExtensions.Contains(extensionId) && !definition.EnabledByDefault) return new(false,"extension-not-enabled");
        if (context.SaveVersion > definition.Version) return new(false,"extension-save-version-newer", definition.Version);
        if (context.SaveVersion < definition.Version) return new(false,"extension-save-migration-required", definition.Version, true);
        return new(true,"extension-authorized",definition.Version);
    }

    public IReadOnlyList<string> ValidateClassicIsolation(RuntimeExtensionContext context)
    {
        if (context.WorldMode != RuntimeWorldMode.Classic) return Array.Empty<string>();
        return context.EnabledExtensions
            .Where(id => !_definitions.TryGetValue(id, out var definition) || !definition.ClassicCompatible)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
