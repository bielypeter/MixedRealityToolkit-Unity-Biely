// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using System;
using MixedReality.Toolkit.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Gaze-operated wheelchair pad with cruise-control speed levels.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Speed button (top): a short dwell opens the speed options (+2 +1 above, -1 -2 below). Dwelling on an
    /// option sets that drive speed, which stays until Stop or another option is picked. The menu closes after a pick
    /// or when the gaze has been away from it for a moment.</item>
    /// <item>Stop (bottom): dwelling sets the speed to zero.</item>
    /// <item>Steer arrows (left / right): while looked at, turning is added to the current drive, so the chair curves
    /// while moving (or turns slowly in place when stopped).</item>
    /// <item>Rotate arrows (top corners): while looked at, driving pauses and the chair rotates in place; the set speed
    /// resumes when the gaze leaves.</item>
    /// </list>
    /// The pad sends nothing itself; <see cref="SpeedPadWheelchairBridge"/> reads <see cref="Command"/>.
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("MRTK/Examples/Gaze Speed Pad")]
    public class GazeSpeedPad : MonoBehaviour
    {
        [Serializable]
        public class Button
        {
            [Tooltip("Gaze target area of this button.")]
            public RectTransform hitArea;

            [Tooltip("Graphic tinted with dwell progress and while active.")]
            public Graphic highlight;
        }

        [Serializable]
        public class SpeedOption
        {
            [Tooltip("Gaze target area of this option.")]
            public RectTransform hitArea;

            [Tooltip("Graphic tinted with dwell progress and while this speed is selected.")]
            public Graphic highlight;

            [Tooltip("Drive speed set by this option: -1 = full reverse .. 1 = full forward.")]
            [Range(-1f, 1f)]
            public float speed;

            [Tooltip("Shown on the speed button while this option is selected.")]
            public string label;
        }

        [Header("Parts")]
        [SerializeField]
        private Button speedButton = new Button();

        [SerializeField]
        [Tooltip("Optional label on the speed button showing the selected level.")]
        private TMP_Text speedLabel;

        [SerializeField]
        [Tooltip("Parent of the option buttons; shown only while the speed menu is open.")]
        private GameObject optionsRoot;

        [SerializeField]
        private SpeedOption[] options = Array.Empty<SpeedOption>();

        [SerializeField]
        private Button stopButton = new Button();

        [SerializeField]
        private Button steerLeft = new Button();

        [SerializeField]
        private Button steerRight = new Button();

        [SerializeField]
        private Button rotateLeft = new Button();

        [SerializeField]
        private Button rotateRight = new Button();

        [Header("Movement")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Turn added to the drive while a steer arrow is looked at during driving.")]
        private float steerAmount = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Turn while a steer arrow is looked at and the chair is stopped.")]
        private float steerInPlaceAmount = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Turn while a rotate arrow is looked at (driving pauses).")]
        private float rotateAmount = 1f;

        [SerializeField]
        [Tooltip("Speed button label while stopped.")]
        private string stoppedLabel = "0";

        [Header("Gaze")]
        [SerializeField]
        [Tooltip("Gaze interactor providing the eye ray. Found automatically when empty.")]
        private GazeInteractor gazeInteractor;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds on the speed button before the speed options open.")]
        private float speedOpenDwell = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds on a speed option before it is selected.")]
        private float optionDwell = 0.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds on the stop button before the chair stops.")]
        private float stopDwell = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds on a steer arrow before it starts steering.")]
        private float steerDwell = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds on a rotate arrow before it starts rotating.")]
        private float rotateDwell = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the gaze may leave a held steer/rotate arrow (blinks, saccades) before it releases.")]
        private float releaseDelay = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the gaze may be away from the speed button and its options before the menu closes.")]
        private float menuCloseDelay = 0.6f;

        [Header("Colors")]
        [SerializeField]
        private Color idleColor = new Color(0.13f, 0.14f, 0.16f, 0.18f);

        [SerializeField]
        private Color activeColor = new Color(0.49f, 0.61f, 1f, 0.75f);

        [SerializeField]
        [Tooltip("Speed button while driving, and the selected option.")]
        private Color selectedColor = new Color(0.49f, 0.61f, 1f, 0.4f);

        [SerializeField]
        [Tooltip("Stop button while driving (i.e. stopping is possible).")]
        private Color stopReadyColor = new Color(1f, 0.42f, 0.42f, 0.35f);

        [SerializeField]
        private Color stopActiveColor = new Color(1f, 0.42f, 0.42f, 0.9f);

        // Gaze targets: fixed buttons first, then option i = OptionBase + i.
        private const int SpeedTarget = 0;
        private const int StopTarget = 1;
        private const int SteerLeftTarget = 2;
        private const int SteerRightTarget = 3;
        private const int RotateLeftTarget = 4;
        private const int RotateRightTarget = 5;
        private const int OptionBase = 6;

        /// <summary>Current command: x = turn (-1 left .. 1 right), y = drive (-1 back .. 1 forward).</summary>
        public Vector2 Command { get; private set; }

        /// <summary>Selected drive speed (-1 .. 1), kept until <see cref="Stop"/>.</summary>
        public float Drive { get; private set; }

        /// <summary>Index of the selected speed option, or -1 when stopped.</summary>
        public int SpeedLevel { get; private set; } = -1;

        /// <summary>Whether the speed options are open.</summary>
        public bool MenuOpen { get; private set; }

        /// <summary>Steer/rotate target currently held by the gaze, or -1.</summary>
        private int held = -1;

        private int dwellTarget = -1;
        private float dwellTimer;
        private bool dwellConsumed;
        private float outsideTimer;
        private float menuAwayTimer;

        private void OnEnable()
        {
            Stop();
            held = dwellTarget = -1;
            dwellTimer = outsideTimer = menuAwayTimer = 0f;
            dwellConsumed = false;
            Command = Vector2.zero;
            RefreshOptionsVisible();
            RefreshVisuals();
        }

        private void OnDisable()
        {
            Command = Vector2.zero;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            int target = TargetUnderGaze();
            UpdateMenu(target);
            UpdateGaze(target);
            Command = ComputeCommand();
            RefreshOptionsVisible();
            RefreshVisuals();
        }

        /// <summary>Stops the chair: clears the selected speed and closes the menu.</summary>
        public void Stop()
        {
            Drive = 0f;
            SpeedLevel = -1;
            MenuOpen = false;
        }

        /// <summary>Selects a speed option, as if it had been dwelled on.</summary>
        public void SelectSpeed(int option)
        {
            if (option < 0 || option >= options.Length)
            {
                return;
            }
            SpeedLevel = option;
            Drive = Mathf.Clamp(options[option].speed, -1f, 1f);
            MenuOpen = false;
        }

        private Vector2 ComputeCommand()
        {
            switch (held)
            {
                case RotateLeftTarget:
                    return new Vector2(-rotateAmount, 0f);
                case RotateRightTarget:
                    return new Vector2(rotateAmount, 0f);
                case SteerLeftTarget:
                case SteerRightTarget:
                    float steer = Drive == 0f ? steerInPlaceAmount : steerAmount;
                    return new Vector2(held == SteerLeftTarget ? -steer : steer, Drive);
                default:
                    return new Vector2(0f, Drive);
            }
        }

        private void UpdateMenu(int target)
        {
            if (!MenuOpen)
            {
                menuAwayTimer = 0f;
                return;
            }

            if (target == SpeedTarget || target >= OptionBase)
            {
                menuAwayTimer = 0f;
                return;
            }

            menuAwayTimer += Time.deltaTime;
            if (menuAwayTimer >= menuCloseDelay)
            {
                MenuOpen = false;
                menuAwayTimer = 0f;
            }
        }

        private void UpdateGaze(int target)
        {
            // A held steer/rotate arrow stays active until the gaze has left it for releaseDelay.
            if (held >= 0)
            {
                if (target == held)
                {
                    outsideTimer = 0f;
                    return;
                }

                outsideTimer += Time.deltaTime;
                if (outsideTimer < releaseDelay)
                {
                    return;
                }
                held = -1;
                dwellTarget = -1;
            }

            if (target < 0 || target != dwellTarget)
            {
                dwellTarget = target;
                dwellTimer = 0f;
                dwellConsumed = false;
                return;
            }

            if (dwellConsumed)
            {
                return; // already fired; look away before it can fire again
            }

            dwellTimer += Time.deltaTime;
            if (dwellTimer >= DwellFor(target))
            {
                Trigger(target);
            }
        }

        private void Trigger(int target)
        {
            switch (target)
            {
                case SpeedTarget:
                    MenuOpen = true;
                    menuAwayTimer = 0f;
                    dwellConsumed = true;
                    break;
                case StopTarget:
                    Stop();
                    dwellConsumed = true;
                    break;
                case SteerLeftTarget:
                case SteerRightTarget:
                case RotateLeftTarget:
                case RotateRightTarget:
                    held = target;
                    outsideTimer = 0f;
                    break;
                default:
                    SelectSpeed(target - OptionBase);
                    dwellConsumed = true;
                    break;
            }
        }

        private float DwellFor(int target)
        {
            switch (target)
            {
                case SpeedTarget:
                    return speedOpenDwell;
                case StopTarget:
                    return stopDwell;
                case SteerLeftTarget:
                case SteerRightTarget:
                    return steerDwell;
                case RotateLeftTarget:
                case RotateRightTarget:
                    return rotateDwell;
                default:
                    return optionDwell;
            }
        }

        private int TargetUnderGaze()
        {
            // Options sit between the other buttons, so they win while the menu is open.
            if (MenuOpen)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (IsLookedAt(options[i].hitArea))
                    {
                        return OptionBase + i;
                    }
                }
            }

            if (IsLookedAt(stopButton.hitArea))
            {
                return StopTarget;
            }
            if (IsLookedAt(speedButton.hitArea))
            {
                return SpeedTarget;
            }
            if (IsLookedAt(rotateLeft.hitArea))
            {
                return RotateLeftTarget;
            }
            if (IsLookedAt(rotateRight.hitArea))
            {
                return RotateRightTarget;
            }
            if (IsLookedAt(steerLeft.hitArea))
            {
                return SteerLeftTarget;
            }
            if (IsLookedAt(steerRight.hitArea))
            {
                return SteerRightTarget;
            }
            return -1;
        }

        private bool IsLookedAt(RectTransform area) =>
            area != null
            && area.gameObject.activeInHierarchy
            && GazeRayUtility.TryGetGazePoint(ref gazeInteractor, area, out Vector2 point)
            && area.rect.Contains(point);

        /// <summary>Dwell progress (0..1) towards triggering <paramref name="target"/>, for feedback.</summary>
        private float Progress(int target)
        {
            if (target != dwellTarget || dwellConsumed || held >= 0)
            {
                return 0f;
            }
            float dwell = DwellFor(target);
            return dwell > 0f ? Mathf.Clamp01(dwellTimer / dwell) : 1f;
        }

        private void RefreshOptionsVisible()
        {
            if (optionsRoot == null)
            {
                return;
            }
            // In the editor the options stay visible so the layout can be edited.
            bool visible = !Application.isPlaying || MenuOpen;
            if (optionsRoot.activeSelf != visible)
            {
                optionsRoot.SetActive(visible);
            }
        }

        private void RefreshVisuals()
        {
            if (speedButton.highlight != null)
            {
                Color baseColor = SpeedLevel >= 0 ? selectedColor : idleColor;
                speedButton.highlight.color = MenuOpen
                    ? activeColor
                    : Color.Lerp(baseColor, activeColor, Progress(SpeedTarget) * 0.6f);
            }

            if (speedLabel != null)
            {
                string text = SpeedLevel >= 0 ? options[SpeedLevel].label : stoppedLabel;
                if (speedLabel.text != text)
                {
                    speedLabel.text = text;
                }
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].highlight != null)
                {
                    Color baseColor = i == SpeedLevel ? selectedColor : idleColor;
                    options[i].highlight.color = Color.Lerp(baseColor, activeColor, Progress(OptionBase + i) * 0.8f);
                }
            }

            if (stopButton.highlight != null)
            {
                Color baseColor = Drive != 0f ? stopReadyColor : idleColor;
                stopButton.highlight.color = Color.Lerp(baseColor, stopActiveColor, Progress(StopTarget) * 0.8f);
            }

            RefreshHoldButton(steerLeft, SteerLeftTarget);
            RefreshHoldButton(steerRight, SteerRightTarget);
            RefreshHoldButton(rotateLeft, RotateLeftTarget);
            RefreshHoldButton(rotateRight, RotateRightTarget);
        }

        private void RefreshHoldButton(Button button, int target)
        {
            if (button.highlight != null)
            {
                button.highlight.color = held == target
                    ? activeColor
                    : Color.Lerp(idleColor, activeColor, Progress(target) * 0.6f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Deferred: changing graphics directly inside OnValidate triggers editor warnings.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    RefreshOptionsVisible();
                    RefreshVisuals();
                }
            };
        }
#endif
    }
}
#pragma warning restore CS1591
