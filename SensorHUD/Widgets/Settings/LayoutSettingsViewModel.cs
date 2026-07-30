using System;
using System.Collections.Generic;
using SensorHUD.Core.Settings;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable state for layout direction and horizontal separation.
/// </summary>
public sealed class LayoutSettingsViewModel : ObservableObject
{
    private static readonly WidgetLayout[] LayoutChoices =
        Enum.GetValues<WidgetLayout>();

    private WidgetLayout _layout;
    private string _horizontalSeparator;

    internal LayoutSettingsViewModel(WidgetSettings settings)
    {
        _layout = settings.Layout;
        _horizontalSeparator = settings.HorizontalSeparator;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<WidgetLayout> Options => LayoutChoices;

    public WidgetLayout Layout
    {
        get => _layout;
        set => SetSetting(ref _layout, value);
    }

    public string HorizontalSeparator
    {
        get => _horizontalSeparator;
        set => SetSetting(ref _horizontalSeparator, value ?? string.Empty);
    }

    internal void ApplyTo(WidgetSettings settings)
    {
        settings.Layout = Layout;
        settings.HorizontalSeparator = HorizontalSeparator;
    }

    private void SetSetting<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
