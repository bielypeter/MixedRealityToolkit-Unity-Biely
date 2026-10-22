// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// A circular uGUI joystick that integrates an X/Y value over time.
    /// </summary>
    /// <remarks>
    /// The further the knob is dragged from the center, the faster X and Y change.
    /// Driven by eye gaze through <see cref="JoystickGazeInput"/>; mouse and XR rays also work through the UI input module.
    /// Put this on the joystick base (a raycast-target graphic); the knob should be a child that is not a raycast target.
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("MRTK/Examples/Joystick Controller")]
    public class JoystickController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ISpeedAdjustable
    {
        [Serializable]
        public class Vector2Event : UnityEvent<Vector2> { }

        [Header("Parts")]
        [SerializeField]
        [Tooltip("The movable knob. Must be a child of this object.")]
        private RectTransform knob;

        [SerializeField]
        [Tooltip("Optional graphic tinted while the knob is held.")]
        private UnityEngine.UI.Graphic knobHighlight;

        [SerializeField]
        private JoystickAxisIndicator xIndicator;

        [SerializeField]
        private JoystickAxisIndicator yIndicator;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("How far the knob center can travel from the middle, in canvas units. 0 = fit inside the base automatically.")]
        [Min(0f)]
        private float travelRadius = 0f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Fraction of the travel radius around the center that produces no change.")]
        private float deadZone = 0.1f;

        [SerializeField]
        [Tooltip("Units per second at the edge of the circle.")]
        private float maxSpeed = 5f;

        [SerializeField]
        [Range(1f, 4f)]
        [Tooltip("Response curve exponent. 1 = linear, 2 = fine control near the center, ramping up towards the edge.")]
        private float speedExponent = 2f;

        [SerializeField]
        [Tooltip("Time (seconds) for the knob to spring back to the center after release.")]
        [Min(0f)]
        private float returnTime = 0.08f;

        [Header("Value")]
        [SerializeField]
        private Vector2 value = Vector2.zero;

        [SerializeField]
        private bool clampValue = false;

        [SerializeField]
        private Vector2 minValue = new Vector2(-100f, -100f);

        [SerializeField]
        private Vector2 maxValue = new Vector2(100f, 100f);

        [Header("Colors")]
        [SerializeField]
        private Color idleColor = new Color(0.91f, 0.92f, 0.95f, 1f);

        [SerializeField]
        private Color activeColor = new Color(0.49f, 0.61f, 1f, 1f);

        [Header("Events")]
        [SerializeField]
        [Tooltip("Raised every frame the value changes.")]
        private Vector2Event onValueChanged = new Vector2Event();

        /// <summary>Current X/Y value.</summary>
        public Vector2 Value
        {
            get => value;
            set => SetValue(value);
        }

        public float X => value.x;

        public float Y => value.y;

        /// <summary>Knob position normalized to the unit circle (before dead zone and curve).</summary>
        public Vector2 StickInput { get; private set; }

        /// <summary>Current rate of change in units per second.</summary>
        public Vector2 Velocity { get; private set; }

        public bool IsHeld { get; private set; }

        public Vector2Event OnValueChanged => onValueChanged;

        public float MaxSpeed
        {
            get => maxSpeed;
            set => maxSpeed = value;
        }

        private RectTransform rectTransform;
        private Vector2 knobVelocity;
        private float engageProgress;

        private float TravelRadius
        {
            get
            {
                if (travelRadius > 0f || knob == null)
                {
                    return travelRadius;
                }
                Rect baseRect = rectTransform.rect;
                float baseRadius = Mathf.Min(baseRect.width, baseRect.height) * 0.5f;
                float knobRadius = Mathf.Min(knob.rect.width, knob.rect.height) * 0.5f;
                return Mathf.Max(1f, baseRadius - knobRadius);
            }
        }

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        private void OnEnable()
        {
            rectTransform = (RectTransform)transform;
            IsHeld = false;
            StickInput = Vector2.zero;
            Velocity = Vector2.zero;
            if (knob != null)
            {
                knob.anchoredPosition = Vector2.zero;
            }
            ApplyHighlight();
            RefreshIndicators();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!IsHeld && knob != null && knob.anchoredPosition != Vector2.zero)
            {
                knob.anchoredPosition = returnTime > 0f
                    ? Vector2.SmoothDamp(knob.anchoredPosition, Vector2.zero, ref knobVelocity, returnTime)
                    : Vector2.zero;
                if (knob.anchoredPosition.sqrMagnitude < 0.0001f)
                {
                    knob.anchoredPosition = Vector2.zero;
                }
            }

            Velocity = ComputeVelocity(StickInput);
            if (Velocity != Vector2.zero)
            {
                SetValue(value + Velocity * Time.deltaTime);
            }
            else
            {
                RefreshIndicators();
            }
        }

        /// <summary>Sets the value (clamped if enabled), updates the indicators and raises <see cref="OnValueChanged"/>.</summary>
        public void SetValue(Vector2 newValue)
        {
            if (clampValue)
            {
                newValue = Vector2.Max(minValue, Vector2.Min(maxValue, newValue));
            }

            bool changed = newValue != value;
            value = newValue;
            RefreshIndicators();

            if (changed)
            {
                onValueChanged.Invoke(value);
            }
        }

        /// <summary>Resets X and Y to zero.</summary>
        public void ResetValue() => SetValue(Vector2.zero);

        /// <summary>Radius of the joystick base in local canvas units.</summary>
        public float BaseRadius
        {
            get
            {
                Rect baseRect = ((RectTransform)transform).rect;
                return Mathf.Min(baseRect.width, baseRect.height) * 0.5f;
            }
        }

        /// <summary>Grabs the knob from an external input source (e.g. gaze).</summary>
        public void BeginHold()
        {
            IsHeld = true;
            knobVelocity = Vector2.zero;
            engageProgress = 0f;
            ApplyHighlight();
        }

        /// <summary>Moves the held knob towards a point given in the base's local space.</summary>
        public void UpdateHold(Vector2 localPoint)
        {
            if (IsHeld)
            {
                MoveKnob(localPoint);
            }
        }

        /// <summary>Releases the knob; it springs back to the center and X/Y stop changing.</summary>
        public void EndHold()
        {
            IsHeld = false;
            StickInput = Vector2.zero;
            ApplyHighlight();
        }

        /// <summary>Feedback while an input source is about to engage (0 = idle, 1 = engaged).</summary>
        public void SetEngageProgress(float progress)
        {
            engageProgress = Mathf.Clamp01(progress);
            ApplyHighlight();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            BeginHold();
            if (TryGetLocalPoint(eventData, out Vector2 local))
            {
                MoveKnob(local);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsHeld && TryGetLocalPoint(eventData, out Vector2 local))
            {
                MoveKnob(local);
            }
        }

        public void OnPointerUp(PointerEventData eventData) => EndHold();

        private void MoveKnob(Vector2 local)
        {
            // The base pivot may not be centered; measure from the rect center.
            Vector2 offset = local - rectTransform.rect.center;
            float radius = TravelRadius;
            Vector2 clamped = Vector2.ClampMagnitude(offset, radius);

            if (knob != null)
            {
                knob.anchoredPosition = clamped;
            }
            StickInput = clamped / radius;
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
        {
            // XR rays and pokes give a world-space hit on the canvas; use it when it is on this joystick.
            RaycastResult hit = eventData.pointerCurrentRaycast;
            if (hit.isValid && hit.gameObject != null && hit.gameObject.transform.IsChildOf(transform))
            {
                local = rectTransform.InverseTransformPoint(hit.worldPosition);
                return true;
            }

            // Otherwise (mouse, or dragging past the edge of the base) project the screen position onto the canvas plane.
            Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, cam, out local);
        }

        private Vector2 ComputeVelocity(Vector2 input)
        {
            float magnitude = input.magnitude;
            if (magnitude <= deadZone || magnitude <= 0f)
            {
                return Vector2.zero;
            }

            // Remap [deadZone, 1] -> [0, 1] and shape it, so speed grows with distance from the center.
            float t = Mathf.InverseLerp(deadZone, 1f, Mathf.Min(magnitude, 1f));
            float speed = maxSpeed * Mathf.Pow(t, speedExponent);
            return input / magnitude * speed;
        }

        private void ApplyHighlight()
        {
            if (knobHighlight != null)
            {
                knobHighlight.color = IsHeld ? activeColor : Color.Lerp(idleColor, activeColor, engageProgress * 0.6f);
            }
        }

        private void RefreshIndicators()
        {
            float max = Mathf.Max(0.0001f, Mathf.Abs(maxSpeed));
            if (xIndicator != null)
            {
                xIndicator.Display(value.x, Velocity.x / max);
            }
            if (yIndicator != null)
            {
                yIndicator.Display(value.y, Velocity.y / max);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep the read-outs in sync when the value is edited in the inspector, also outside Play mode.
            // Deferred: changing graphics and RectTransforms directly inside OnValidate triggers editor warnings.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }
                rectTransform = transform as RectTransform;
                ApplyHighlight();
                RefreshIndicators();
            };
        }
#endif
    }
}
#pragma warning restore CS1591
