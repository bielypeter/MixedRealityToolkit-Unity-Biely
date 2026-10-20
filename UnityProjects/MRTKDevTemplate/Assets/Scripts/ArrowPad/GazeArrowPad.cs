// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using System;
using MixedReality.Toolkit.Input;
using UnityEngine;
using UnityEngine.Events;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Gaze-operated arrows that change an X/Y value, plus turn buttons that change a rotation (degrees).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Look at an arrow (after a short dwell): the value moves in that direction while you keep looking.</item>
    /// <item>While an arrow is active its lock (confirmation) square appears; moving the gaze from the arrow straight into
    /// the square locks that direction without interrupting the movement. Leaving the arrow anywhere else hides the square.
    /// Only one arrow can be locked; dwelling on a locked square unlocks it.</item>
    /// <item>Look at the stop button in the middle: the locked movement stops.</item>
    /// </list>
    /// While an arrow is locked, looking at another arrow takes over for as long as you look at it.
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("MRTK/Examples/Gaze Arrow Pad")]
    public class GazeArrowPad : MonoBehaviour, ISpeedAdjustable
    {
        [Serializable]
        public class Arrow
        {
            [Tooltip("Gaze target area of this arrow.")]
            public RectTransform hitArea;

            [Tooltip("Graphic tinted with dwell progress and while active.")]
            public UnityEngine.UI.Graphic highlight;

            [Tooltip("Direction the value moves while this arrow is active.")]
            public Vector2 direction;

            [Tooltip("Turn direction while active: -1 = left (counter-clockwise), 1 = right, 0 = no turning.")]
            public float turn;

            [Tooltip("Gaze target of this arrow's lock square.")]
            public RectTransform lockArea;

            [Tooltip("Graphic of the lock square, tinted with dwell progress and while locked.")]
            public UnityEngine.UI.Graphic lockHighlight;

            [NonSerialized]
            public float reveal;

            [NonSerialized]
            public CanvasGroup lockGroup;
        }

        [Serializable]
        public class Vector2Event : UnityEvent<Vector2> { }

        [Header("Parts")]
        [SerializeField]
        private Arrow[] arrows = Array.Empty<Arrow>();

        [SerializeField]
        [Tooltip("Gaze target of the stop button.")]
        private RectTransform stopArea;

        [SerializeField]
        private UnityEngine.UI.Graphic stopHighlight;

        [SerializeField]
        private JoystickAxisIndicator xIndicator;

        [SerializeField]
        private JoystickAxisIndicator yIndicator;

        [SerializeField]
        private JoystickAxisIndicator rotationIndicator;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Units per second while an arrow is active or locked.")]
        private float maxSpeed = 5f;

        [SerializeField]
        private Vector2 value = Vector2.zero;

        [SerializeField]
        [Tooltip("Degrees per second while a turn button is active or locked.")]
        private float turnSpeed = 45f;

        [SerializeField]
        [Tooltip("Current rotation in degrees.")]
        private float rotation = 0f;

        [Header("Gaze")]
        [SerializeField]
        [Tooltip("Gaze interactor providing the eye ray. Found automatically when empty.")]
        private GazeInteractor gazeInteractor;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze must stay on an arrow before it starts moving the value.")]
        private float engageDwell = 0.4f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze must stay on the confirmation square, coming from its active arrow, to lock it. 0 = lock on entry.")]
        private float confirmDwell = 0f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze must stay on a locked square to unlock it.")]
        private float lockDwell = 0.35f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze must stay on the stop button to stop the locked movement.")]
        private float stopDwell = 0.4f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the gaze may leave the active arrow (blinks, saccades) before it stops.")]
        private float releaseDelay = 0.2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra seconds a lock square stays visible after its button is released. 0 = hide as soon as the gaze leaves the button.")]
        private float lockRevealDuration = 0f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Fade speed of the lock squares (alpha per second).")]
        private float lockFadeSpeed = 8f;

        [Header("Colors")]
        [SerializeField]
        private Color idleColor = new Color(0.13f, 0.14f, 0.16f, 0.18f);

        [SerializeField]
        private Color activeColor = new Color(0.49f, 0.61f, 1f, 0.75f);

        [SerializeField]
        [Tooltip("Arrow fill while it is locked but not being looked at.")]
        private Color lockedArrowColor = new Color(0.49f, 0.61f, 1f, 0.4f);

        [SerializeField]
        private Color lockedColor = new Color(0.49f, 0.61f, 1f, 0.9f);

        [SerializeField]
        [Tooltip("Stop button while something is locked (i.e. stopping is possible).")]
        private Color stopReadyColor = new Color(1f, 0.42f, 0.42f, 0.35f);

        [SerializeField]
        private Color stopActiveColor = new Color(1f, 0.42f, 0.42f, 0.9f);

        [Header("Events")]
        [SerializeField]
        private Vector2Event onValueChanged = new Vector2Event();

        [SerializeField]
        private FloatEvent onRotationChanged = new FloatEvent();

        [Serializable]
        public class FloatEvent : UnityEvent<float> { }

        public Vector2 Value
        {
            get => value;
            set => SetValue(value);
        }

        public float X => value.x;

        public float Y => value.y;

        public float MaxSpeed
        {
            get => maxSpeed;
            set => maxSpeed = value;
        }

        /// <summary>Current rotation in degrees.</summary>
        public float Rotation
        {
            get => rotation;
            set => SetRotation(value);
        }

        public float TurnSpeed
        {
            get => turnSpeed;
            set => turnSpeed = value;
        }

        public FloatEvent OnRotationChanged => onRotationChanged;

        /// <summary>Current rate of change of <see cref="Value"/> (units/s); zero when no arrow is active or locked.</summary>
        public Vector2 Velocity { get; private set; }

        /// <summary>Current rate of change of <see cref="Rotation"/> (degrees/s); zero when no turn arrow is active or locked.</summary>
        public float TurnRate { get; private set; }

        /// <summary>Index of the arrow the gaze is currently holding, or -1.</summary>
        public int ActiveArrow { get; private set; } = -1;

        /// <summary>Index of the locked arrow, or -1.</summary>
        public int LockedArrow { get; private set; } = -1;

        public Vector2Event OnValueChanged => onValueChanged;

        // Gaze targets are encoded as: arrow i = i, lock i = Count + i, stop = 2 * Count.
        private int Count => arrows.Length;
        private int StopTarget => 2 * Count;

        private int dwellTarget = -1;
        private float dwellTimer;
        private bool dwellConsumed;
        private float outsideTimer;
        private float confirmTimer;

        private void OnEnable()
        {
            ActiveArrow = LockedArrow = dwellTarget = -1;
            dwellTimer = outsideTimer = 0f;
            dwellConsumed = false;

            // Lock squares start hidden at runtime; in the editor they stay visible so the layout can be edited.
            foreach (Arrow arrow in arrows)
            {
                arrow.reveal = 0f;
                arrow.lockGroup = arrow.lockArea != null ? arrow.lockArea.GetComponent<CanvasGroup>() : null;
                SetLockAlpha(arrow, Application.isPlaying ? 0f : 1f);
            }

            RefreshVisuals();
            RefreshIndicators(Vector2.zero);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdateGaze(TargetUnderGaze());
            UpdateLockReveal();

            int moving = ActiveArrow >= 0 ? ActiveArrow : LockedArrow;
            Vector2 velocity = moving >= 0 ? arrows[moving].direction.normalized * maxSpeed : Vector2.zero;
            float turnRate = moving >= 0 ? Mathf.Clamp(arrows[moving].turn, -1f, 1f) * turnSpeed : 0f;
            Velocity = velocity;
            TurnRate = turnRate;

            if (velocity != Vector2.zero)
            {
                SetValue(value + velocity * Time.deltaTime, velocity);
            }
            if (turnRate != 0f)
            {
                SetRotation(rotation + turnRate * Time.deltaTime);
            }
            RefreshIndicators(velocity, turnRate);
            RefreshVisuals();
        }

        private void UpdateGaze(int target)
        {
            // Dual action: from an active arrow, moving into its confirmation square locks it. The arrow stays active
            // until the lock engages, so the movement continues without a gap.
            if (ActiveArrow >= 0 && target == Count + ActiveArrow)
            {
                confirmTimer += Time.deltaTime;
                if (confirmTimer >= confirmDwell)
                {
                    LockedArrow = ActiveArrow;
                    ActiveArrow = -1;
                    confirmTimer = 0f;
                    dwellTarget = target;
                    dwellTimer = 0f;
                    dwellConsumed = true; // don't let the same look unlock it again
                }
                return;
            }
            confirmTimer = 0f;

            // An arrow held by gaze stays active until the gaze has left it for releaseDelay.
            if (ActiveArrow >= 0)
            {
                if (target == ActiveArrow)
                {
                    outsideTimer = 0f;
                    return;
                }

                outsideTimer += Time.deltaTime;
                if (outsideTimer < releaseDelay)
                {
                    return;
                }
                ActiveArrow = -1;
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
                return; // lock / stop already fired; look away before it can fire again
            }

            dwellTimer += Time.deltaTime;
            if (dwellTimer >= DwellFor(target))
            {
                Trigger(target);
            }
        }

        private void Trigger(int target)
        {
            if (target < Count)
            {
                ActiveArrow = target;
                outsideTimer = 0f;
            }
            else if (target < StopTarget)
            {
                int arrow = target - Count;
                LockedArrow = LockedArrow == arrow ? -1 : arrow;
                dwellConsumed = true;
            }
            else
            {
                LockedArrow = -1;
                dwellConsumed = true;
            }
        }

        private float DwellFor(int target)
        {
            if (target < Count)
            {
                return engageDwell;
            }
            return target < StopTarget ? lockDwell : stopDwell; // lock squares are only dwell targets when unlocking
        }

        /// <summary>Stops the locked movement (same as looking at the stop button).</summary>
        public void Stop() => LockedArrow = -1;

        public void SetValue(Vector2 newValue) => SetValue(newValue, Vector2.zero);

        public void ResetValue() => SetValue(Vector2.zero);

        public void SetRotation(float degrees)
        {
            bool changed = degrees != rotation;
            rotation = degrees;
            RefreshIndicators(Vector2.zero, 0f);
            if (changed)
            {
                onRotationChanged.Invoke(rotation);
            }
        }

        private void SetValue(Vector2 newValue, Vector2 velocity)
        {
            bool changed = newValue != value;
            value = newValue;
            RefreshIndicators(velocity);
            if (changed)
            {
                onValueChanged.Invoke(value);
            }
        }

        private int TargetUnderGaze()
        {
            if (IsLookedAt(stopArea))
            {
                return StopTarget;
            }

            // Visible buttons win over lock squares, which win over the arrows' padded hit areas,
            // so the squares can sit flush against their arrows without stealing the arrow itself.
            for (int i = 0; i < Count; i++)
            {
                if (arrows[i].highlight != null && IsLookedAt(arrows[i].highlight.rectTransform))
                {
                    return i;
                }
            }
            for (int i = 0; i < Count; i++)
            {
                // The active arrow's square counts as soon as it appears, even while still fading in.
                if ((i == ActiveArrow || IsLockShown(arrows[i])) && IsLookedAt(arrows[i].lockArea))
                {
                    return Count + i;
                }
            }
            for (int i = 0; i < Count; i++)
            {
                if (IsLookedAt(arrows[i].hitArea))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Shows a button's lock square while the button is active, locked, or its lock is being looked at,
        /// and keeps it for <see cref="lockRevealDuration"/> afterwards so the gaze can move to it.
        /// </summary>
        private void UpdateLockReveal()
        {
            for (int i = 0; i < Count; i++)
            {
                Arrow arrow = arrows[i];
                bool keep = i == ActiveArrow || i == LockedArrow || dwellTarget == Count + i;
                arrow.reveal = keep ? lockRevealDuration : Mathf.Max(0f, arrow.reveal - Time.deltaTime);

                bool visible = keep || arrow.reveal > 0f;
                float current = arrow.lockGroup != null ? arrow.lockGroup.alpha : (visible ? 1f : 0f);
                float target = visible ? 1f : 0f;
                SetLockAlpha(arrow, Mathf.MoveTowards(current, target, lockFadeSpeed * Time.deltaTime));
            }
        }

        private static void SetLockAlpha(Arrow arrow, float alpha)
        {
            if (arrow.lockArea == null)
            {
                return;
            }
            if (arrow.lockGroup != null)
            {
                arrow.lockGroup.alpha = alpha;
            }
            else
            {
                arrow.lockArea.gameObject.SetActive(alpha > 0f);
            }
        }

        private static bool IsLockShown(Arrow arrow)
        {
            if (!Application.isPlaying)
            {
                return true;
            }
            return arrow.lockGroup != null ? arrow.lockGroup.alpha > 0.5f : arrow.lockArea != null && arrow.lockArea.gameObject.activeSelf;
        }

        private bool IsLookedAt(RectTransform area) =>
            area != null
            && GazeRayUtility.TryGetGazePoint(ref gazeInteractor, area, out Vector2 point)
            && area.rect.Contains(point);

        /// <summary>Dwell progress (0..1) towards triggering <paramref name="target"/>, for feedback.</summary>
        private float Progress(int target)
        {
            if (target != dwellTarget || dwellConsumed || ActiveArrow >= 0)
            {
                return 0f;
            }
            float dwell = DwellFor(target);
            return dwell > 0f ? Mathf.Clamp01(dwellTimer / dwell) : 1f;
        }

        private void RefreshVisuals()
        {
            for (int i = 0; i < Count; i++)
            {
                Arrow arrow = arrows[i];
                if (arrow.highlight != null)
                {
                    Color baseColor = i == LockedArrow ? lockedArrowColor : idleColor;
                    arrow.highlight.color = i == ActiveArrow
                        ? activeColor
                        : Color.Lerp(baseColor, activeColor, Progress(i) * 0.6f);
                }
                if (arrow.lockHighlight != null)
                {
                    arrow.lockHighlight.color = i == LockedArrow
                        ? Color.Lerp(lockedColor, idleColor, Progress(Count + i) * 0.6f) // dwelling again previews the unlock
                        : Color.Lerp(idleColor, lockedColor, Progress(Count + i) * 0.6f);
                }
            }

            if (stopHighlight != null)
            {
                Color baseColor = LockedArrow >= 0 ? stopReadyColor : idleColor;
                stopHighlight.color = Color.Lerp(baseColor, stopActiveColor, Progress(StopTarget) * 0.8f);
            }
        }

        private void RefreshIndicators(Vector2 velocity) => RefreshIndicators(velocity, 0f);

        private void RefreshIndicators(Vector2 velocity, float turnRate)
        {
            if (rotationIndicator != null)
            {
                rotationIndicator.Display(rotation, turnRate / Mathf.Max(0.0001f, Mathf.Abs(turnSpeed)));
            }

            float max = Mathf.Max(0.0001f, Mathf.Abs(maxSpeed));
            if (xIndicator != null)
            {
                xIndicator.Display(value.x, velocity.x / max);
            }
            if (yIndicator != null)
            {
                yIndicator.Display(value.y, velocity.y / max);
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
                    RefreshVisuals();
                    RefreshIndicators(Vector2.zero);
                }
            };
        }
#endif
    }
}
#pragma warning restore CS1591
