using System;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Shared change contract for editable settings presented through XAML.
/// </summary>
public abstract class EditableSettingsViewModel : ObservableObject
{
    public event EventHandler? Changed;

    protected bool SetSetting<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value))
        {
            return false;
        }

        RaiseChanged();
        return true;
    }

    protected void RaiseChanged() =>
        Changed?.Invoke(this, EventArgs.Empty);
}
