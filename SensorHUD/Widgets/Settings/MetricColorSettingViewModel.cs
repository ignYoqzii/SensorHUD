using System;
using SensorHUD.Presentation;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable state for one metric color setting.
/// </summary>
public sealed class MetricColorSettingViewModel : ObservableObject
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

    public event EventHandler? Changed;

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
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public SolidColorBrush ColorBrush => _colorBrush;

    public string ColorText => XamlTextStyle.FormatColor(Color);
}
