using System;
using System.Globalization;
using SensorHUD.Core.Settings;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
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

    /// <summary>
    /// Converts a durable horizontal alignment to its XAML equivalent.
    /// </summary>
    public static TextAlignment ToTextAlignment(
        WidgetHorizontalAlignment alignment) => alignment switch
        {
            WidgetHorizontalAlignment.Center => TextAlignment.Center,
            WidgetHorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };

    /// <summary>
    /// Converts a durable vertical alignment to its XAML equivalent.
    /// </summary>
    public static VerticalAlignment ToVerticalAlignment(
        WidgetVerticalAlignment alignment) => alignment switch
        {
            WidgetVerticalAlignment.Center => VerticalAlignment.Center,
            WidgetVerticalAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Top,
        };

    public static Color ParseColor(string value)
    {
        return TryParseColor(value, out Color color)
            ? color
            : Colors.White;
    }

    /// <summary>
    /// Tries to parse an RGB or ARGB hexadecimal color.
    /// </summary>
    public static bool TryParseColor(string? value, out Color color)
    {
        string hex = value?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length == 8 &&
            uint.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out uint argb))
        {
            color = Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb);
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>
    /// Formats a XAML color using the portable ARGB representation stored in
    /// the core settings model.
    /// </summary>
    public static string FormatColor(Color color) =>
        FormattableString.Invariant(
            $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
}
