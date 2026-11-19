namespace BreakManager.Domain.Common;

/// <summary>
/// Marca un hecho ocurrido en el dominio. Los módulos se comunican mediante estos eventos
/// sin acceder a las entidades internas de otro módulo.
/// </summary>
#pragma warning disable CA1040 // Interfaz marcadora intencional.
public interface IDomainEvent;
#pragma warning restore CA1040
