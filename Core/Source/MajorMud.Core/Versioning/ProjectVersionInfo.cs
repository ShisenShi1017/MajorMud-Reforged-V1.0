namespace MajorMud.Core.Versioning;

/// <summary>
/// Tracks the project, code update, and database version independently.
/// </summary>
public sealed record ProjectVersionInfo(
    string ProjectVersion,
    string CodeUpdate,
    string DatabaseVersion,
    string Milestone,
    DateTimeOffset UpdatedAtUtc);
