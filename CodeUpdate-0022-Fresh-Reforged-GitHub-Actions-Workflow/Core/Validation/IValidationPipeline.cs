namespace Reforged.Core.Validation;

public interface IValidationPipeline<in T>
{
    ValidationResult Validate(T value);
    IReadOnlyCollection<string> ValidatorNames { get; }
}
