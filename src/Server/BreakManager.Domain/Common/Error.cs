namespace BreakManager.Domain.Common;

/// <summary>
/// Error de negocio esperable. <see cref="Code"/> es estable (para lógica y tests);
/// <see cref="Message"/> es el texto para el usuario, en español.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "La spec de la Fase 1 nombra el tipo Error; la solución es solo C#.")]
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
