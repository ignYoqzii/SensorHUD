namespace SensorHUD.Collector.Sampling.Frames;

/// <summary>
/// ETW identifiers, time windows, and process exclusions for frame capture.
/// </summary>
internal static class FrameCaptureDefaults
{
    public const int DxgKernelPresentEventId = 0x00B8;
    public static readonly Guid DxgKernelProvider =
        new("802EC45A-1E99-4B83-9920-87C98277BA9D");
    public static readonly TimeSpan CalculationWindow =
        TimeSpan.FromSeconds(2);
    public static readonly TimeSpan RetentionWindow =
        TimeSpan.FromSeconds(6);

    public static readonly HashSet<string> ExcludedProcessNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "GameBar",
            "GameBarFTServer",
            "dwm",
            "TextInputHost",
            "ShellExperienceHost",
        };
}
