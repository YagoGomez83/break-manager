namespace BreakManager.Application.Common;

/// <summary>
/// Otro request cambió los mismos datos al mismo tiempo (p. ej. dos usos del mismo ticket, o dos sesiones
/// abiertas a la vez en un puesto). No se guardó nada; el cliente puede reintentar.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Conflicto de concurrencia: los datos cambiaron mientras se guardaban.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
