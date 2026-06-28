namespace MajorMud.Reforged.Core.Validation;

public static class ValidationExtensions
{
    public static bool HasErrors(this ValidationResult result)
    {
        return result.Issues.Any(i => i.Severity == ValidationSeverity.Error);
    }

    public static ValidationResult Merge(this ValidationResult target, ValidationResult source)
    {
        foreach (var issue in source.Issues)
        {
            target.Issues.Add(issue);
        }

        return target;
    }
}
