// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Drives the wheelchair from <see cref="JoystickController"/>: stick X turns, stick Y drives.
    /// </summary>
    [AddComponentMenu("MRTK/Examples/Joystick Wheelchair Bridge")]
    public class JoystickWheelchairBridge : WheelchairBridge
    {
        [SerializeField]
        private JoystickController joystick;

        public JoystickController Joystick
        {
            get => joystick;
            set => joystick = value;
        }

        protected override Vector2 ReadCommand()
        {
            if (joystick == null || !joystick.isActiveAndEnabled)
            {
                return Vector2.zero;
            }

            // Velocity is the rate (zero in the dead zone and when released), not the accumulated Value.
            return joystick.Velocity / Mathf.Max(0.0001f, joystick.MaxSpeed);
        }
    }
}
#pragma warning restore CS1591
