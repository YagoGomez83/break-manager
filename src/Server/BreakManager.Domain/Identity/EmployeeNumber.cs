using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Legajo del operador: solo dígitos, de 1 a <see cref="MaxLength"/> caracteres.
/// </summary>
public sealed record EmployeeNumber
{
    public const int MaxLength = 20;

    private EmployeeNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<EmployeeNumber> Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length is 0 or > MaxLength || !trimmed.All(char.IsAsciiDigit))
        {
            return EmployeeNumberErrors.Invalid;
        }

        return new EmployeeNumber(trimmed);
    }

    public override string ToString() => Value;
}

public static class EmployeeNumberErrors
{
    public static readonly Error Invalid = new(
        "EmployeeNumber.Invalid",
        $"El legajo debe tener solo dígitos (máximo {EmployeeNumber.MaxLength}).");
}
