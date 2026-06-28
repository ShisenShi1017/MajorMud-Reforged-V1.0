namespace MajorMud.Reforged.Core.Validation;

/// <summary>
/// Reusable validator for required string properties.
/// </summary>
public sealed class RequiredStringValidator<T> : IValidator<T>
{
    private readonly string _fieldName;
    private readonly Func<T, string?> _selector;

    public RequiredStringValidator(string fieldName, Func<T, string?> selector)
    {
        _fieldName = fieldName;
        _selector = selector;
    }

    public ValidationResult Validate(T value)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(_selector(value)))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Code = "Required",
                Message = $"{_fieldName} is required."
            });
        }

        return result;
    }
}
