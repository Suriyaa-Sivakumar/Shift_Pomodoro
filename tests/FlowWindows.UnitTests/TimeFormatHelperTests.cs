using System;
using FlowWindows.Core.Common;
using Xunit;

namespace FlowWindows.UnitTests;

public class TimeFormatHelperTests
{
    [Theory]
    [InlineData(60, 0, "60:00")]
    [InlineData(90, 0, "90:00")]
    [InlineData(120, 0, "120:00")]
    [InlineData(25, 0, "25:00")]
    [InlineData(5, 0, "05:00")]
    [InlineData(15, 0, "15:00")]
    [InlineData(0, 59, "00:59")]
    [InlineData(59, 59, "59:59")]
    [InlineData(89, 59, "89:59")]
    [InlineData(0, 0, "00:00")]
    public void FormatTimer_CorrectlyFormatsTotalMinutesAndSeconds(int minutes, int seconds, string expected)
    {
        var time = TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(seconds));
        var result = TimeFormatHelper.FormatTimer(time);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatTimer_HandlesNegativeTimeSpanGracefully()
    {
        var time = TimeSpan.FromSeconds(-10);
        var result = TimeFormatHelper.FormatTimer(time);
        Assert.Equal("00:00", result);
    }
}

