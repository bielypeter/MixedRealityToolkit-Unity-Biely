// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using MixedReality.Toolkit.Input;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// "Back to main menu" gaze button that hovers straight above the user's head, so it stays out of view while
    /// driving. Looking up at it for <see cref="dwellTime"/> stops the chair and loads the main menu.
    /// </summary>
    /// <remarks>
    /// The button follows the head position (not its pitch), facing down at the user. Its visual is built at runtime,
    /// so the component only needs to sit on an empty GameObject in the interface scene.
    /// </remarks>
    [AddComponentMenu("MRTK/Examples/Gaze Return To Menu")]
    public class GazeReturnToMenu : MonoBehaviour
    {
        [Tooltip("Scene loaded on dwell (must be in Build Settings).")]
        [SerializeField]
        private string menuSceneName = "MainMenu";

        [Tooltip("Seconds the gaze must rest on the button.")]
        [SerializeField, Min(0f)]
        private float dwellTime = 0.5f;

        [Tooltip("Seconds the gaze may slip off the button (blinks, jitter) before the dwell resets.")]
        [SerializeField, Min(0f)]
        private float releaseGrace = 0.1f;

        [Header("Placement")]
        [Tooltip("Meters above the camera.")]
        [SerializeField, Min(0.1f)]
        private float height = 0.6f;

        [Tooltip("Diameter of the button (and its gaze target) in meters.")]
        [SerializeField, Min(0.01f)]
        private float diameter = 0.3f;

        [Header("Look")]
        [SerializeField]
        private string label = "Main\nmenu";

        [SerializeField]
        private Color idleColor = new Color(0.13f, 0.14f, 0.16f, 0.5f);

        [SerializeField]
        private Color fillColor = new Color(0.49f, 0.61f, 1f, 0.85f);

        [Header("Gaze")]
        [Tooltip("Gaze interactor providing the eye ray. Found automatically when empty.")]
        [SerializeField]
        private GazeInteractor gazeInteractor;

        // 1 canvas unit = 1 mm.
        private const float CanvasScale = 0.001f;

        private RectTransform hitArea;
        private RectTransform fill;
        private Camera cam;
        private float dwellTimer;
        private float awayTimer;
        private bool loading;

        private void Awake()
        {
            BuildVisual();
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    return;
                }
            }

            Place();

            if (!loading)
            {
                UpdateDwell();
            }

            float progress = dwellTime > 0f ? Mathf.Clamp01(dwellTimer / dwellTime) : (loading ? 1f : 0f);
            fill.localScale = Vector3.one * (loading ? 1f : progress);
        }

        private void Place()
        {
            Transform head = cam.transform;
            transform.position = head.position + Vector3.up * height;

            // Facing down at the head, with the label upright for someone tilting their head back. The head's up vector
            // gives its heading when looking up (where its forward is nearly vertical).
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (head.forward.y > 0f)
            {
                forward -= Vector3.ProjectOnPlane(head.up, Vector3.up);
            }
            if (forward.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(Vector3.up, -forward);
            }
        }

        private void UpdateDwell()
        {
            bool lookedAt = GazeRayUtility.TryGetGazePoint(ref gazeInteractor, hitArea, out Vector2 point)
                && Vector2.Distance(point, hitArea.rect.center) <= hitArea.rect.width * 0.5f;

            if (lookedAt)
            {
                awayTimer = 0f;
                dwellTimer += Time.deltaTime;
                if (dwellTimer >= dwellTime)
                {
                    ReturnToMenu();
                }
                return;
            }

            awayTimer += Time.deltaTime;
            if (awayTimer > releaseGrace)
            {
                dwellTimer = 0f;
            }
        }

        public void ReturnToMenu()
        {
            if (loading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(menuSceneName))
            {
                Debug.LogError($"[GazeReturnToMenu] Scene '{menuSceneName}' is not in Build Settings.");
                dwellTimer = 0f;
                return;
            }

            loading = true;

            // Stop the chair right away rather than when the scene unloads (each bridge sends neutral when disabled).
            foreach (WheelchairBridge bridge in FindObjectsOfType<WheelchairBridge>())
            {
                bridge.enabled = false;
            }

            SceneManager.LoadSceneAsync(menuSceneName);
        }

        private void BuildVisual()
        {
            float size = diameter / CanvasScale;

            var canvasGo = new GameObject("ReturnToMenuCanvas", typeof(RectTransform), typeof(Canvas));
            hitArea = (RectTransform)canvasGo.transform;
            hitArea.SetParent(transform, false);
            hitArea.sizeDelta = new Vector2(size, size);
            hitArea.localScale = Vector3.one * CanvasScale;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            RoundedShapeGraphic background = CreateCircle("Background", hitArea);
            background.color = idleColor;

            RoundedShapeGraphic progress = CreateCircle("Fill", hitArea);
            progress.color = fillColor;
            fill = progress.rectTransform;
            fill.localScale = Vector3.zero;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(hitArea, false);
            Stretch(text.rectTransform);
            text.text = label;
            text.fontSize = size * 0.16f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private static RoundedShapeGraphic CreateCircle(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedShapeGraphic));
            var shape = go.GetComponent<RoundedShapeGraphic>();
            shape.rectTransform.SetParent(parent, false);
            Stretch(shape.rectTransform);
            shape.Circle = true;
            shape.raycastTarget = false;
            return shape;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#pragma warning restore CS1591
