using System;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FlowWindows_App;

public static class ThemeHelper
{
    public static Brush GetBrush(string key, string fallbackHex = "#2DB88F")
    {
        try
        {
            if (Application.Current?.Resources != null)
            {
                if (Application.Current.Resources.TryGetValue(key, out var val) && val is Brush b)
                {
                    return b;
                }
            }
        }
        catch { }

        // Provide crisp, high-contrast defaults based on key name
        if (key.Contains("TextPrimary", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(ParseColor("#FFFFFF"));
        }
        if (key.Contains("TextSecondary", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(ParseColor("#CBD5E1"));
        }
        if (key.Contains("TextMuted", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(ParseColor("#94A3B8"));
        }

        return new SolidColorBrush(ParseColor(fallbackHex));
    }

    public static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return Color.FromArgb(255, r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }

        return Color.FromArgb(255, 45, 184, 143);
    }

    public static void ApplyTheme(FrameworkElement? element, FlowWindows.Core.Enums.ThemeMode mode)
    {
        if (element == null) return;
        element.RequestedTheme = mode switch
        {
            FlowWindows.Core.Enums.ThemeMode.Dark => ElementTheme.Dark,
            FlowWindows.Core.Enums.ThemeMode.Light => ElementTheme.Light,
            _ => ElementTheme.Default
        };
    }
}

