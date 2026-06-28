namespace Reforged.Core.Configuration;

/// <summary>
/// Centralized repository path contract used by Server, Importer, Editor, Launcher, and tools.
/// </summary>
public sealed record ProjectPaths(
    string Root,
    string Apps,
    string Core,
    string Data,
    string Docs,
    string ImportedDatabases,
    string Reports,
    string Saves,
    string Tools,
    string Updates,
    string Bin);
