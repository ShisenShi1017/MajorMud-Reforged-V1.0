namespace Reforged.Core.Provenance;

public interface IProvenanceTracked
{
    IReadOnlyCollection<SourceProvenance> Provenance { get; }
}
