using System;
using UnityEngine;

namespace WheelchairSim
{
    /// <summary>
    /// Virtual wheelchair driven by exactly the same X/Y bytes as the real chair (see <see cref="TestBle"/>).
    /// 1 Unity unit = 1 m; the chair moves on the X/Z plane and is blocked by colliders (walls, furniture).
    /// </summary>
    /// <remarks>
    /// The byte -> speed mapping and the acceleration limits are the calibration knobs: measure the real chair
    /// (e.g. drive with Y = 158 for 5 s and measure the distance) and enter the values here.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public class SimulatedWheelchair : MonoBehaviour, IWheelchairDriver
    {
        public const int Neutral = 128;

        [Header("Command -> motion (calibrate against the real chair)")]
        [Tooltip("Forward speed (m/s) at full scale, i.e. Y = 255. Backward uses the same scale.")]
        [SerializeField, Min(0f)]
        private float maxLinearSpeed = 2f;

        [Tooltip("Turn speed (deg/s) at full scale, i.e. X = 0 or 255.")]
        [SerializeField, Min(0f)]
        private float maxTurnSpeed = 90f;

        [Tooltip("Byte offsets from 128 up to this value are treated as neutral.")]
        [SerializeField, Range(0, 20)]
        private int deadband = 2;

        [Tooltip("How fast the chair speeds up (m/s²).")]
        [SerializeField, Min(0.01f)]
        private float acceleration = 1f;

        [Tooltip("How fast the chair slows down / brakes (m/s²).")]
        [SerializeField, Min(0.01f)]
        private float deceleration = 1.5f;

        [Tooltip("How fast the turn rate changes (deg/s²).")]
        [SerializeField, Min(0.01f)]
        private float angularAcceleration = 180f;

        [Header("Fidelity")]
        [Tooltip("Only pick up new commands every Sample Interval, like TestBle which sends every 50 ms.")]
        [SerializeField]
        private bool sampleLikeBle = true;

        [SerializeField, Min(0.001f)]
        private float sampleInterval = 0.05f;

        [Header("Mount points")]
        [Tooltip("The XR rig is parented here (floor level, under the user's head).")]
        [SerializeField]
        private Transform seatMount;

        [Tooltip("The interface panel is parented here, so it moves with the chair.")]
        [SerializeField]
        private Transform panelMount;

        private CharacterController controller;
        private int pendingX = Neutral;
        private int pendingY = Neutral;
        private float sampleTimer;
        private bool wasColliding;
        private Vector3 startPosition;
        private Quaternion startRotation;

        public Transform SeatMount => seatMount;

        public Transform PanelMount => panelMount;

        /// <summary>X byte currently acted on (turn: 0 left .. 128 neutral .. 255 right).</summary>
        public int CommandX { get; private set; } = Neutral;

        /// <summary>Y byte currently acted on (drive: 0 backward .. 128 neutral .. 255 forward).</summary>
        public int CommandY { get; private set; } = Neutral;

        /// <summary>Current forward speed (m/s, negative = backward).</summary>
        public float LinearSpeed { get; private set; }

        /// <summary>Current turn rate (deg/s, positive = right / clockwise seen from above).</summary>
        public float AngularSpeed { get; private set; }

        /// <summary>Position on the floor in metres: x = world X, y = world Z.</summary>
        public Vector2 Position2D => new Vector2(transform.position.x, transform.position.z);

        /// <summary>Heading in degrees (0 = +Z, 90 = +X).</summary>
        public float HeadingDeg => transform.eulerAngles.y;

        /// <summary>Distance actually travelled since the last reset (m).</summary>
        public float PathLength { get; private set; }

        /// <summary>Number of separate wall/furniture contacts since the last reset.</summary>
        public int CollisionCount { get; private set; }

        /// <summary>True while the chair is touching a wall or furniture.</summary>
        public bool IsColliding { get; private set; }

        public event Action Collided;

        public event Action WasReset;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void Start()
        {
            IgnoreOwnColliders();
        }

        private void OnEnable()
        {
            WheelchairDrivers.Register(this);
        }

        /// <summary>
        /// Makes the chair ignore colliders parented to it (rig, panel), so they can't block it.
        /// Call again after mounting new objects.
        /// </summary>
        public void IgnoreOwnColliders()
        {
            foreach (Collider other in GetComponentsInChildren<Collider>(true))
            {
                if (other != controller)
                {
                    Physics.IgnoreCollision(controller, other);
                }
            }
        }

        private void OnDisable()
        {
            WheelchairDrivers.Unregister(this);
        }

        public void SetXY(int x, int y)
        {
            // Same wrap-around as TestBle's byte cast, so out-of-range values behave like on the real chair.
            pendingX = (byte)x;
            pendingY = (byte)y;
            if (!sampleLikeBle)
            {
                CommandX = pendingX;
                CommandY = pendingY;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (sampleLikeBle)
            {
                sampleTimer += dt;
                if (sampleTimer >= sampleInterval)
                {
                    sampleTimer %= sampleInterval;
                    CommandX = pendingX;
                    CommandY = pendingY;
                }
            }

            float targetLinear = Normalize(CommandY) * maxLinearSpeed;
            float targetAngular = Normalize(CommandX) * maxTurnSpeed;

            bool slowingDown = LinearSpeed != 0f
                && (Mathf.Abs(targetLinear) < Mathf.Abs(LinearSpeed) || Mathf.Sign(targetLinear) != Mathf.Sign(LinearSpeed));
            LinearSpeed = Mathf.MoveTowards(LinearSpeed, targetLinear, (slowingDown ? deceleration : acceleration) * dt);
            AngularSpeed = Mathf.MoveTowards(AngularSpeed, targetAngular, angularAcceleration * dt);

            transform.Rotate(0f, AngularSpeed * dt, 0f, Space.World);

            Vector3 before = transform.position;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            controller.Move(forward.normalized * (LinearSpeed * dt));

            // Stay on the floor even if the controller was nudged up by an edge.
            Vector3 after = transform.position;
            if (!Mathf.Approximately(after.y, before.y))
            {
                after.y = before.y;
                controller.enabled = false;
                transform.position = after;
                controller.enabled = true;
            }

            Vector3 delta = after - before;
            delta.y = 0f;
            PathLength += delta.magnitude;

            IsColliding = (controller.collisionFlags & CollisionFlags.Sides) != 0;
            if (IsColliding && !wasColliding)
            {
                CollisionCount++;
                Collided?.Invoke();
            }
            wasColliding = IsColliding;
        }

        /// <summary>Puts the chair back at its start pose, stopped, and clears the counters.</summary>
        public void ResetToStart()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;

            pendingX = pendingY = CommandX = CommandY = Neutral;
            LinearSpeed = AngularSpeed = PathLength = 0f;
            CollisionCount = 0;
            IsColliding = wasColliding = false;
            WasReset?.Invoke();
        }

        private float Normalize(int value)
        {
            int offset = value - Neutral;
            if (Mathf.Abs(offset) <= deadband)
            {
                return 0f;
            }
            return Mathf.Clamp(offset / 127f, -1f, 1f);
        }
    }
}
