using Sysora.Core.History;

namespace Sysora.Tests.History;

public sealed class LocalDaysTests
{
    [Fact]
    public void OrdinaryDay_StartsAtMidnight()
    {
        var now = new DateTimeOffset(2026, 3, 2, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), LocalDays.Midnight(now, TimeZoneInfo.Utc));
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), LocalDays.Midnight(now, TimeZoneInfo.Utc, daysBefore: 1));
    }

    [Fact]
    public void DayAfterClocksGoBack_YesterdayStartsAtItsOwnMidnight()
    {
        // Monday 26 October 2026, 13:00 local (UTC+1); on Sunday the 25th, 3:00 summer time became 2:00.
        var now = new DateTimeOffset(2026, 10, 26, 13, 0, 0, TimeSpan.FromHours(1));

        var today = LocalDays.Midnight(now, TestData.CentralEurope);
        var yesterday = LocalDays.Midnight(now, TestData.CentralEurope, daysBefore: 1);

        Assert.Equal(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.FromHours(1)), today);
        Assert.Equal(TimeSpan.FromHours(1), today.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.FromHours(2)), yesterday);
        Assert.Equal(TimeSpan.FromHours(2), yesterday.Offset);
        Assert.Equal(TimeSpan.FromHours(25), today - yesterday);
    }

    [Fact]
    public void DayAfterClocksGoForward_YesterdayLasts23Hours()
    {
        // Monday 30 March 2026, 9:00 local (UTC+2); on Sunday the 29th, 2:00 became 3:00 summer time.
        var now = new DateTimeOffset(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(2));

        var today = LocalDays.Midnight(now, TestData.CentralEurope);
        var yesterday = LocalDays.Midnight(now, TestData.CentralEurope, daysBefore: 1);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.FromHours(1)), yesterday);
        Assert.Equal(TimeSpan.FromHours(23), today - yesterday);
    }

    [Fact]
    public void SeveralDaysBack_CrossesAClockChange()
    {
        var now = new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.FromHours(2)), LocalDays.Midnight(now, TestData.CentralEurope, daysBefore: 6));
    }
}
