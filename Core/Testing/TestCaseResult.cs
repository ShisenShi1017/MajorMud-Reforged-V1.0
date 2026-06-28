namespace MajorMud.Reforged.Core.Testing;

public sealed class TestCaseResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
}
