// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// One dual-gaze row of the <see cref="MainMenuController"/>: an option button and its confirmation button.
    /// Confirming loads <see cref="SceneName"/>, or invokes <see cref="OnConfirmed"/> when no scene is set.
    /// </summary>
    [AddComponentMenu("MRTK/Examples/Menu Row")]
    public class MenuRow : MonoBehaviour
    {
        [Tooltip("Looked at to reveal the confirmation. Should not use gaze dwell itself.")]
        [SerializeField]
        private StatefulInteractable option;

        [Tooltip("Revealed while the option is looked at; selects the row (Use Gaze Dwell on).")]
        [SerializeField]
        private StatefulInteractable confirm;

        [Tooltip("Text of the option button.")]
        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        private string labelText = "Option";

        [Tooltip("Scene to load on confirm (must be in Build Settings). Empty = only On Confirmed is invoked.")]
        [SerializeField]
        private string sceneName;

        [SerializeField]
        private UnityEvent onConfirmed = new UnityEvent();

        public StatefulInteractable Option => option;

        public StatefulInteractable Confirm => confirm;

        public string SceneName => sceneName;

        public UnityEvent OnConfirmed => onConfirmed;

        /// <summary>Seconds left before the confirmation hides; managed by <see cref="MainMenuController"/>.</summary>
        public float Reveal { get; set; }

        public bool IsEngaged =>
            (option != null && option.isHovered) ||
            (confirm != null && confirm.isActiveAndEnabled && confirm.isHovered);

        public string LabelText
        {
            get => labelText;
            set
            {
                labelText = value;
                ApplyLabel();
            }
        }

        public void SetConfirmVisible(bool visible)
        {
            if (confirm != null && confirm.gameObject.activeSelf != visible)
            {
                confirm.gameObject.SetActive(visible);
            }
        }

        public void SetInteractable(bool value)
        {
            if (option != null)
            {
                option.enabled = value;
            }
            if (confirm != null)
            {
                confirm.enabled = value;
            }
        }

        private void OnValidate() => ApplyLabel();

        private void ApplyLabel()
        {
            if (label != null && label.text != labelText)
            {
                label.text = labelText;
            }
        }
    }
}
#pragma warning restore CS1591
