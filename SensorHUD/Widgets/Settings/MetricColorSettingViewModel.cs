using SensorHUD.Presentation;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable state for one metric color setting.
/// </summary>
public sealed class MetricColorSettingViewModel : EditableSettingsViewModel
{
    private readonly SolidColorBrush _colorBrush;
    private Color _color;

    internal MetricColorSettingViewModel(
        string label,
        string description,
        string automationName,
        Color color)
    {
        Label = label;
        Description = description;
        AutomationName = automationName;
        _color = color;
        _colorBrush = new SolidColorBrush(color);
    }

    public string Label { get; }

    public string Description { get; }

    public string AutomationName { get; }

    public Color Color
    {
        get => _color;
        set
        {
            if (!SetProperty(ref _color, value))
            {
                return;
            }

            _colorBrush.Color = value;
            Notify(nameof(ColorText));
            RaiseChanged();
        }
    }

    public SolidColorBrush ColorBrush => _colorBrush;

    /// <summary>
    /// Gets or sets the color as RGB or ARGB hexadecimal text.
    /// Invalid input is discarded without changing the current color.
    /// </summary>
    public string ColorText
    {
        get => XamlTextStyle.FormatColor(Color);
        set
        {
            if (XamlTextStyle.TryParseColor(value, out Color color))
            {
                Color = color;
            }
            else
            {
                Notify(nameof(ColorText));
            }
        }
    }
}
