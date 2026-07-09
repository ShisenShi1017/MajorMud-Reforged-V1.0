namespace MajorMud.Reforged.Compiler;

public sealed class CompileResult
{
    public string WorldId { get; init; } = "Classic";
    public string OutputRoot { get; init; } = string.Empty;
    public int ObjectFamiliesCompiled { get; set; }
    public int ObjectsCompiled { get; set; }
    public int FilesWritten { get; set; }
    public List<CompilerDiagnostic> Diagnostics { get; } = new();
    public bool Success => Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);
}
