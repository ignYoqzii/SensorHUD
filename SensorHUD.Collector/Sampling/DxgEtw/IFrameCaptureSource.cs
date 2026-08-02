namespace SensorHUD.Collector.Sampling.DxgEtw;

/// <summary>
/// Supplies process-identified presentation windows independently of their
/// ETW source.
/// </summary>
internal interface IFrameCaptureSource
{
    FrameCaptureSubsystemHealth CaptureHealth { get; }

    FrameCaptureWindow Capture();
}
