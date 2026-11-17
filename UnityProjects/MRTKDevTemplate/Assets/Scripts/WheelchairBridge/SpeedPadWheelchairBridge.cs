// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Drives the wheelchair from <see cref="GazeSpeedPad"/>: the selected speed level drives, steer arrows add turning,
    /// rotate arrows turn in place.
    /// </summary>
    [AddComponentMenu("MRTK/Examples/Speed Pad Wheelchair Bridge")]
    public class SpeedPadWheelchairBridge : WheelchairBridge
    {
        [SerializeField]
        private GazeSpeedPad pad;

        public GazeSpeedPad Pad
        {
            get => pad;
            set => pad = value;
        }

        protected override Vector2 ReadCommand()
        {
            if (pad == null || !pad.isActiveAndEnabled)
            {
                return Vector2.zero;
            }
            return pad.Command;
        }
    }
}
#pragma warning restore CS1591
