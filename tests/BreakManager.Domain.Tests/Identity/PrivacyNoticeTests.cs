using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class PrivacyNoticeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void Publish_First_IsVersionOne()
    {
        var result = PrivacyNotice.Publish(Guid.NewGuid(), currentVersion: null, "  Texto del aviso  ", T0, Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal("Texto del aviso", result.Value.Text);
        Assert.Equal(T0, result.Value.PublishedAt);
        Assert.Equal(Admin, result.Value.PublishedByUserId);
    }

    [Fact]
    public void Publish_Next_IncrementsTheCurrentVersion()
    {
        var result = PrivacyNotice.Publish(Guid.NewGuid(), currentVersion: 3, "Texto nuevo", T0, Admin);

        Assert.Equal(4, result.Value.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Publish_WithoutText_Fails(string text)
    {
        var result = PrivacyNotice.Publish(Guid.NewGuid(), null, text, T0, Admin);

        Assert.Equal(PrivacyNoticeErrors.TextRequired, result.Error);
    }

    [Fact]
    public void Publish_WithTextTooLong_Fails()
    {
        var result = PrivacyNotice.Publish(Guid.NewGuid(), null, new string('a', PrivacyNotice.MaxTextLength + 1), T0, Admin);

        Assert.Equal(PrivacyNoticeErrors.TextTooLong, result.Error);
    }

    [Fact]
    public void Accept_RecordsOperatorVersionTimeAndWorkstation()
    {
        var notice = PrivacyNotice.Publish(Guid.NewGuid(), null, "Aviso", T0, Admin).Value;
        var operatorId = Guid.NewGuid();
        var workstationId = Guid.NewGuid();

        var acceptance = notice.Accept(Guid.NewGuid(), operatorId, workstationId, T0.AddDays(1));

        Assert.Equal(operatorId, acceptance.OperatorId);
        Assert.Equal(notice.Version, acceptance.NoticeVersion);
        Assert.Equal(workstationId, acceptance.WorkstationId);
        Assert.Equal(T0.AddDays(1), acceptance.AcceptedAt);
    }
}
