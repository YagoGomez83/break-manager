using BreakManager.Application.Identity;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Tests.Fakes;

internal sealed class FakeUserAccountService : IUserAccountService
{
    public Dictionary<Guid, UserAccountSummary> Accounts { get; } = [];

    public Dictionary<Guid, string> Passwords { get; } = [];

    public Guid Seed(string userName, WebRole role, Group? group)
    {
        var id = Guid.NewGuid();
        Accounts[id] = new UserAccountSummary(id, userName, role, group, Active: true, LockedOut: false);
        return id;
    }

    public Task<Result<Guid>> CreateAsync(NewUserAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        var id = Seed(account.UserName, account.Role, account.Group);
        Passwords[id] = account.TemporaryPassword;
        return Task.FromResult(Result.Success(id));
    }

    public Task<Result> UpdateRoleAsync(Guid userId, WebRole role, Group? group, CancellationToken cancellationToken)
    {
        Accounts[userId] = Accounts[userId] with { Role = role, Group = group };
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken)
    {
        Accounts[userId] = Accounts[userId] with { Active = active };
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken cancellationToken)
    {
        Passwords[userId] = temporaryPassword;
        return Task.FromResult(Result.Success());
    }

    public Task<UserAccountSummary?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.GetValueOrDefault(userId));

    public Task<IReadOnlyList<UserAccountSummary>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserAccountSummary>>([.. Accounts.Values]);
}
