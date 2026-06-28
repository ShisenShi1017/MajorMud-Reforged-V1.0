namespace MajorMud.Reforged.Core.Validation;

public sealed class ValidationPipeline<T> : IValidationPipeline<T>
{
    private readonly List<IValidator<T>> _validators = new();

    public IReadOnlyCollection<string> ValidatorNames => _validators.Select(v => v.GetType().Name).ToArray();

    public ValidationPipeline<T> Add(IValidator<T> validator)
    {
        _validators.Add(validator);
        return this;
    }

    public ValidationResult Validate(T value)
    {
        var merged = new ValidationResult();
        foreach (var validator in _validators)
        {
            var result = validator.Validate(value);
            foreach (var issue in result.Issues)
            {
                merged.Issues.Add(issue);
            }
        }
        return merged;
    }
}
