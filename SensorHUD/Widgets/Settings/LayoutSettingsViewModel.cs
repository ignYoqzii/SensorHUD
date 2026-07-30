using System;
using System.Collections.Generic;
using SensorHUD.Core.Settings;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable state for layout direction and horizontal separation.
/// </summary>
public sealed class LayoutSettingsViewModel : EditableSettingsViewModel
{
    private static readonly WidgetLayout[] DirectionChoices =
        Enum.GetValues<WidgetLayout>();

    private WidgetLayout _direction;
    private string _horizontalSeparator;

    internal LayoutSettingsViewModel(WidgetSettings settings)
    {
        _direction = settings.Layout.Direction;
        _horizontalSeparator = settings.Layout.HorizontalSeparator;
    }

    public IReadOnlyList<WidgetLayout> DirectionOptions => DirectionChoices;

    public WidgetLayout Direction
    {
        get => _direction;
        set => SetSetting(ref _direction, value);
    }

    public string HorizontalSeparator
    {
        get => _horizontalSeparator;
        set => SetSetting(ref _horizontalSeparator, value ?? string.Empty);
    }

    internal void ApplyTo(WidgetSettings settings)
    {
        settings.Layout = new LayoutSettings
        {
            Direction = Direction,
            HorizontalSeparator = HorizontalSeparator,
        };
    }
}
