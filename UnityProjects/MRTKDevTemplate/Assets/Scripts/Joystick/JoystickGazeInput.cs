// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using MixedReality.Toolkit.Input;
using UnityEngine;
using UnityEngine.UI;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Drives a <see cref="JoystickController"/> with eye gaze.
    /// </summary>
    /// <remarks>
    /// Dwell on the joystick to grab the knob; while held, the knob follows the gaze point on the joystick plane,
    /// so looking further from the center changes X/Y faster. Looking away from the joystick releases it.
    /// Uses MRTK's <see cref="GazeInteractor"/>, which is fed by the Pupil Labs Neon (or the input simulator).
    /// <para>
    /// Optional lock ring: a ring around the joystick (with a small gap). While the knob is held, moving the gaze
    /// out of the joystick into the ring and staying there for <see cref="lockDwell"/> locks the stick position
    /// (direction and speed) so it keeps driving while the user looks around. Grabbing the joystick again, or
    /// dwelling on the optional stop button, clears the lock.
    /// </para>
    /// <para>
    /// With <see cref="LockMode.ForwardWithSteering"/> the ring locks only the forward speed. Looking back at the
    /// joystick (a short dwell) then steers sideways while that speed is kept; looking deep into its backward half
    /// drops the lock and the stick is held normally again. The stop button also clears it.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(JoystickController))]
    [AddComponentMenu("MRTK/Examples/Joystick Gaze Input")]
    public class JoystickGazeInput : MonoBehaviour
    {
        public enum LockMode
        {
            /// <summary>The whole stick position (direction and speed) is locked; grabbing the joystick clears it.</summary>
            WholeStick,

            /// <summary>Only the forward speed is locked; the joystick then steers it sideways.</summary>
            ForwardWithSteering,
        }

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

        [Header("Lock ring (optional)")]
        [SerializeField]
        [Tooltip("Ring around the joystick, centered on it. Outer radius = half its rect, inner radius = outer - outline thickness. Empty = no locking.")]
        private RoundedShapeGraphic lockRing;

        [SerializeField]
        [Tooltip("Optional marker placed on the ring at the locked direction; shown only while locked.")]
        private RectTransform lockMarker;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze must stay in the ring, coming from the held joystick, to lock.")]
        private float lockDwell = 0.2f;

        [SerializeField]
        [Tooltip("What the ring locks: the whole stick, or only the forward speed with sideways steering afterwards.")]
        private LockMode lockMode = LockMode.WholeStick;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Forward lock: seconds on the joystick before it starts steering the lock.")]
        private float steerEngageDwell = 0.2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Forward lock: how far below the center (fraction of the knob travel) the gaze must go while steering to drop the lock.")]
        private float unlockBackDepth = 0.5f;

        [SerializeField]
        private Color ringIdleColor = new Color(1f, 1f, 1f, 0.12f);

        [SerializeField]
        private Color ringLockedColor = new Color(0.49f, 0.61f, 1f, 0.55f);

        [Header("Stop button (optional)")]
        [SerializeField]
        [Tooltip("Gaze target of the stop button; dwelling on it clears a lock.")]
        private RectTransform stopButton;

        [SerializeField]
        private Graphic stopHighlight;

        [SerializeField]
        [Min(0f)]
        private float stopDwell = 0.4f;

        [SerializeField]
        private Color stopIdleColor = new Color(0.13f, 0.14f, 0.16f, 0.18f);

        [SerializeField]
        [Tooltip("Stop button while something is locked (i.e. stopping is possible).")]
        private Color stopReadyColor = new Color(1f, 0.42f, 0.42f, 0.35f);

        [SerializeField]
        private Color stopActiveColor = new Color(1f, 0.42f, 0.42f, 0.9f);

        private JoystickController joystick;
        private RectTransform rectTransform;
        private Vector2 smoothedPoint;
        private float dwellTimer;
        private float outsideTimer;
        private float lockTimer;
        private float stopTimer;
        private bool stopConsumed;
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

        private void OnEnable()
        {
            lockTimer = stopTimer = 0f;
            stopConsumed = false;
            RefreshLockVisuals();
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
            Vector2 offset = hasPoint ? point - rectTransform.rect.center : Vector2.zero;
            float radius = joystick.BaseRadius;
            float distance = hasPoint ? offset.magnitude : float.MaxValue;

            UpdateStop();

            // A lock cleared from elsewhere (stop button) while steering it leaves nothing to hold.
            if (holding && !joystick.IsHeld && !joystick.IsSteering)
            {
                holding = false;
                dwellTimer = 0f;
            }

            if (!holding)
            {
                bool steerLock = lockMode == LockMode.ForwardWithSteering && joystick.IsLocked;
                float dwell = steerLock ? steerEngageDwell : engageDwell;
                if (distance <= radius && !joystick.IsHeld)
                {
                    dwellTimer += Time.deltaTime;
                    joystick.SetEngageProgress(dwell > 0f ? dwellTimer / dwell : 1f);
                    if (dwellTimer >= dwell)
                    {
                        holding = true;
                        dwellTimer = 0f;
                        outsideTimer = 0f;
                        lockTimer = 0f;
                        smoothedPoint = point;
                        if (steerLock)
                        {
                            joystick.BeginSteer(); // keeps the locked forward speed
                        }
                        else
                        {
                            joystick.BeginHold(); // also clears a lock
                        }
                        HoldAt(smoothedPoint);
                    }
                }
                else if (dwellTimer > 0f)
                {
                    dwellTimer = 0f;
                    joystick.SetEngageProgress(0f);
                }
                RefreshLockVisuals();
                return;
            }

            if (distance <= radius * (1f + releaseMargin))
            {
                outsideTimer = 0f;
                lockTimer = 0f;
                smoothedPoint = Vector2.Lerp(smoothedPoint, point, GazeRayUtility.SmoothingFactor(smoothing));
                HoldAt(smoothedPoint);
            }
            else if (IsInLockRing(distance, offset))
            {
                // The knob keeps its last position inside the joystick, so driving continues while the lock dwells.
                outsideTimer = 0f;
                lockTimer += Time.deltaTime;
                if (lockTimer >= lockDwell)
                {
                    TryLock();
                }
            }
            else
            {
                lockTimer = 0f;
                outsideTimer += Time.deltaTime;
                if (outsideTimer >= releaseDelay)
                {
                    Release();
                }
            }
            RefreshLockVisuals();
        }

        private void HoldAt(Vector2 point)
        {
            if (joystick.IsSteering)
            {
                if (joystick.LocalToStick(point).y > -unlockBackDepth)
                {
                    joystick.UpdateSteer(point);
                    return;
                }
                // Deep into reverse: drop the lock and hand the stick over to normal holding.
                joystick.BeginHold();
            }
            joystick.UpdateHold(point);
        }

        private bool IsInLockRing(float distance, Vector2 offset)
        {
            if (lockRing == null || !lockRing.isActiveAndEnabled)
            {
                return false;
            }
            if (lockRing.TopHalf && offset.y < 0f)
            {
                return false; // half ring: only its upper half locks
            }
            Rect ringRect = lockRing.rectTransform.rect;
            float outer = Mathf.Min(ringRect.width, ringRect.height) * 0.5f;
            float inner = Mathf.Max(0f, outer - lockRing.OutlineThickness);
            return distance >= inner && distance <= outer * (1f + releaseMargin);
        }

        private void TryLock()
        {
            Vector2 input = joystick.StickInput;
            if (lockMode == LockMode.ForwardWithSteering)
            {
                // Only the forward speed; while steering a lock, keep its speed rather than the steered one.
                input = new Vector2(0f, joystick.IsSteering ? joystick.LockedInput.y : Mathf.Max(0f, input.y));
            }
            lockTimer = 0f;
            if (input == Vector2.zero || joystick.VelocityFor(input) == Vector2.zero)
            {
                Release(); // stick in the dead zone (or, for a forward lock, not going forward): nothing to lock
                return;
            }

            holding = false;
            dwellTimer = 0f;
            joystick.Lock(input);
            joystick.SetEngageProgress(0f);

            if (lockMarker != null && lockRing != null)
            {
                Rect ringRect = lockRing.rectTransform.rect;
                float outer = Mathf.Min(ringRect.width, ringRect.height) * 0.5f;
                float middle = outer - lockRing.OutlineThickness * 0.5f;
                lockMarker.anchoredPosition = input.normalized * middle;
            }
        }

        private void UpdateStop()
        {
            if (stopButton == null)
            {
                return;
            }

            bool looking = GazeRayUtility.TryGetGazePoint(ref gazeInteractor, stopButton, out Vector2 stopPoint)
                && stopButton.rect.Contains(stopPoint);
            if (!looking)
            {
                stopTimer = 0f;
                stopConsumed = false;
            }
            else if (!stopConsumed)
            {
                stopTimer += Time.deltaTime;
                if (stopTimer >= stopDwell)
                {
                    stopConsumed = true; // look away before it can fire again
                    stopTimer = 0f;
                    joystick.Unlock();
                }
            }

            if (stopHighlight != null)
            {
                Color baseColor = joystick.IsLocked ? stopReadyColor : stopIdleColor;
                float progress = stopConsumed || stopDwell <= 0f ? 0f : Mathf.Clamp01(stopTimer / stopDwell);
                stopHighlight.color = Color.Lerp(baseColor, stopActiveColor, progress * 0.8f);
            }
        }

        private void RefreshLockVisuals()
        {
            if (lockRing != null)
            {
                float progress = holding && lockDwell > 0f ? Mathf.Clamp01(lockTimer / lockDwell) : 0f;
                lockRing.color = joystick != null && joystick.IsLocked
                    ? ringLockedColor
                    : Color.Lerp(ringIdleColor, ringLockedColor, progress);
            }

            if (lockMarker != null)
            {
                bool show = joystick != null && joystick.IsLocked;
                if (lockMarker.gameObject.activeSelf != show)
                {
                    lockMarker.gameObject.SetActive(show);
                }
            }
        }

        private void Release()
        {
            lockTimer = 0f;
            if (holding)
            {
                holding = false;
                dwellTimer = 0f;
                if (joystick.IsSteering)
                {
                    joystick.EndSteer(); // the lock goes on, straight ahead
                }
                else
                {
                    joystick.EndHold();
                }
                joystick.SetEngageProgress(0f);
            }
        }
    }
}
#pragma warning restore CS1591
