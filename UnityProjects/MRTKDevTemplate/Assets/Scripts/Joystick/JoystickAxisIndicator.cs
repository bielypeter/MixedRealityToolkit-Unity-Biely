// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using TMPro;
using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Read-out card for one joystick axis: shows the current value and a centered bar
    /// that fills left or right with the current rate of change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("MRTK/Examples/Joystick Axis Indicator")]
    public class JoystickAxisIndicator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Text that displays the axis value.")]
        private TMP_Text valueText;

        [SerializeField]
        [Tooltip("Fill of the rate bar. Anchored inside a full-width track; the indicator moves its anchors.")]
        private RectTransform rateFill;

        [SerializeField]
        [Tooltip("Numeric format of the displayed value.")]
        private string valueFormat = "+0.00;-0.00;0.00";

        /// <summary>
        /// Updates the read-out.
        /// </summary>
        /// <param name="value">Current axis value.</param>
        /// <param name="normalizedRate">Current rate of change, from -1 (full speed negative) to 1 (full speed positive).</param>
        public void Display(float value, float normalizedRate)
        {
            if (valueText != null)
            {
                // Monospaced digits so the number doesn't jitter while it changes.
                valueText.text = $"<mspace=0.56em>{value.ToString(valueFormat)}</mspace>";
            }

            if (rateFill != null)
            {
                float rate = Mathf.Clamp(normalizedRate, -1f, 1f);
                float min = rate < 0f ? 0.5f + rate * 0.5f : 0.5f;
                float max = rate > 0f ? 0.5f + rate * 0.5f : 0.5f;
                rateFill.anchorMin = new Vector2(min, 0f);
                rateFill.anchorMax = new Vector2(max, 1f);
                rateFill.offsetMin = Vector2.zero;
                rateFill.offsetMax = Vector2.zero;
            }
        }
    }
}
#pragma warning restore CS1591
