using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class WorkstationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_IsInServiceAndNotEnrolled()
    {
        var workstation = NewWorkstation();

        Assert.Equal(7, workstation.Number);
        Assert.Equal(ServiceStatus.InService, workstation.ServiceStatus);
        Assert.False(workstation.Revoked);
        Assert.Null(workstation.ActiveCredential);
    }

    [Theory]
    [InlineData(0, 1, 1, "PC-07")]
    [InlineData(21, 1, 1, "PC-07")]
    [InlineData(7, 0, 1, "PC-07")]
    [InlineData(7, 1, 0, "PC-07")]
    [InlineData(7, 1, 1, " ")]
    public void Create_WithInvalidData_Fails(int number, int row, int column, string hostname)
    {
        var result = Workstation.Create(Guid.NewGuid(), number, row, column, hostname);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RejectsAHostnameLongerThanTheMaximum()
    {
        var result = Workstation.Create(Guid.NewGuid(), 7, 2, 2, new string('a', Workstation.MaxHostnameLength + 1));

        Assert.Equal(WorkstationErrors.HostnameInvalid, result.Error);
    }

    [Fact]
    public void Enroll_IssuesAnActiveCredential()
    {
        var workstation = NewWorkstation();
        var credentialId = Guid.NewGuid();

        workstation.Enroll(credentialId, "token-hash", T0);

        Assert.NotNull(workstation.ActiveCredential);
        Assert.Equal(credentialId, workstation.ActiveCredential.Id);
        Assert.Equal("token-hash", workstation.ActiveCredential.TokenHash);
        Assert.Equal(T0, workstation.ActiveCredential.IssuedAt);
    }

    [Fact]
    public void Enroll_AgainRevokesThePreviousCredential()
    {
        var workstation = NewWorkstation();
        var first = workstation.Enroll(Guid.NewGuid(), "first", T0);

        var second = workstation.Enroll(Guid.NewGuid(), "second", T0.AddHours(1));

        Assert.Equal(T0.AddHours(1), first.RevokedAt);
        Assert.Same(second, workstation.ActiveCredential);
        Assert.Equal(2, workstation.Credentials.Count);
    }

    [Fact]
    public void Enroll_AfterRevocation_ReactivatesTheWorkstation()
    {
        var workstation = NewWorkstation();
        workstation.Enroll(Guid.NewGuid(), "first", T0);
        workstation.Revoke(T0.AddMinutes(1));

        workstation.Enroll(Guid.NewGuid(), "second", T0.AddMinutes(2));

        Assert.False(workstation.Revoked);
        Assert.NotNull(workstation.ActiveCredential);
    }

    [Fact]
    public void Revoke_MarksTheWorkstationRevoked_AndRevokesTheCredential()
    {
        var workstation = NewWorkstation();
        var credential = workstation.Enroll(Guid.NewGuid(), "hash", T0);

        workstation.Revoke(T0.AddMinutes(5));

        Assert.True(workstation.Revoked);
        Assert.Equal(T0.AddMinutes(5), credential.RevokedAt);
        Assert.Null(workstation.ActiveCredential);
    }

    [Fact]
    public void Update_ChangesLocationAndHostname()
    {
        var workstation = NewWorkstation();

        var result = workstation.Update(row: 3, column: 4, hostname: " PC-NUEVA ");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, workstation.Row);
        Assert.Equal(4, workstation.Column);
        Assert.Equal("PC-NUEVA", workstation.Hostname);
    }

    [Fact]
    public void RecordSeen_FirstTime_StoresTimeAndIp_WithoutIpChange()
    {
        var credential = NewWorkstation().Enroll(Guid.NewGuid(), "hash", T0);

        var seen = credential.RecordSeen(T0, "10.0.0.7");

        Assert.True(seen.Modified);
        Assert.False(seen.IpChanged);
        Assert.Equal(T0, credential.LastSeenAt);
        Assert.Equal("10.0.0.7", credential.LastSeenIp);
    }

    [Fact]
    public void RecordSeen_WithinAMinuteFromTheSameIp_DoesNotWrite()
    {
        var credential = NewWorkstation().Enroll(Guid.NewGuid(), "hash", T0);
        credential.RecordSeen(T0, "10.0.0.7");

        var seen = credential.RecordSeen(T0.AddSeconds(59), "10.0.0.7");

        Assert.False(seen.Modified);
        Assert.Equal(T0, credential.LastSeenAt);
    }

    [Fact]
    public void RecordSeen_AfterAMinute_UpdatesTheTime()
    {
        var credential = NewWorkstation().Enroll(Guid.NewGuid(), "hash", T0);
        credential.RecordSeen(T0, "10.0.0.7");

        var seen = credential.RecordSeen(T0.AddMinutes(1), "10.0.0.7");

        Assert.True(seen.Modified);
        Assert.Equal(T0.AddMinutes(1), credential.LastSeenAt);
    }

    [Fact]
    public void RecordSeen_FromAnotherIp_ReportsTheChange_EvenWithinAMinute()
    {
        var credential = NewWorkstation().Enroll(Guid.NewGuid(), "hash", T0);
        credential.RecordSeen(T0, "10.0.0.7");

        var seen = credential.RecordSeen(T0.AddSeconds(10), "10.0.0.99");

        Assert.True(seen.Modified);
        Assert.True(seen.IpChanged);
        Assert.Equal("10.0.0.7", seen.PreviousIp);
        Assert.Equal("10.0.0.99", credential.LastSeenIp);
        Assert.Equal(T0.AddSeconds(10), credential.LastSeenAt);
    }

    private static Workstation NewWorkstation() => Workstation.Create(Guid.NewGuid(), 7, 2, 2, "PC-07").Value;
}
