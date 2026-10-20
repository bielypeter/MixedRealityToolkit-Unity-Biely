// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Base for interface -> wheelchair bridges: turns a normalized command into the chair's X/Y bytes and sends them
    /// to every registered <see cref="IWheelchairDriver"/> (real chair over BLE and/or the simulated chair).
    /// </summary>
    public abstract class WheelchairBridge : MonoBehaviour
    {
        public const int Neutral = 128;

        [Tooltip("Maximum offset from 128 forward/backward (keep low while testing on the real chair).")]
        [SerializeField, Range(0, 127)]
        private int maxDrive = 30;

        [Tooltip("Maximum offset from 128 for turning.")]
        [SerializeField, Range(0, 127)]
        private int maxTurn = 30;

        public int MaxDrive
        {
            get => maxDrive;
            set => maxDrive = Mathf.Clamp(value, 0, 127);
        }

        public int MaxTurn
        {
            get => maxTurn;
            set => maxTurn = Mathf.Clamp(value, 0, 127);
        }

        /// <summary>Last X byte sent (turn).</summary>
        public int LastX { get; private set; } = Neutral;

        /// <summary>Last Y byte sent (drive).</summary>
        public int LastY { get; private set; } = Neutral;

        /// <summary>Current command: x = turn (-1 left .. 1 right), y = drive (-1 back .. 1 forward).</summary>
        protected abstract Vector2 ReadCommand();

        private void Update()
        {
            Vector2 c = Vector2.ClampMagnitude(ReadCommand(), 1f);
            Send(Neutral + Mathf.RoundToInt(c.x * maxTurn), Neutral + Mathf.RoundToInt(c.y * maxDrive));
        }

        // Never leave the chair driving when the interface goes away.
        private void OnDisable()
        {
            Send(Neutral, Neutral);
        }

        private void Send(int x, int y)
        {
            LastX = Mathf.Clamp(x, 0, 255);
            LastY = Mathf.Clamp(y, 0, 255);
            WheelchairDrivers.Broadcast(LastX, LastY);
        }
    }
}
#pragma warning restore CS1591
