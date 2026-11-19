using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity;

// Puertos de persistencia del módulo Identity. Los cambios se confirman con IUnitOfWork.

public interface IOperatorRepository
{
    Task<Operator?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Operator?> GetByEmployeeNumberAsync(EmployeeNumber employeeNumber, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(EmployeeNumber employeeNumber, CancellationToken cancellationToken);

    void Add(Operator op);
}

public interface IWorkstationRepository
{
    Task<Workstation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NumberExistsAsync(int number, CancellationToken cancellationToken);

    /// <summary>Puesto cuya credencial (vigente o no) tiene ese hash de token.</summary>
    Task<Workstation?> GetByCredentialTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(Workstation workstation);
}

public interface IEnrollmentCodeRepository
{
    Task<EnrollmentCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken);

    /// <summary>Códigos sin canjear ni invalidar del puesto (para invalidarlos al generar uno nuevo).</summary>
    Task<IReadOnlyList<EnrollmentCode>> ListPendingForWorkstationAsync(Guid workstationId, CancellationToken cancellationToken);

    void Add(EnrollmentCode code);
}

public interface ILoginTicketRepository
{
    Task<LoginTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(LoginTicket ticket);
}

public interface IPrivacyNoticeRepository
{
    /// <summary>Versión vigente (la última publicada), o <c>null</c> si no hay ninguna.</summary>
    Task<PrivacyNotice?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<bool> HasAcceptedAsync(Guid operatorId, int version, CancellationToken cancellationToken);

    void Add(PrivacyNotice notice);

    void AddAcceptance(NoticeAcceptance acceptance);
}
