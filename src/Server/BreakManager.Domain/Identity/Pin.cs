using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// PIN del operador en texto plano, ya validado: exactamente 6 dígitos y no trivial.
/// Existe solo en memoria, entre la entrada y el hash; nunca se persiste ni se loguea.
/// </summary>
public sealed class Pin
{
    public const int Length = 6;

    private Pin(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    /// Valida el formato y rechaza los PIN triviales: todos los dígitos iguales, secuencias
    /// ascendentes o descendentes, y el legajo (o sus últimos 6 dígitos).
    /// </summary>
    public static Result<Pin> Create(string? value, EmployeeNumber employeeNumber)
    {
        ArgumentNullException.ThrowIfNull(employeeNumber);

        if (value is null || value.Length != Length || !value.All(char.IsAsciiDigit))
        {
            return PinErrors.InvalidFormat;
        }

        if (IsTrivial(value, employeeNumber))
        {
            return PinErrors.Trivial;
        }

        return new Pin(value);
    }

    public override string ToString() => new('*', Length);

    private static bool IsTrivial(string value, EmployeeNumber employeeNumber) =>
        HasConstantStep(value, 0)
        || HasConstantStep(value, 1)
        || HasConstantStep(value, -1)
        || EndsWithPin(employeeNumber.Value, value);

    private static bool HasConstantStep(string value, int step)
    {
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] - value[i - 1] != step)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EndsWithPin(string employeeNumber, string pin) =>
        employeeNumber.Length >= Length && employeeNumber.EndsWith(pin, StringComparison.Ordinal);
}

public static class PinErrors
{
    public static readonly Error InvalidFormat = new("Pin.InvalidFormat", "El PIN debe tener exactamente 6 dígitos.");

    public static readonly Error Trivial = new(
        "Pin.Trivial",
        "El PIN es demasiado fácil: no uses dígitos repetidos, secuencias ni tu legajo.");

    public static readonly Error SameAsCurrent = new("Pin.SameAsCurrent", "El PIN nuevo tiene que ser distinto del actual.");
}
