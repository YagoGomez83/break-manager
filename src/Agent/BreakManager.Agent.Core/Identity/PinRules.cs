namespace BreakManager.Agent.Core.Identity;

/// <summary>
/// Copia local de las reglas del PIN, solo para avisar antes de enviar. La validación real la hace el servidor.
/// </summary>
public static class PinRules
{
    public const int Length = 6;

    /// <summary>Mensaje para el operador si el PIN no sirve, o <c>null</c> si está bien.</summary>
    public static string? Check(string? pin, string employeeNumber)
    {
        if (pin is null || pin.Length != Length || !pin.All(char.IsAsciiDigit))
        {
            return "El PIN tiene que tener exactamente 6 dígitos.";
        }

        var trivial = HasConstantStep(pin, 0) || HasConstantStep(pin, 1) || HasConstantStep(pin, -1)
            || (employeeNumber.Length >= Length && employeeNumber.EndsWith(pin, StringComparison.Ordinal));

        return trivial ? "El PIN es demasiado fácil: no uses dígitos repetidos, secuencias ni tu legajo." : null;
    }

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
}
