namespace MajorMud.Core.Models;

public sealed record SourceReference(
    string SourceType,
    string SourceName,
    string? RecordKey = null,
    string Confidence = "Unknown");
