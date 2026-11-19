using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Presence;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;

namespace BreakManager.Application.Tests.Fakes;

/// <summary>
/// Unidad de trabajo en memoria: los repositorios fake solo "publican" lo agregado cuando se guarda,
/// así los tests pueden verificar que algo NO se confirmó.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly List<Action> _pending = [];

    public int SaveCount { get; private set; }

    public void Enlist(Action commit) => _pending.Add(commit);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var commit in _pending)
        {
            commit();
        }

        _pending.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTokenService : ITokenService
{
    private int _counter;

    public string GenerateToken() => $"token-{++_counter}";

    public string GenerateEnrollmentCode() => $"CODE{++_counter}";

    public string Hash(string secret) => "h(" + secret + ")";
}

internal sealed class FakePinHasher : IPinHasher
{
    public string Hash(Pin pin) => "pin:" + pin.Value;

    public bool Verify(string pinHash, string candidate) => pinHash == "pin:" + candidate;
}

internal sealed class FakeSecurityEventLog(FakeUnitOfWork unitOfWork) : ISecurityEventLog
{
    private readonly List<SecurityEvent> _saved = [];

    public IReadOnlyList<SecurityEvent> Saved => _saved;

    public void Record(SecurityEvent securityEvent) => unitOfWork.Enlist(() => _saved.Add(securityEvent));

    public IEnumerable<SecurityEvent> OfType(SecurityEventType type) => _saved.Where(e => e.Type == type);
}

internal sealed class InMemoryOperatorRepository(FakeUnitOfWork unitOfWork) : IOperatorRepository
{
    private readonly List<Operator> _items = [];

    public IReadOnlyList<Operator> Items => _items;

    public Task<Operator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(o => o.Id == id));

    public Task<Operator?> GetByEmployeeNumberAsync(EmployeeNumber employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(o => o.EmployeeNumber == employeeNumber));

    public Task<bool> ExistsAsync(EmployeeNumber employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Any(o => o.EmployeeNumber == employeeNumber));

    public void Add(Operator op) => unitOfWork.Enlist(() => _items.Add(op));

    public void Seed(Operator op) => _items.Add(op);
}

internal sealed class InMemoryWorkstationRepository(FakeUnitOfWork unitOfWork) : IWorkstationRepository
{
    private readonly List<Workstation> _items = [];

    public IReadOnlyList<Workstation> Items => _items;

    public Task<Workstation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(w => w.Id == id));

    public Task<bool> NumberExistsAsync(int number, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Any(w => w.Number == number));

    public Task<Workstation?> GetByCredentialTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(w => w.Credentials.Any(c => c.TokenHash == tokenHash)));

    public void Add(Workstation workstation) => unitOfWork.Enlist(() => _items.Add(workstation));

    public void Seed(Workstation workstation) => _items.Add(workstation);
}

internal sealed class InMemoryEnrollmentCodeRepository(FakeUnitOfWork unitOfWork) : IEnrollmentCodeRepository
{
    private readonly List<EnrollmentCode> _items = [];

    public IReadOnlyList<EnrollmentCode> Items => _items;

    public Task<EnrollmentCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(c => c.CodeHash == codeHash));

    public Task<IReadOnlyList<EnrollmentCode>> ListPendingForWorkstationAsync(Guid workstationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EnrollmentCode>>(
            [.. _items.Where(c => c.WorkstationId == workstationId && c.RedeemedAt is null && c.InvalidatedAt is null)]);

    public void Add(EnrollmentCode code) => unitOfWork.Enlist(() => _items.Add(code));
}

internal sealed class InMemoryLoginTicketRepository(FakeUnitOfWork unitOfWork) : ILoginTicketRepository
{
    private readonly List<LoginTicket> _items = [];

    public IReadOnlyList<LoginTicket> Items => _items;

    public Task<LoginTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(t => t.TokenHash == tokenHash));

    public void Add(LoginTicket ticket) => unitOfWork.Enlist(() => _items.Add(ticket));
}

internal sealed class InMemoryPrivacyNoticeRepository(FakeUnitOfWork unitOfWork) : IPrivacyNoticeRepository
{
    private readonly List<PrivacyNotice> _notices = [];
    private readonly List<NoticeAcceptance> _acceptances = [];

    public IReadOnlyList<NoticeAcceptance> Acceptances => _acceptances;

    public Task<PrivacyNotice?> GetCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_notices.OrderByDescending(n => n.Version).FirstOrDefault());

    public Task<bool> HasAcceptedAsync(Guid operatorId, int version, CancellationToken cancellationToken) =>
        Task.FromResult(_acceptances.Any(a => a.OperatorId == operatorId && a.NoticeVersion == version));

    public void Add(PrivacyNotice notice) => unitOfWork.Enlist(() => _notices.Add(notice));

    public void AddAcceptance(NoticeAcceptance acceptance) => unitOfWork.Enlist(() => _acceptances.Add(acceptance));

    public void Seed(PrivacyNotice notice) => _notices.Add(notice);

    public void SeedAcceptance(NoticeAcceptance acceptance) => _acceptances.Add(acceptance);
}

internal sealed class InMemoryWorkstationSessionRepository(FakeUnitOfWork unitOfWork) : IWorkstationSessionRepository
{
    private readonly List<WorkstationSession> _items = [];

    public IReadOnlyList<WorkstationSession> Items => _items;

    public Task<WorkstationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(s => s.Id == id));

    public Task<WorkstationSession?> GetOpenForWorkstationAsync(Guid workstationId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(s => s.WorkstationId == workstationId && s.IsOpen));

    public Task<WorkstationSession?> GetOpenForOperatorAsync(Guid operatorId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(s => s.OperatorId == operatorId && s.IsOpen));

    public Task<WorkstationSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_items.SingleOrDefault(s => s.TokenHash == tokenHash));

    public void Add(WorkstationSession session) => unitOfWork.Enlist(() => _items.Add(session));
}
