// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// A gaze input whose rate of change (units per second) can be tuned at runtime, e.g. by <see cref="JoystickSpeedSlider"/>.
    /// </summary>
    public interface ISpeedAdjustable
    {
        float MaxSpeed { get; set; }
    }
}
#pragma warning restore CS1591
