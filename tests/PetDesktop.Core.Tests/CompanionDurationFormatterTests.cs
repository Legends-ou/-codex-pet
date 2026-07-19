using PetDesktop.Core.Progress;

namespace PetDesktop.Core.Tests;

public sealed class CompanionDurationFormatterTests
{
    [Theory]
    [InlineData(0, "0 分钟")]
    [InlineData(59, "0 分钟")]
    [InlineData(3660, "1 小时 1 分钟")]
    [InlineData(34566120, "1 年 1 个月 5 天 1 小时 42 分钟")]
    public void FormatUsesCompactChineseCalendarLikeUnits(long seconds, string expected) =>
        Assert.Equal(expected, CompanionDurationFormatter.Format(seconds));
}
