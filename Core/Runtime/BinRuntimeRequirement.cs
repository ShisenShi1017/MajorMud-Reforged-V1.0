namespace MajorMud.Reforged.Core.Runtime;

/// <summary>
/// Describes a required runtime-ready executable artifact under Bin.
/// This supports the permanent rule that normal runtime must not require the .NET SDK.
/// </summary>
public sealed class BinRuntimeRequirement
{
    public required string ApplicationName { get; init; }
    public required string RelativeExecutablePath { get; init; }
    public bool RequiredForMilestoneOne { get; init; }
}
