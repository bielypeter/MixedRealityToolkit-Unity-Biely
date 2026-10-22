// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Drives the wheelchair from <see cref="GazeArrowPad"/>: Up/Down drive, the turn arrows turn.
    /// A wheelchair can't move sideways, so the Left/Right arrows turn as well.
    /// </summary>
    [AddComponentMenu("MRTK/Examples/Arrow Pad Wheelchair Bridge")]
    public class ArrowPadWheelchairBridge : WheelchairBridge
    {
        [SerializeField]
        private GazeArrowPad pad;

        public GazeArrowPad Pad
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

            float drive = pad.Velocity.y / Mathf.Max(0.0001f, pad.MaxSpeed);
            float turn = pad.TurnRate != 0f
                ? pad.TurnRate / Mathf.Max(0.0001f, pad.TurnSpeed)
                : pad.Velocity.x / Mathf.Max(0.0001f, pad.MaxSpeed);

            return new Vector2(turn, drive);
        }
    }
}
#pragma warning restore CS1591
