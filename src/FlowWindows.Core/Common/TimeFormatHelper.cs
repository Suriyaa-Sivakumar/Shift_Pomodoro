using System;

namespace FlowWindows.Core.Common;

public static class TimeFormatHelper
{
    /// <summary>
    /// Formats a TimeSpan for timer display. Always displays total minutes (e.g. 60:00, 90:00, 25:00)
    /// without wrapping around modulo 60.
    /// </summary>
    public static string FormatTimer(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        int totalMinutes = (int)Math.Floor(time.TotalMinutes);
        int seconds = time.Seconds;
        return $"{totalMinutes:D2}:{seconds:D2}";
    }
}

