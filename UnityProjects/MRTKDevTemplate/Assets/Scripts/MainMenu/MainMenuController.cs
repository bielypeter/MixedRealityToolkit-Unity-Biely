// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using WheelchairSim;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Start menu with dual-gaze selection, like the arrow lock of <see cref="GazeArrowPad"/>:
    /// <list type="bullet">
    /// <item>Looking at an option reveals its confirmation button next to it; looking anywhere else hides it.</item>
    /// <item>Moving the gaze from the option into its confirmation (MRTK gaze dwell) selects it.</item>
    /// </list>
    /// Rows are the <see cref="MenuRow"/> children, so adding an interface is duplicating a row and setting its scene.
    /// </summary>
    [AddComponentMenu("MRTK/Examples/Main Menu Controller")]
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Row whose label shows the current mode; its On Confirmed should call ToggleSimulation.")]
        [SerializeField]
        private MenuRow modeRow;

        [SerializeField]
        private TMP_Text statusLabel;

        [Tooltip("Seconds a confirmation stays visible after the gaze leaves its row, so the jump from option to confirmation doesn't hide it.")]
        [SerializeField, Min(0f)]
        private float revealGrace = 0.3f;

        private MenuRow[] rows;
        private bool loading;

        private static bool Simulate => SimulationLoader.SimulateOverride ?? true;

        private void Awake()
        {
            rows = GetComponentsInChildren<MenuRow>(true);
            foreach (MenuRow row in rows)
            {
                if (row.Confirm != null)
                {
                    MenuRow current = row;
                    row.Confirm.OnClicked.AddListener(() => Confirmed(current));
                }
                row.SetConfirmVisible(false);
            }

            // The menu always decides the mode, so interfaces opened from it follow the label shown here.
            SimulationLoader.SimulateOverride = Simulate;
            RefreshModeLabel();
            SetStatus(string.Empty);
        }

        private void Update()
        {
            if (loading)
            {
                return;
            }

            // Only one confirmation at a time: the row being looked at takes over at once.
            MenuRow engaged = null;
            foreach (MenuRow row in rows)
            {
                if (row.IsEngaged)
                {
                    engaged = row;
                    break;
                }
            }

            foreach (MenuRow row in rows)
            {
                if (engaged != null)
                {
                    row.Reveal = row == engaged ? revealGrace : 0f;
                }
                else
                {
                    row.Reveal = Mathf.Max(0f, row.Reveal - Time.deltaTime);
                }

                row.SetConfirmVisible(row == engaged || row.Reveal > 0f);
            }
        }

        /// <summary>
        /// Switches between the simulated chair (virtual apartment) and the real chair (BLE), then reloads the menu
        /// so its backdrop matches.
        /// </summary>
        public void ToggleSimulation()
        {
            SimulationLoader.SimulateOverride = !Simulate;
            RefreshModeLabel();
            // Not gameObject.scene: in simulation the panel is mounted on the chair inside the sim scene.
            Load(SceneManager.GetActiveScene().name);
        }

        public void Load(string sceneName)
        {
            if (loading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MainMenu] Scene '{sceneName}' is not in Build Settings.");
                SetStatus($"Scene '{sceneName}' not found");
                return;
            }

            loading = true;
            // Lock the menu once a scene is picked so nothing else fires while it loads.
            foreach (MenuRow row in rows)
            {
                row.SetInteractable(false);
            }

            SetStatus("Loading...");
            SceneManager.LoadSceneAsync(sceneName);
        }

        private void Confirmed(MenuRow row)
        {
            if (!string.IsNullOrEmpty(row.SceneName))
            {
                Load(row.SceneName);
            }
            else
            {
                row.OnConfirmed.Invoke();
            }
        }

        private void RefreshModeLabel()
        {
            if (modeRow != null)
            {
                modeRow.LabelText = Simulate ? "Mode: Simulation" : "Mode: Real wheelchair";
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }
    }
}
#pragma warning restore CS1591
