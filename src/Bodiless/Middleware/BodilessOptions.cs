namespace Bodiless.Middleware;

public sealed class BodilessOptions
{
    public const string DefaultRequiredHeader = "Discard-Body";

    public const string DefaultRequiredValue = "true";

    public string RequiredHeader { get; set; } = DefaultRequiredHeader;

    public string? RequiredValue { get; set; } = DefaultRequiredValue;
}
