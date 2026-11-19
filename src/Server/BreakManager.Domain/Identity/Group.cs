namespace BreakManager.Domain.Identity;

/// <summary>
/// Grupo de operadores asignado a un turno fijo de 6 h: A (00–06), B (06–12), C (12–18), D (18–00).
/// </summary>
public enum Group
{
    A,
    B,
    C,
    D,
}

public static class GroupExtensions
{
    private static readonly TimeSpan ShiftLength = TimeSpan.FromHours(6);

    /// <summary>
    /// Hora de inicio del turno del grupo, en la zona operativa.
    /// </summary>
    public static TimeOnly ShiftStart(this Group group) => group switch
    {
        Group.A or Group.B or Group.C or Group.D => TimeOnly.FromTimeSpan(ShiftLength * (int)group),
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Grupo desconocido."),
    };
}
