using System;
using System.Globalization;
using SensorHUD.Core.Settings;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml.Media;

namespace SensorHUD.Presentation;

/// <summary>
/// Converts validated core appearance values into UWP XAML types.
/// </summary>
internal static class XamlTextStyle
{
    public static FontFamily CreateFontFamily(string family)
    {
        try
        {
            return new FontFamily(family);
        }
        catch
        {
            return new FontFamily(SettingsDefaults.FontFamily);
        }
    }

    public static FontWeight ToFontWeight(WidgetFontWeight weight) =>
        weight switch
        {
            WidgetFontWeight.Light => FontWeights.Light,
            WidgetFontWeight.Normal => FontWeights.Normal,
            WidgetFontWeight.Bold => FontWeights.Bold,
            WidgetFontWeight.Black => FontWeights.Black,
            _ => FontWeights.SemiBold,
        };

    public static Color ParseColor(string value)
    {
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        return hex.Length == 8 &&
            uint.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out uint argb)
            ? Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb)
            : Colors.White;
    }

    /// <summary>
    /// Formats a XAML color using the portable ARGB representation stored in
    /// the core settings model.
    /// </summary>
    public static string FormatColor(Color color) =>
        FormattableString.Invariant(
            $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
}
