// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using MixedReality.Toolkit.Input;
using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Drives a <see cref="JoystickController"/> with eye gaze.
    /// </summary>
    /// <remarks>
    /// Dwell on the joystick to grab the knob; while held, the knob follows the gaze point on the joystick plane,
    /// so looking further from the center changes X/Y faster. Looking away from the joystick releases it.
    /// Uses MRTK's <see cref="GazeInteractor"/>, which is fed by the Pupil Labs Neon (or the input simulator).
    /// </remarks>
    [RequireComponent(typeof(JoystickController))]
    [AddComponentMenu("MRTK/Examples/Joystick Gaze Input")]
    public class JoystickGazeInput : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Gaze interactor providing the eye ray. Found automatically when empty.")]
        private GazeInteractor gazeInteractor;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze must stay on the joystick before the knob is grabbed.")]
        private float engageDwell = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Tolerance beyond the rim (fraction of the base radius) before leaving the circle counts as leaving. Small, to absorb eye-tracker jitter.")]
        private float releaseMargin = 0.05f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze may leave the joystick (blinks, saccades) before the knob is released.")]
        private float releaseDelay = 0.25f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Smoothing time constant for the gaze point, to reduce eye-tracking jitter. 0 = raw.")]
        private float smoothing = 0.06f;

        private JoystickController joystick;
        private RectTransform rectTransform;
        private Vector2 smoothedPoint;
        private float dwellTimer;
        private float outsideTimer;
        private bool holding;

        public GazeInteractor GazeInteractor
        {
            get => gazeInteractor;
            set => gazeInteractor = value;
        }

        private void Awake()
        {
            joystick = GetComponent<JoystickController>();
            rectTransform = (RectTransform)transform;
        }

        private void OnDisable()
        {
            Release();
            dwellTimer = 0f;
            joystick.SetEngageProgress(0f);
        }

        private void Update()
        {
            bool hasPoint = GazeRayUtility.TryGetGazePoint(ref gazeInteractor, rectTransform, out Vector2 point);
            float radius = joystick.BaseRadius;
            float distance = hasPoint ? (point - rectTransform.rect.center).magnitude : float.MaxValue;

            if (!holding)
            {
                if (distance <= radius && !joystick.IsHeld)
                {
                    dwellTimer += Time.deltaTime;
                    joystick.SetEngageProgress(engageDwell > 0f ? dwellTimer / engageDwell : 1f);
                    if (dwellTimer >= engageDwell)
                    {
                        holding = true;
                        outsideTimer = 0f;
                        smoothedPoint = point;
                        joystick.BeginHold();
                        joystick.UpdateHold(smoothedPoint);
                    }
                }
                else if (dwellTimer > 0f)
                {
                    dwellTimer = 0f;
                    joystick.SetEngageProgress(0f);
                }
                return;
            }

            if (distance <= radius * (1f + releaseMargin))
            {
                outsideTimer = 0f;
                smoothedPoint = Vector2.Lerp(smoothedPoint, point, GazeRayUtility.SmoothingFactor(smoothing));
                joystick.UpdateHold(smoothedPoint);
            }
            else
            {
                outsideTimer += Time.deltaTime;
                if (outsideTimer >= releaseDelay)
                {
                    Release();
                }
            }
        }

        private void Release()
        {
            if (holding)
            {
                holding = false;
                dwellTimer = 0f;
                joystick.EndHold();
                joystick.SetEngageProgress(0f);
            }
        }
    }
}
#pragma warning restore CS1591
