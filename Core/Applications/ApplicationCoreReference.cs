namespace Reforged.Core.Applications;

/// <summary>
/// Machine-readable declaration that an application source project references the Core project.
/// Repository health checks and handoff documents use this model to verify Milestone 1 progress.
/// </summary>
public sealed class ApplicationCoreReference
{
    public required ApplicationRole Role { get; init; }
    public required string ApplicationProjectPath { get; init; }
    public required string CoreProjectReference { get; init; }
    public bool IsRequiredForMilestoneOne { get; init; } = true;
}
