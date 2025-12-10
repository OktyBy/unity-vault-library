using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// Vehicle controller for cars, bikes, and other wheeled vehicles.
    /// </summary>
    public class VehicleController : MonoBehaviour
    {
        [Header("Vehicle Stats")]
        [SerializeField] private float maxSpeed = 30f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float brakeForce = 15f;
        [SerializeField] private float reverseSpeed = 10f;
        [SerializeField] private float steeringSensitivity = 2f;
        [SerializeField] private float maxSteerAngle = 35f;

        [Header("Physics")]
        [SerializeField] private float downforce = 100f;
        [SerializeField] private float grip = 1f;
        [SerializeField] private float driftFactor = 0.9f;
        [SerializeField] private Vector3 centerOfMass = new Vector3(0, -0.5f, 0);

        [Header("Wheels")]
        [SerializeField] private WheelData[] wheels;
        [SerializeField] private float suspensionDistance = 0.3f;
        [SerializeField] private float suspensionSpring = 35000f;
        [SerializeField] private float suspensionDamper = 4500f;

        [Header("Engine")]
        [SerializeField] private AnimationCurve enginePowerCurve;
        [SerializeField] private float[] gearRatios = { 3.5f, 2.5f, 1.8f, 1.3f, 1f, 0.8f };
        [SerializeField] private float currentGear = 1;
        [SerializeField] private bool automaticTransmission = true;

        [Header("Input")]
        [SerializeField] private bool useRawInput = false;
        [SerializeField] private KeyCode brakeKey = KeyCode.Space;
        [SerializeField] private KeyCode handbrakeKey = KeyCode.LeftShift;

        [Header("Audio")]
        [SerializeField] private float minEnginePitch = 0.5f;
        [SerializeField] private float maxEnginePitch = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent onEngineStart;
        [SerializeField] private UnityEvent onEngineStop;
        [SerializeField] private UnityEvent<int> onGearChanged;
        [SerializeField] private UnityEvent onDriftStart;
        [SerializeField] private UnityEvent onDriftEnd;

        // State
        private float currentSpeed;
        private float currentThrottle;
        private float currentSteering;
        private float currentBrake;
        private bool isEngineOn = true;
        private bool isDrifting;
        private bool isGrounded;

        // Components
        private Rigidbody rb;

        // Properties
        public float CurrentSpeed => currentSpeed;
        public float SpeedKMH => currentSpeed * 3.6f;
        public float SpeedMPH => currentSpeed * 2.237f;
        public float SpeedPercent => currentSpeed / maxSpeed;
        public int CurrentGear => (int)currentGear;
        public bool IsEngineOn => isEngineOn;
        public bool IsDrifting => isDrifting;
        public bool IsGrounded => isGrounded;
        public float ThrottleInput => currentThrottle;
        public float SteeringInput => currentSteering;

        // Events
        public event Action EngineStarted;
        public event Action EngineStopped;
        public event Action<int> GearChanged;
        public event Action DriftStarted;
        public event Action DriftEnded;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.centerOfMass = centerOfMass;
            }

            // Initialize wheel colliders
            SetupWheels();

            // Default engine curve if not set
            if (enginePowerCurve == null || enginePowerCurve.length == 0)
            {
                enginePowerCurve = AnimationCurve.Linear(0, 0.5f, 1, 1);
            }
        }

        private void SetupWheels()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider != null)
                {
                    JointSpring spring = wheel.wheelCollider.suspensionSpring;
                    spring.spring = suspensionSpring;
                    spring.damper = suspensionDamper;
                    wheel.wheelCollider.suspensionSpring = spring;
                    wheel.wheelCollider.suspensionDistance = suspensionDistance;
                }
            }
        }

        private void Update()
        {
            if (!isEngineOn) return;

            HandleInput();
            UpdateWheelVisuals();
            CheckDrifting();

            if (automaticTransmission)
            {
                HandleAutomaticTransmission();
            }
        }

        private void FixedUpdate()
        {
            if (!isEngineOn) return;

            ApplyMotor();
            ApplySteering();
            ApplyBrakes();
            ApplyDownforce();
            UpdateGroundedState();

            currentSpeed = rb.velocity.magnitude;
        }

        private void HandleInput()
        {
            if (useRawInput)
            {
                currentThrottle = Input.GetAxisRaw("Vertical");
                currentSteering = Input.GetAxisRaw("Horizontal");
            }
            else
            {
                currentThrottle = Input.GetAxis("Vertical");
                currentSteering = Input.GetAxis("Horizontal");
            }

            currentBrake = Input.GetKey(brakeKey) ? 1f : 0f;

            // Handbrake
            if (Input.GetKey(handbrakeKey))
            {
                currentBrake = 1f;
                // Reduce rear wheel grip for drift
            }
        }

        private void ApplyMotor()
        {
            if (currentThrottle == 0) return;

            float motorTorque = 0f;

            if (currentThrottle > 0)
            {
                // Forward
                if (currentSpeed < maxSpeed)
                {
                    float powerMultiplier = enginePowerCurve.Evaluate(SpeedPercent);
                    motorTorque = currentThrottle * acceleration * powerMultiplier * 100f;
                }
            }
            else
            {
                // Reverse
                if (currentSpeed < reverseSpeed)
                {
                    motorTorque = currentThrottle * acceleration * 50f;
                }
            }

            // Apply gear ratio
            motorTorque *= gearRatios[Mathf.Clamp((int)currentGear - 1, 0, gearRatios.Length - 1)];

            // Apply to drive wheels
            foreach (var wheel in wheels)
            {
                if (wheel.isDriveWheel && wheel.wheelCollider != null)
                {
                    wheel.wheelCollider.motorTorque = motorTorque;
                }
            }
        }

        private void ApplySteering()
        {
            float steerAngle = currentSteering * maxSteerAngle;

            // Reduce steering at high speed
            float speedFactor = Mathf.Clamp01(1f - (currentSpeed / maxSpeed) * 0.5f);
            steerAngle *= speedFactor;

            foreach (var wheel in wheels)
            {
                if (wheel.isSteering && wheel.wheelCollider != null)
                {
                    wheel.wheelCollider.steerAngle = Mathf.Lerp(
                        wheel.wheelCollider.steerAngle,
                        steerAngle,
                        Time.deltaTime * steeringSensitivity * 10f
                    );
                }
            }
        }

        private void ApplyBrakes()
        {
            float brakeTorque = currentBrake * brakeForce * 1000f;

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider != null)
                {
                    if (currentBrake > 0)
                    {
                        wheel.wheelCollider.brakeTorque = brakeTorque;
                        wheel.wheelCollider.motorTorque = 0;
                    }
                    else
                    {
                        wheel.wheelCollider.brakeTorque = 0;
                    }
                }
            }
        }

        private void ApplyDownforce()
        {
            if (!isGrounded) return;

            float force = downforce * SpeedPercent;
            rb.AddForce(-transform.up * force);
        }

        private void UpdateGroundedState()
        {
            int groundedWheels = 0;
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider != null && wheel.wheelCollider.isGrounded)
                {
                    groundedWheels++;
                }
            }

            isGrounded = groundedWheels >= 2;
        }

        private void UpdateWheelVisuals()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider != null && wheel.wheelMesh != null)
                {
                    wheel.wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                    wheel.wheelMesh.position = pos;
                    wheel.wheelMesh.rotation = rot;
                }
            }
        }

        private void HandleAutomaticTransmission()
        {
            float rpm = GetEngineRPM();

            // Shift up
            if (rpm > 0.8f && currentGear < gearRatios.Length)
            {
                ShiftUp();
            }
            // Shift down
            else if (rpm < 0.3f && currentGear > 1)
            {
                ShiftDown();
            }
        }

        private float GetEngineRPM()
        {
            float wheelRPM = 0;
            int count = 0;

            foreach (var wheel in wheels)
            {
                if (wheel.isDriveWheel && wheel.wheelCollider != null)
                {
                    wheelRPM += wheel.wheelCollider.rpm;
                    count++;
                }
            }

            if (count > 0)
            {
                wheelRPM /= count;
            }

            return Mathf.Abs(wheelRPM) / 1000f; // Normalized
        }

        private void CheckDrifting()
        {
            if (!isGrounded)
            {
                if (isDrifting)
                {
                    isDrifting = false;
                    DriftEnded?.Invoke();
                    onDriftEnd?.Invoke();
                }
                return;
            }

            // Check sideways velocity
            float sidewaysVelocity = Vector3.Dot(rb.velocity, transform.right);
            bool wasDrifting = isDrifting;
            isDrifting = Mathf.Abs(sidewaysVelocity) > 5f && currentSpeed > 10f;

            if (isDrifting && !wasDrifting)
            {
                DriftStarted?.Invoke();
                onDriftStart?.Invoke();
            }
            else if (!isDrifting && wasDrifting)
            {
                DriftEnded?.Invoke();
                onDriftEnd?.Invoke();
            }
        }

        // Public methods
        public void StartEngine()
        {
            isEngineOn = true;
            EngineStarted?.Invoke();
            onEngineStart?.Invoke();
            Debug.Log("[Vehicle] Engine started");
        }

        public void StopEngine()
        {
            isEngineOn = false;

            // Stop all wheels
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider != null)
                {
                    wheel.wheelCollider.motorTorque = 0;
                    wheel.wheelCollider.brakeTorque = brakeForce * 100f;
                }
            }

            EngineStopped?.Invoke();
            onEngineStop?.Invoke();
            Debug.Log("[Vehicle] Engine stopped");
        }

        public void ShiftUp()
        {
            if (currentGear < gearRatios.Length)
            {
                currentGear++;
                GearChanged?.Invoke((int)currentGear);
                onGearChanged?.Invoke((int)currentGear);
            }
        }

        public void ShiftDown()
        {
            if (currentGear > 1)
            {
                currentGear--;
                GearChanged?.Invoke((int)currentGear);
                onGearChanged?.Invoke((int)currentGear);
            }
        }

        public void SetGear(int gear)
        {
            currentGear = Mathf.Clamp(gear, 1, gearRatios.Length);
            GearChanged?.Invoke((int)currentGear);
            onGearChanged?.Invoke((int)currentGear);
        }

        public void ApplyBoost(float force, float duration)
        {
            StartCoroutine(BoostCoroutine(force, duration));
        }

        private System.Collections.IEnumerator BoostCoroutine(float force, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                rb.AddForce(transform.forward * force, ForceMode.Acceleration);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        public void Flip()
        {
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.position += Vector3.up * 2f;
        }

        public float GetEnginePitch()
        {
            float rpm = GetEngineRPM();
            return Mathf.Lerp(minEnginePitch, maxEnginePitch, rpm);
        }

        private void OnDrawGizmosSelected()
        {
            // Center of mass
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(centerOfMass), 0.1f);

            // Wheels
            if (wheels != null)
            {
                foreach (var wheel in wheels)
                {
                    if (wheel.wheelCollider != null)
                    {
                        Gizmos.color = wheel.isDriveWheel ? Color.green : Color.yellow;
                        Gizmos.DrawWireSphere(wheel.wheelCollider.transform.position, wheel.wheelCollider.radius);
                    }
                }
            }
        }
    }

    [Serializable]
    public class WheelData
    {
        public WheelCollider wheelCollider;
        public Transform wheelMesh;
        public bool isDriveWheel = false;
        public bool isSteering = false;
    }
}
