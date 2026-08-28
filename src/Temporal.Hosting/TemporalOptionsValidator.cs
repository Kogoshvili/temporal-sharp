using Microsoft.Extensions.Options;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Options-pipeline validator for <see cref="TemporalOptions"/>, re-running the
/// shared validation rules whenever the options value is produced, including on
/// configuration reload through <see cref="IOptionsMonitor{TemporalOptions}"/>.
/// </summary>
public sealed class TemporalOptionsValidator : IValidateOptions<TemporalOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TemporalOptions options)
    {
        try
        {
            TemporalOptionsValidation.Validate(options);
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
