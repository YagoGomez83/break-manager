namespace BreakManager.Agent.Core.Activity;

/// <summary>
/// Fuente de "pulsos de actividad" del puesto (ADR-0003). La implementación de Windows (Raw Input,
/// Fase 4) vive en Agent.Windows; la lógica de umbrales la consume desde Agent.Core.
/// El evento no transporta datos: no se registran coordenadas, botones ni teclas (ADR-0006).
/// </summary>
public interface IActivitySource
{
    event EventHandler? ActivityDetected;
}
