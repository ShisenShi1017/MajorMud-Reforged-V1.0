namespace MajorMud.Reforged.Core.Provenance;

public sealed class SourceProvenance
{
    public string SourceKind { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string? SourceRecordId { get; set; }
    public string Confidence { get; set; } = "Unknown";
    public string Notes { get; set; } = string.Empty;
}
