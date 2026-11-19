using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

public sealed class GroupTests
{
    [Theory]
    [InlineData(Group.A, 0)]
    [InlineData(Group.B, 6)]
    [InlineData(Group.C, 12)]
    [InlineData(Group.D, 18)]
    public void ShiftStart_IsTheStartHourOfTheGroupShift(Group group, int expectedHour)
    {
        Assert.Equal(new TimeOnly(expectedHour, 0), group.ShiftStart());
    }
}
