using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace WheelchairSim
{
    /// <summary>
    /// Writes the chair's trajectory and the commands it received to <c>&lt;run&gt;.csv</c> at a fixed rate.
    /// A new file is started whenever the chair is reset.
    /// </summary>
    public class WheelchairSessionLogger : MonoBehaviour
    {
        private const string Header =
            "t,x,z,headingDeg,linSpeed,angSpeed,cmdX,cmdY,interface,goal,contact,collisions,pathMetres";

        [SerializeField]
        private SimulatedWheelchair chair;

        [SerializeField]
        private SimTaskManager tasks;

        [Tooltip("Seconds between rows (0.05 = 20 Hz, the same rate TestBle sends to the real chair).")]
        [SerializeField, Min(0.005f)]
        private float sampleInterval = 0.05f;

        [SerializeField]
        private bool logEnabled = true;

        private StreamWriter writer;
        private float startTime;
        private float sampleTimer;
        private float flushTimer;

        public string CurrentPath { get; private set; }

        private void OnEnable()
        {
            if (chair != null)
            {
                chair.WasReset += Restart;
            }
            Open();
        }

        private void OnDisable()
        {
            if (chair != null)
            {
                chair.WasReset -= Restart;
            }
            Close();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                writer?.Flush();
            }
        }

        private void Restart()
        {
            Close();
            Open();
        }

        private void Open()
        {
            if (!logEnabled || chair == null || !SimSession.Recording)
            {
                return;
            }

            SimSession.New();
            CurrentPath = SimSession.PathFor(string.Empty);
            writer = new StreamWriter(CurrentPath, false, new UTF8Encoding(false));
            writer.WriteLine(Header);
            startTime = Time.time;
            sampleTimer = flushTimer = 0f;
            Debug.Log($"[WheelchairSessionLogger] Logging to {CurrentPath}");
        }

        private void Close()
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Dispose();
                writer = null;
            }
        }

        private void Update()
        {
            if (writer == null)
            {
                return;
            }

            sampleTimer += Time.deltaTime;
            if (sampleTimer >= sampleInterval)
            {
                sampleTimer %= sampleInterval;
                WriteRow();
            }

            flushTimer += Time.deltaTime;
            if (flushTimer >= 1f)
            {
                flushTimer = 0f;
                writer.Flush();
            }
        }

        private void WriteRow()
        {
            Vector2 p = chair.Position2D;
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3:F1},{4:F3},{5:F1},{6},{7},{8},{9},{10},{11},{12:F3}",
                Time.time - startTime, p.x, p.y, chair.HeadingDeg, chair.LinearSpeed, chair.AngularSpeed,
                chair.CommandX, chair.CommandY, SimSession.InterfaceName, tasks != null ? tasks.CurrentGoalName : string.Empty,
                chair.IsColliding ? 1 : 0, chair.CollisionCount, chair.PathLength));
        }
    }
}
