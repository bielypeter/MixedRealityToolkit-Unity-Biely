using System.Collections;
using MixedReality.Toolkit;
using MixedReality.Toolkit.Examples.Demos;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WheelchairSim
{
    /// <summary>
    /// Put in an interface scene (Interface1 / Interface2). With <see cref="Simulate"/> on, it loads the shared
    /// simulation scene additively and seats the user in the virtual wheelchair: the XR rig and the interface panel are
    /// parented to the chair, so both move with it. With it off, the scene is untouched (real chair over BLE only).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimulationLoader : MonoBehaviour
    {
        [Tooltip("Load the virtual apartment and drive the simulated chair. Turn off to use only the real chair.")]
        [SerializeField]
        private bool simulate = true;

        [SerializeField]
        private string simSceneName = "WheelchairSim";

        [Tooltip("Don't connect to the real chair while simulating.")]
        [SerializeField]
        private bool disableBleWhileSimulating = true;

        [Tooltip("Log the run to CSV. Off where the apartment is only a backdrop (main menu).")]
        [SerializeField]
        private bool recordSession = true;

        [Header("Seating")]
        [Tooltip("Sets the rig's camera height to a seated eye height. Applies when the device has no floor tracking " +
                 "(Editor simulator, device-origin tracking); with floor tracking the user's real head height is used.")]
        [SerializeField]
        private bool overrideEyeHeight = true;

        [SerializeField, Range(0.8f, 1.8f)]
        private float seatedEyeHeight = 1.2f;

        [Tooltip("Interface panel to mount on the chair. Empty = the canvas of the JoystickController / GazeArrowPad in this scene.")]
        [SerializeField]
        private Transform panel;

        [Tooltip("Keep the panel where it is placed in this scene, relative to the XR rig, and carry it with the chair. " +
                 "Off = snap it to the chair's PanelMount.")]
        [SerializeField]
        private bool keepPanelScenePose = true;

        [Header("Display")]
        [Tooltip("Opaque background so a headset shows the virtual apartment (VR) instead of passthrough.")]
        [SerializeField]
        private bool forceOpaqueCamera = true;

        [SerializeField]
        private Color backgroundColor = new Color(0.62f, 0.72f, 0.82f, 1f);

        /// <summary>
        /// Set by the main menu; when it has a value it replaces <see cref="Simulate"/> in every interface scene.
        /// Null (scene opened directly) keeps the Inspector value.
        /// </summary>
        public static bool? SimulateOverride;

        // Keeps a stale choice from leaking into the next play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SimulateOverride = null;

        public bool Simulate
        {
            get => simulate;
            set => simulate = value;
        }

        public SimulatedWheelchair Chair { get; private set; }

        private void Awake()
        {
            if (SimulateOverride.HasValue)
            {
                simulate = SimulateOverride.Value;
            }

            if (simulate && disableBleWhileSimulating)
            {
                // Disabled before its Start runs, so it never starts scanning for the chair.
                foreach (TestBle ble in FindObjectsOfType<TestBle>())
                {
                    ble.enabled = false;
                }
            }
        }

        private IEnumerator Start()
        {
            if (!simulate)
            {
                yield break;
            }

            // Before the sim scene loads: its logger decides in OnEnable whether to open a file.
            SimSession.Recording = recordSession;

            if (!SceneManager.GetSceneByName(simSceneName).isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(simSceneName, LoadSceneMode.Additive);
                if (load == null)
                {
                    Debug.LogError($"[SimulationLoader] Scene '{simSceneName}' is not in Build Settings. Run MRTK > Wheelchair Sim > Build Sim Scene.");
                    yield break;
                }
                yield return load;
            }

            Chair = FindObjectOfType<SimulatedWheelchair>();
            if (Chair == null)
            {
                Debug.LogError($"[SimulationLoader] No SimulatedWheelchair in '{simSceneName}'.");
                yield break;
            }

            Transform panelTarget = panel != null ? panel : FindInterfacePanel();
            XROrigin origin = FindObjectOfType<XROrigin>();

            // The panel's pose relative to the rig as placed in the scene, taken before the rig is moved onto the chair.
            bool keepPose = keepPanelScenePose && origin != null && panelTarget != null;
            Vector3 panelLocalPos = keepPose ? origin.transform.InverseTransformPoint(panelTarget.position) : Vector3.zero;
            Quaternion panelLocalRot = keepPose ? Quaternion.Inverse(origin.transform.rotation) * panelTarget.rotation : Quaternion.identity;

            MountRig(origin);
            MountPanel(panelTarget, keepPose ? Chair.SeatMount : Chair.PanelMount, panelLocalPos, panelLocalRot);
            Chair.IgnoreOwnColliders();

            if (forceOpaqueCamera)
            {
                // MRTK's camera settings would switch back to the (passthrough) clear mode, so take over.
                foreach (CameraSettingsManager settings in FindObjectsOfType<CameraSettingsManager>())
                {
                    settings.enabled = false;
                }
            }

            yield return null; // after the first frame, once the rig has set up tracking and the camera

            if (overrideEyeHeight && origin != null)
            {
                ApplySeatedEyeHeight(origin);
            }

            if (forceOpaqueCamera)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = backgroundColor;
                }
            }
        }

        private void MountRig(XROrigin origin)
        {
            if (origin == null)
            {
                Debug.LogWarning("[SimulationLoader] No XR Origin found; the camera won't ride the chair.");
                return;
            }

            origin.transform.SetParent(Chair.SeatMount, false);
            origin.transform.localPosition = Vector3.zero;
            origin.transform.localRotation = Quaternion.identity;
        }

        private void ApplySeatedEyeHeight(XROrigin origin)
        {
            // With floor tracking the headset reports the real (seated) head height; leave it alone.
            if (origin.CurrentTrackingOriginMode == UnityEngine.XR.TrackingOriginModeFlags.Floor)
            {
                return;
            }

            origin.CameraYOffset = seatedEyeHeight;
            // Without an XR device (Editor simulator) XROrigin doesn't apply the offset itself.
            if (origin.CameraFloorOffsetObject != null)
            {
                Transform offset = origin.CameraFloorOffsetObject.transform;
                offset.localPosition = new Vector3(offset.localPosition.x, seatedEyeHeight, offset.localPosition.z);
            }
        }

        private static void MountPanel(Transform target, Transform mount, Vector3 localPosition, Quaternion localRotation)
        {
            if (target == null)
            {
                Debug.LogWarning("[SimulationLoader] No interface panel found; it will stay where it is.");
                return;
            }

            // Keeps its local scale (0.001 = 1 canvas unit per mm) because the mounts aren't scaled.
            target.SetParent(mount, false);
            target.localPosition = localPosition;
            target.localRotation = localRotation;
        }

        private static Transform FindInterfacePanel()
        {
            Component control = FindObjectOfType<JoystickController>();
            if (control == null)
            {
                control = FindObjectOfType<GazeArrowPad>();
            }
            if (control == null)
            {
                return null;
            }

            Canvas canvas = control.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.transform : control.transform.root;
        }
    }
}
