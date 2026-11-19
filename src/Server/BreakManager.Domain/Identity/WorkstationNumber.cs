namespace BreakManager.Domain.Identity;

/// <summary>
/// Numeración de los puestos físicos de la sala (02-dominio.md §1: del 1 al 20).
/// </summary>
public static class WorkstationNumber
{
    public const int Min = 1;
    public const int Max = 20;

    public static bool IsValid(int number) => number is >= Min and <= Max;
}
