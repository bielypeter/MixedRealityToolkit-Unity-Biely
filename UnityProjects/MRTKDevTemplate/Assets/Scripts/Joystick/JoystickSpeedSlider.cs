// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using MixedReality.Toolkit.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Gaze-operated vertical slider that sets the maximum speed of a joystick or arrow pad at runtime.
    /// </summary>
    /// <remarks>
    /// Dwell on the slider to grab it; the handle then follows the gaze up and down. Looking away releases it and keeps the value.
    /// Put this on the slider's hit area (a raycast-target graphic, so the mouse also works for testing in the editor).
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("MRTK/Examples/Joystick Speed Slider")]
    public class JoystickSpeedSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Parts")]
        [SerializeField]
        [FormerlySerializedAs("joystick")]
        [Tooltip("Component whose speed this slider sets. Must implement ISpeedAdjustable (JoystickController, GazeArrowPad).")]
        private MonoBehaviour target;

        private ISpeedAdjustable Target => target as ISpeedAdjustable;

        [SerializeField]
        [Tooltip("Track whose height is the slider range.")]
        private RectTransform track;

        [SerializeField]
        [Tooltip("Fill inside the track, stretched from the bottom up to the value.")]
        private RectTransform fill;

        [SerializeField]
        private RectTransform handle;

        [SerializeField]
        [Tooltip("Tinted while dwelling / held.")]
        private UnityEngine.UI.Graphic handleGraphic;

        [SerializeField]
        private TMP_Text valueText;

        [Header("Range")]
        [SerializeField]
        [Min(0.001f)]
        private float minSpeed = 0.1f;

        [SerializeField]
        private float maxSpeed = 20f;

        [SerializeField]
        [Tooltip("Logarithmic mapping gives finer control at low speeds.")]
        private bool logarithmic = true;

        [Header("Gaze")]
        [SerializeField]
        private GazeInteractor gazeInteractor;

        [SerializeField]
        [Min(0f)]
        private float engageDwell = 0.4f;

        [SerializeField]
        [Min(0f)]
        private float releaseDelay = 0.25f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Tolerance around the hit area, in canvas units, before the gaze counts as having left.")]
        private float releasePadding = 4f;

        [SerializeField]
        [Min(0f)]
        private float smoothing = 0.08f;

        [Header("Colors")]
        [SerializeField]
        private Color idleColor = new Color(0.91f, 0.92f, 0.95f, 1f);

        [SerializeField]
        private Color activeColor = new Color(0.49f, 0.61f, 1f, 1f);

        private RectTransform rectTransform;
        private float normalized;
        private float smoothedNormalized;
        private float dwellTimer;
        private float outsideTimer;
        private bool gazeHolding;
        private bool pointerHolding;

        public bool IsHeld => gazeHolding || pointerHolding;

        private void OnEnable()
        {
            rectTransform = (RectTransform)transform;
            gazeHolding = pointerHolding = false;
            dwellTimer = outsideTimer = 0f;
            SyncFromJoystick();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                // Mirror the target's speed in the editor so the slider shows the real value.
                SyncFromJoystick();
                return;
            }

            UpdateGaze();
        }

        private void UpdateGaze()
        {
            bool hasPoint = GazeRayUtility.TryGetGazePoint(ref gazeInteractor, rectTransform, out Vector2 point);
            Rect area = rectTransform.rect;

            if (!gazeHolding)
            {
                if (hasPoint && area.Contains(point) && !pointerHolding)
                {
                    dwellTimer += Time.deltaTime;
                    ApplyHighlight(engageDwell > 0f ? dwellTimer / engageDwell : 1f);
                    if (dwellTimer >= engageDwell)
                    {
                        gazeHolding = true;
                        outsideTimer = 0f;
                        smoothedNormalized = normalized;
                        ApplyHighlight(1f);
                    }
                }
                else if (dwellTimer > 0f)
                {
                    dwellTimer = 0f;
                    ApplyHighlight(0f);
                }
                return;
            }

            Rect padded = new Rect(area.x - releasePadding, area.y - releasePadding,
                area.width + 2f * releasePadding, area.height + 2f * releasePadding);
            if (hasPoint && padded.Contains(point))
            {
                outsideTimer = 0f;
                float target = NormalizedFromLocal(point);
                smoothedNormalized = Mathf.Lerp(smoothedNormalized, target, GazeRayUtility.SmoothingFactor(smoothing));
                SetNormalized(smoothedNormalized);
            }
            else
            {
                outsideTimer += Time.deltaTime;
                if (outsideTimer >= releaseDelay)
                {
                    gazeHolding = false;
                    dwellTimer = 0f;
                    ApplyHighlight(0f);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerHolding = true;
            ApplyHighlight(1f);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
            if (pointerHolding && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, cam, out Vector2 local))
            {
                SetNormalized(NormalizedFromLocal(local));
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerHolding = false;
            ApplyHighlight(0f);
        }

        /// <summary>Speed for a slider position from 0 (bottom) to 1 (top).</summary>
        public float SpeedFromNormalized(float t)
        {
            t = Mathf.Clamp01(t);
            return logarithmic && minSpeed > 0f && maxSpeed > minSpeed
                ? minSpeed * Mathf.Pow(maxSpeed / minSpeed, t)
                : Mathf.Lerp(minSpeed, maxSpeed, t);
        }

        public float NormalizedFromSpeed(float speed)
        {
            if (logarithmic && minSpeed > 0f && maxSpeed > minSpeed)
            {
                return Mathf.Clamp01(Mathf.Log(Mathf.Max(speed, minSpeed) / minSpeed) / Mathf.Log(maxSpeed / minSpeed));
            }
            return Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        }

        private float NormalizedFromLocal(Vector2 local)
        {
            RectTransform reference = track != null ? track : rectTransform;
            Vector2 inTrack = reference.InverseTransformPoint(rectTransform.TransformPoint(local));
            Rect r = reference.rect;
            return Mathf.InverseLerp(r.yMin, r.yMax, inTrack.y);
        }

        private void SetNormalized(float t)
        {
            normalized = Mathf.Clamp01(t);
            if (Target != null)
            {
                Target.MaxSpeed = SpeedFromNormalized(normalized);
            }
            UpdateVisuals();
        }

        private void SyncFromJoystick()
        {
            if (Target != null)
            {
                normalized = NormalizedFromSpeed(Target.MaxSpeed);
            }
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (fill != null)
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(1f, normalized);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            if (handle != null && track != null)
            {
                Rect r = track.rect;
                Vector3 world = track.TransformPoint(new Vector3(r.center.x, Mathf.Lerp(r.yMin, r.yMax, normalized), 0f));
                Vector3 local = ((RectTransform)handle.parent).InverseTransformPoint(world);
                handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, local.y - ((RectTransform)handle.parent).rect.center.y);
            }

            if (valueText != null)
            {
                float speed = Target != null ? Target.MaxSpeed : SpeedFromNormalized(normalized);
                valueText.text = speed < 10f ? $"{speed:0.0#}/s" : $"{speed:0.0}/s";
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Deferred: changing RectTransforms directly inside OnValidate triggers editor warnings.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && !Application.isPlaying)
                {
                    rectTransform = (RectTransform)transform;
                    SyncFromJoystick();
                }
            };
        }
#endif

        private void ApplyHighlight(float progress)
        {
            if (handleGraphic != null)
            {
                handleGraphic.color = IsHeld ? activeColor : Color.Lerp(idleColor, activeColor, Mathf.Clamp01(progress) * 0.6f);
            }
        }
    }
}
#pragma warning restore CS1591
