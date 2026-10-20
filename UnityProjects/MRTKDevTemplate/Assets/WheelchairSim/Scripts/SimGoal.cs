using TMPro;
using UnityEngine;

namespace WheelchairSim
{
    /// <summary>
    /// A target spot on the floor. It counts as reached when the chair's centre is within <see cref="Radius"/>
    /// (checked on the X/Z plane by <see cref="SimTaskManager"/>, no physics triggers needed).
    /// </summary>
    public class SimGoal : MonoBehaviour
    {
        public enum State { Idle, Active, Done }

        [SerializeField]
        private string goalName = "Goal";

        [SerializeField, Min(0.1f)]
        private float radius = 0.5f;

        [Tooltip("Flat disc on the floor.")]
        [SerializeField]
        private Renderer marker;

        [Tooltip("Floating name above the goal.")]
        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        private Color idleColor = new Color(0.55f, 0.6f, 0.7f, 1f);

        [SerializeField]
        private Color activeColor = new Color(1f, 0.75f, 0.2f, 1f);

        [SerializeField]
        private Color doneColor = new Color(0.3f, 0.85f, 0.45f, 1f);

        private MaterialPropertyBlock block;
        private Vector3 markerScale;

        public string GoalName => goalName;

        public float Radius => radius;

        public State Current { get; private set; } = State.Idle;

        public bool Contains(Vector2 floorPosition)
        {
            Vector2 centre = new Vector2(transform.position.x, transform.position.z);
            return (floorPosition - centre).sqrMagnitude <= radius * radius;
        }

        private void Awake()
        {
            if (marker != null)
            {
                markerScale = marker.transform.localScale;
            }
            SetState(State.Idle);
        }

        public void SetState(State state)
        {
            Current = state;
            Color color = state == State.Active ? activeColor : state == State.Done ? doneColor : idleColor;

            if (marker != null)
            {
                block ??= new MaterialPropertyBlock();
                marker.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                marker.SetPropertyBlock(block);
                marker.transform.localScale = markerScale;
            }
            if (label != null)
            {
                label.color = color;
                label.alpha = state == State.Active ? 1f : 0.45f;
            }
        }

        private void Update()
        {
            // Pulse the current goal so it is easy to find.
            if (Current == State.Active && marker != null)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 4f);
                marker.transform.localScale = new Vector3(markerScale.x * pulse, markerScale.y, markerScale.z * pulse);
            }

            // Keep the label readable from wherever the user is.
            Camera cam = Camera.main;
            if (label != null && cam != null)
            {
                Vector3 away = label.transform.position - cam.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    label.transform.rotation = Quaternion.LookRotation(away);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = activeColor;
            Vector3 c = transform.position;
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                                c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }
    }
}
