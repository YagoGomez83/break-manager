using BreakManager.Domain.Common;

namespace BreakManager.Application.Identity;

/// <summary>
/// Errores de los casos de uso de identidad. Los que ve el agente son genéricos a propósito:
/// no revelan si un legajo, un código o un token existen.
/// </summary>
public static class IdentityErrors
{
    public static readonly Error InvalidCredentials = new("Identity.InvalidCredentials", "Legajo o PIN incorrectos.");

    public static readonly Error OperatorLockedOut = new("Identity.OperatorLockedOut", "Legajo bloqueado. Consultá con tu supervisor.");

    public static readonly Error InvalidEnrollmentCode = new("Identity.InvalidEnrollmentCode", "El código de enrolamiento no es válido o venció.");

    public static readonly Error InvalidDeviceToken = new("Identity.InvalidDeviceToken", "El puesto no está autorizado.");

    public static readonly Error InvalidSession = new("Identity.InvalidSession", "La sesión no es válida. Volvé a ingresar.");

    public static readonly Error InvalidLoginTicket = new("Identity.InvalidLoginTicket", "El inicio de sesión no es válido o venció. Volvé a ingresar.");

    public static readonly Error NewPinRequired = new("Identity.NewPinRequired", "Tenés que elegir un PIN nuevo.");

    public static readonly Error WorkstationNotFound = new("Identity.WorkstationNotFound", "El puesto no existe.");

    public static readonly Error WorkstationNumberTaken = new("Identity.WorkstationNumberTaken", "Ya existe un puesto con ese número.");

    public static readonly Error OperatorNotFound = new("Identity.OperatorNotFound", "El operador no existe.");

    public static readonly Error EmployeeNumberTaken = new("Identity.EmployeeNumberTaken", "Ya existe un operador con ese legajo.");

    public static readonly Error UserNotFound = new("Identity.UserNotFound", "El usuario no existe.");

    public static readonly Error CannotDeactivateSelf = new("Identity.CannotDeactivateSelf", "No podés desactivar tu propio usuario.");

    public static readonly Error SupervisorGroupRequired = new(
        "Identity.SupervisorGroupRequired",
        "Un supervisor tiene que tener un grupo; los demás roles no llevan grupo.");
}
