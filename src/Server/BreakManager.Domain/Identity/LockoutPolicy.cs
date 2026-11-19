namespace BreakManager.Domain.Identity;

/// <summary>
/// Reglas de bloqueo del operador. Los valores vienen de los parámetros configurables
/// (<c>OperatorLockout*</c>); el dominio no los conoce como constantes.
/// </summary>
public sealed record LockoutPolicy
{
    public LockoutPolicy(int attempts, TimeSpan duration, int lockoutsBeforeManualUnlock, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lockoutsBeforeManualUnlock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        Attempts = attempts;
        Duration = duration;
        LockoutsBeforeManualUnlock = lockoutsBeforeManualUnlock;
        Window = window;
    }

    /// <summary>Intentos fallidos seguidos que provocan un bloqueo temporal.</summary>
    public int Attempts { get; }

    /// <summary>Duración de un bloqueo temporal.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Bloqueos dentro de <see cref="Window"/> que dejan el legajo bloqueado hasta un desbloqueo manual.</summary>
    public int LockoutsBeforeManualUnlock { get; }

    /// <summary>Ventana en la que se cuentan los bloqueos.</summary>
    public TimeSpan Window { get; }
}
