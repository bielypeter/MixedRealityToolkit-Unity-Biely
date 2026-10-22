using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WheelchairSim
{
    /// <summary>
    /// Runs a test route: the user drives to each <see cref="SimGoal"/> in order. Time, path length and collisions are
    /// recorded per goal and written to <c>&lt;run&gt;_summary.csv</c> when the route is finished.
    /// </summary>
    /// <remarks>
    /// The clock starts with the first non-neutral command, so reading the instructions doesn't count.
    /// Press R (Editor / keyboard) or call <see cref="ResetRun"/> to start over.
    /// </remarks>
    public class SimTaskManager : MonoBehaviour
    {
        public enum RunState { Waiting, Running, Finished }

        public struct GoalResult
        {
            public string Goal;
            public float Seconds;
            public float PathMetres;
            public int Collisions;
        }

        [SerializeField]
        private SimulatedWheelchair chair;

        [Tooltip("Goals in the order they must be reached.")]
        [SerializeField]
        private SimGoal[] route = new SimGoal[0];

        [Tooltip("Optional text shown to the user (e.g. above the interface panel).")]
        [SerializeField]
        private TMP_Text hud;

        private readonly List<GoalResult> results = new List<GoalResult>();
        private float runStartTime;
        private float segmentStartTime;
        private float segmentStartPath;
        private int segmentStartCollisions;

        public RunState State { get; private set; } = RunState.Waiting;

        public int CurrentIndex { get; private set; }

        public string CurrentGoalName =>
            State == RunState.Running && CurrentIndex < route.Length ? route[CurrentIndex].GoalName : string.Empty;

        public IReadOnlyList<GoalResult> Results => results;

        private void Start()
        {
            ResetRun(false);
        }

        private void Update()
        {
            if (ResetPressed())
            {
                ResetRun(true);
                return;
            }

            if (chair == null || route.Length == 0)
            {
                return;
            }

            switch (State)
            {
                case RunState.Waiting:
                    if (chair.CommandX != SimulatedWheelchair.Neutral || chair.CommandY != SimulatedWheelchair.Neutral)
                    {
                        State = RunState.Running;
                        runStartTime = segmentStartTime = Time.time;
                        segmentStartPath = chair.PathLength;
                        segmentStartCollisions = chair.CollisionCount;
                        route[CurrentIndex].SetState(SimGoal.State.Active);
                    }
                    break;

                case RunState.Running:
                    if (route[CurrentIndex].Contains(chair.Position2D))
                    {
                        CompleteCurrentGoal();
                    }
                    break;
            }

            UpdateHud();
        }

        /// <summary>Back to the start pose and the first goal. With <paramref name="moveChair"/> the chair is reset too.</summary>
        public void ResetRun(bool moveChair = true)
        {
            if (moveChair && chair != null)
            {
                chair.ResetToStart();
            }

            results.Clear();
            CurrentIndex = 0;
            State = RunState.Waiting;
            for (int i = 0; i < route.Length; i++)
            {
                route[i].SetState(i == 0 ? SimGoal.State.Active : SimGoal.State.Idle);
            }
            UpdateHud();
        }

        private void CompleteCurrentGoal()
        {
            SimGoal goal = route[CurrentIndex];
            results.Add(new GoalResult
            {
                Goal = goal.GoalName,
                Seconds = Time.time - segmentStartTime,
                PathMetres = chair.PathLength - segmentStartPath,
                Collisions = chair.CollisionCount - segmentStartCollisions,
            });
            goal.SetState(SimGoal.State.Done);

            segmentStartTime = Time.time;
            segmentStartPath = chair.PathLength;
            segmentStartCollisions = chair.CollisionCount;
            CurrentIndex++;

            if (CurrentIndex >= route.Length)
            {
                State = RunState.Finished;
                WriteSummary();
            }
            else
            {
                route[CurrentIndex].SetState(SimGoal.State.Active);
            }
        }

        private void WriteSummary()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("interface,goal,seconds,pathMetres,collisions");
            float totalSeconds = 0f, totalPath = 0f;
            int totalCollisions = 0;
            foreach (GoalResult r in results)
            {
                sb.AppendLine(string.Format(inv, "{0},{1},{2:F2},{3:F2},{4}", SimSession.InterfaceName, r.Goal, r.Seconds, r.PathMetres, r.Collisions));
                totalSeconds += r.Seconds;
                totalPath += r.PathMetres;
                totalCollisions += r.Collisions;
            }
            sb.AppendLine(string.Format(inv, "{0},TOTAL,{1:F2},{2:F2},{3}", SimSession.InterfaceName, totalSeconds, totalPath, totalCollisions));

            string path = SimSession.PathFor("_summary");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[SimTaskManager] Route finished in {totalSeconds:F1} s, {totalPath:F1} m, {totalCollisions} collisions. Summary: {path}");
        }

        private void UpdateHud()
        {
            if (hud == null)
            {
                return;
            }

            switch (State)
            {
                case RunState.Waiting:
                    hud.text = route.Length > 0
                        ? $"Goal 1/{route.Length}: {route[0].GoalName}  -  start driving"
                        : "No goals";
                    break;
                case RunState.Running:
                    hud.text = $"Goal {CurrentIndex + 1}/{route.Length}: {CurrentGoalName}  -  {Time.time - runStartTime:F1} s";
                    break;
                case RunState.Finished:
                    float total = 0f;
                    foreach (GoalResult r in results)
                    {
                        total += r.Seconds;
                    }
                    hud.text = $"Done in {total:F1} s  -  collisions: {chair.CollisionCount}";
                    break;
            }
        }

        private static bool ResetPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}
