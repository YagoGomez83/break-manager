using BreakManager.Domain.Identity;
using Microsoft.Extensions.Options;

namespace BreakManager.Application.Identity;

/// <summary>
/// Parámetros configurables de identidad (sección <c>Identity</c>; ver docs/fases/fase-02-identidad.md).
/// Los valores por defecto son los iniciales de la spec.
/// </summary>
public sealed class IdentityParameters
{
    public const string SectionName = "Identity";

    public int OperatorLockoutAttempts { get; set; } = 5;

    public TimeSpan OperatorLockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

    public int OperatorLockoutsBeforeManualUnlock { get; set; } = 3;

    public TimeSpan OperatorLockoutWindow { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan EnrollmentCodeLifetime { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan AgentSessionLifetime { get; set; } = TimeSpan.FromHours(8);

    public TimeSpan LoginTicketLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan WebSessionLifetime { get; set; } = TimeSpan.FromHours(8);

    public TimeSpan WebSessionRevalidationInterval { get; set; } = TimeSpan.FromMinutes(5);

    public int WebLockoutAttempts { get; set; } = 5;

    public TimeSpan WebLockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    public int WebPasswordMinLength { get; set; } = 12;

    public LockoutPolicy ToOperatorLockoutPolicy() =>
        new(OperatorLockoutAttempts, OperatorLockoutDuration, OperatorLockoutsBeforeManualUnlock, OperatorLockoutWindow);
}

/// <summary>
/// Valida los parámetros al arrancar: todos los valores tienen que ser positivos.
/// </summary>
public sealed class IdentityParametersValidator : IValidateOptions<IdentityParameters>
{
    public ValidateOptionsResult Validate(string? name, IdentityParameters options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        RequirePositive(options.OperatorLockoutAttempts, nameof(options.OperatorLockoutAttempts), failures);
        RequirePositive(options.OperatorLockoutDuration, nameof(options.OperatorLockoutDuration), failures);
        RequirePositive(options.OperatorLockoutsBeforeManualUnlock, nameof(options.OperatorLockoutsBeforeManualUnlock), failures);
        RequirePositive(options.OperatorLockoutWindow, nameof(options.OperatorLockoutWindow), failures);
        RequirePositive(options.EnrollmentCodeLifetime, nameof(options.EnrollmentCodeLifetime), failures);
        RequirePositive(options.AgentSessionLifetime, nameof(options.AgentSessionLifetime), failures);
        RequirePositive(options.LoginTicketLifetime, nameof(options.LoginTicketLifetime), failures);
        RequirePositive(options.WebSessionLifetime, nameof(options.WebSessionLifetime), failures);
        RequirePositive(options.WebSessionRevalidationInterval, nameof(options.WebSessionRevalidationInterval), failures);
        RequirePositive(options.WebLockoutAttempts, nameof(options.WebLockoutAttempts), failures);
        RequirePositive(options.WebLockoutDuration, nameof(options.WebLockoutDuration), failures);
        RequirePositive(options.WebPasswordMinLength, nameof(options.WebPasswordMinLength), failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(int value, string name, List<string> failures)
    {
        if (value <= 0)
        {
            failures.Add($"{SectionPrefix(name)} debe ser mayor que 0.");
        }
    }

    private static void RequirePositive(TimeSpan value, string name, List<string> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{SectionPrefix(name)} debe ser mayor que 0.");
        }
    }

    private static string SectionPrefix(string name) => $"{IdentityParameters.SectionName}:{name}";
}
