using BreakManager.Application.Identity;
using BreakManager.Application.Presence;
using BreakManager.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BreakManager.Application.Tests.Fakes;

/// <summary>
/// Dependencias en memoria compartidas por los tests de handlers de Identity y Presence.
/// </summary>
internal sealed class IdentityTestContext
{
    public static readonly DateTimeOffset Start = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    public IdentityTestContext()
    {
        UnitOfWork = new FakeUnitOfWork();
        SecurityEvents = new FakeSecurityEventLog(UnitOfWork);
        Operators = new InMemoryOperatorRepository(UnitOfWork);
        Workstations = new InMemoryWorkstationRepository(UnitOfWork);
        EnrollmentCodes = new InMemoryEnrollmentCodeRepository(UnitOfWork);
        LoginTickets = new InMemoryLoginTicketRepository(UnitOfWork);
        Notices = new InMemoryPrivacyNoticeRepository(UnitOfWork);
        SessionRepository = new InMemoryWorkstationSessionRepository(UnitOfWork);
        Sessions = new WorkstationSessionService(SessionRepository, Tokens, Time, ParametersOptions);
    }

    public FakeTimeProvider Time { get; } = new(Start);

    public IdentityParameters Parameters { get; } = new();

    public IOptions<IdentityParameters> ParametersOptions => Options.Create(Parameters);

    public FakeUnitOfWork UnitOfWork { get; }

    public FakeTokenService Tokens { get; } = new();

    public FakePinHasher PinHasher { get; } = new();

    public FakeSecurityEventLog SecurityEvents { get; }

    public InMemoryOperatorRepository Operators { get; }

    public InMemoryWorkstationRepository Workstations { get; }

    public InMemoryEnrollmentCodeRepository EnrollmentCodes { get; }

    public InMemoryLoginTicketRepository LoginTickets { get; }

    public InMemoryPrivacyNoticeRepository Notices { get; }

    public InMemoryWorkstationSessionRepository SessionRepository { get; }

    public WorkstationSessionService Sessions { get; }

    public static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    public DateTimeOffset Now => Time.GetUtcNow();

    public Workstation SeedWorkstation(int number = 7)
    {
        var workstation = Workstation.Create(Guid.CreateVersion7(), number, 2, 2, $"PC-{number:00}").Value;
        Workstations.Seed(workstation);
        return workstation;
    }

    /// <summary>Puesto enrolado con el token en claro <paramref name="deviceToken"/>.</summary>
    public (Workstation Workstation, DeviceCredential Credential) SeedEnrolledWorkstation(int number = 7, string? deviceToken = null)
    {
        var workstation = SeedWorkstation(number);
        var credential = workstation.Enroll(Guid.CreateVersion7(), Tokens.Hash(deviceToken ?? $"device-{number}"), Now);
        return (workstation, credential);
    }

    /// <summary>
    /// Operador con <paramref name="pin"/> como PIN: temporal si <paramref name="mustChangePin"/>, definitivo si no.
    /// </summary>
    public Operator SeedOperator(string employeeNumber = "20481", string pin = "482913", bool mustChangePin = false)
    {
        var number = EmployeeNumber.Create(employeeNumber).Value;
        var initial = mustChangePin ? pin : (pin == "918273" ? "736251" : "918273");
        var op = Operator.Create(
            Guid.CreateVersion7(), number, "Operador", Group.C, null, PinHasher.Hash(Pin.Create(initial, number).Value)).Value;

        if (!mustChangePin)
        {
            op.ChangePin(Pin.Create(pin, number).Value, PinHasher);
        }

        Operators.Seed(op);
        return op;
    }

    public PrivacyNotice SeedNotice(string text = "Aviso de prueba")
    {
        var current = Notices.GetCurrentAsync(CancellationToken.None).Result;
        var notice = PrivacyNotice.Publish(Guid.CreateVersion7(), current?.Version, text, Now, Guid.Empty).Value;
        Notices.Seed(notice);
        return notice;
    }

    public void SeedAcceptance(Operator op, PrivacyNotice notice, Guid workstationId) =>
        Notices.SeedAcceptance(notice.Accept(Guid.CreateVersion7(), op.Id, workstationId, Now));
}
