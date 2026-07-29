using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Owns the modal metric-editor surface, its motion, focus restoration, and
/// category-specific editor content.
/// </summary>
public sealed partial class MetricEditorPanel : UserControl
{
    private Control? _returnFocusTarget;
    private bool _restoreFocusAfterAnimation;

    /// <summary>
    /// Initializes an empty metric editor that remains hidden until
    /// <see cref="Open"/> is called.
    /// </summary>
    public MetricEditorPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised after the editor becomes visible.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Raised after the editor has closed and released its category.
    /// </summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Opens the editor for one global or device-specific category.
    /// </summary>
    internal void Open(
        MetricCategoryViewModel category,
        Control returnFocusTarget)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(returnFocusTarget);

        _returnFocusTarget = returnFocusTarget;
        TitleText.Text = $"{category.Name} Metrics";
        DescriptionText.Text = category.Description;
        DescriptionText.Visibility = category.DescriptionVisibility;
        MetricItems.ItemsSource = category.Metrics;

        Visibility = Visibility.Visible;
        CloseStoryboard.Stop();
        OpenStoryboard.Begin();
        _ = CloseButton.Focus(FocusState.Programmatic);
        Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Closes immediately for page lifecycle changes such as reset, rebuild,
    /// or unload.
    /// </summary>
    internal void CloseImmediately() =>
        Close(restoreFocus: false, animate: false);

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Close(restoreFocus: true, animate: true);

    private void Overlay_KeyDown(
        object sender,
        KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Close(restoreFocus: true, animate: true);
            e.Handled = true;
        }
    }

    private void Close(bool restoreFocus, bool animate)
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        if (animate)
        {
            _restoreFocusAfterAnimation = restoreFocus;
            OpenStoryboard.Stop();
            CloseStoryboard.Begin();
            return;
        }

        OpenStoryboard.Stop();
        CloseStoryboard.Stop();
        _restoreFocusAfterAnimation = false;
        CompleteClose(restoreFocus);
    }

    private void CloseStoryboard_Completed(object sender, object e)
    {
        CloseStoryboard.Stop();
        CompleteClose(_restoreFocusAfterAnimation);
        _restoreFocusAfterAnimation = false;
    }

    private void CompleteClose(bool restoreFocus)
    {
        Visibility = Visibility.Collapsed;
        MetricItems.ItemsSource = null;
        TitleText.Text = string.Empty;
        DescriptionText.Text = string.Empty;
        DescriptionText.Visibility = Visibility.Collapsed;

        if (restoreFocus)
        {
            _ = _returnFocusTarget?.Focus(FocusState.Programmatic);
        }

        _returnFocusTarget = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
