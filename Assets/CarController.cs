using UnityEngine;
using System;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public enum Axel { Front, Rear }
    public enum DriveType { FWD, RWD, AWD }
    public enum ControlMode { Keyboard, Buttons };

    [Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public Axel axel;
    }

    [Header("Configuration")]
    public CarPreset currentPreset;
    public ControlMode control;
    public List<Wheel> wheels;
    private Rigidbody carRb;

    [Header("Drift Settings")]
    public float minSpeedForDrift = 15f;
    public float enterDriftAngle = 25f;
    public float exitDriftAngle = 15f;

    [Header("Debug / State")]
    public int currentGearIndex = 1;
    public float currentRPM;
    public float currentSpeedKPH;
    public float wheelSpeedKPH;
    public bool isSliding;
    public float driftAngle;

    private float moveInput;
    private float steerInput;
    private bool isBraking;
    private bool isHandbraking;
    private bool isClutching;

    private bool wasClutchingLastFrame;
    private float clutchImpulseTimer = 0f;
    private float debugTimer;

    void Start() //1
    {
        carRb = GetComponent<Rigidbody>();
        if (currentPreset != null)
        {
            carRb.mass = currentPreset.carMass;
            ApplyPreset();
        }
        currentGearIndex = 1;
    }

    void OnValidate()
    {
        if (carRb != null && currentPreset != null) ApplyPreset();
    }

    public void ApplyPreset() //3
    {
        if (currentPreset == null) return;
        carRb.centerOfMass = currentPreset.centerOfMass;

        foreach (var wheel in wheels)
        {
            CarPreset.WheelSettings settings = (wheel.axel == Axel.Front) ? currentPreset.frontWheels : currentPreset.rearWheels;
            WheelCollider wc = wheel.wheelCollider;

            wc.suspensionDistance = settings.suspensionDistance;
            JointSpring spring = wc.suspensionSpring;
            spring.spring = settings.springRate;
            spring.damper = settings.damper;
            wc.suspensionSpring = spring;

            WheelFrictionCurve fwdFriction = wc.forwardFriction;
            fwdFriction.stiffness = settings.forwardGrip;
            wc.forwardFriction = fwdFriction;

            WheelFrictionCurve sideFriction = wc.sidewaysFriction;
            sideFriction.stiffness = settings.sidewaysGrip;
            wc.sidewaysFriction = sideFriction;
        }
    }

    void Update() //4
    {
        GetInputs();
        CalculateEngineRPM();
        AnimateWheelMeshes();

        // limit debug log
        debugTimer += Time.deltaTime;
        if (debugTimer >= 0.2f)
        {
            DebugInfo();
            debugTimer = 0;
        }
    }

    void FixedUpdate()
    {
        if (currentPreset == null) return;

        currentSpeedKPH = Mathf.Round(carRb.velocity.magnitude * 3.6f);

        CalculateDriftState();
        HandleGears();
        Move(); //5
        Steer();
        Brake();
        HandleDriftPhysics();
    }

    void GetInputs()
    {
        if (control == ControlMode.Keyboard)
        {
            moveInput = Input.GetAxis("Vertical");
            steerInput = Input.GetAxis("Horizontal");
            isHandbraking = Input.GetKey(KeyCode.Space);
            isClutching = Input.GetKey(KeyCode.X);

            if (currentPreset.transmissionMode == CarPreset.TransmissionMode.Manual)
            {
                if (Input.GetKeyDown(KeyCode.LeftShift)) ShiftUp();
                if (Input.GetKeyDown(KeyCode.LeftAlt)) ShiftDown();
            }

            //braking
            bool movingForward = Vector3.Dot(transform.forward, carRb.velocity) > 0.5f;
            isBraking = (movingForward && moveInput < 0);
        }
    }

    void ShiftUp()
    {
        if (currentGearIndex > 0 && currentGearIndex < currentPreset.gears.Count - 1)
            currentGearIndex++;
    }

    void ShiftDown()
    {
        if (currentGearIndex > 1)
            currentGearIndex--;
    }

    void CalculateEngineRPM()
    {
        float wheelRPMsum = 0;
        int driveWheelCount = 0;
        foreach (var wheel in wheels)
        {
            if (IsDriveWheel(wheel))
            {
                wheelRPMsum += wheel.wheelCollider.rpm;
                driveWheelCount++;
            }
        }
        float avgWheelRPM = (driveWheelCount > 0) ? Mathf.Abs(wheelRPMsum / driveWheelCount) : 0;

        // convert RPM -> KPH 
        float wheelCircumference = 2 * Mathf.PI * wheels[0].wheelCollider.radius;
        wheelSpeedKPH = Mathf.Abs(((avgWheelRPM * wheelCircumference) * 60) / 1000f);

        if (isClutching)
        {
            // engine revs when decoupled
            float gasTarget = (Mathf.Abs(moveInput) > 0) ? currentPreset.redlineRPM : currentPreset.idleRPM;
            float changeRate = (Mathf.Abs(moveInput) > 0) ? currentPreset.engineInertia : currentPreset.engineDrag;
            currentRPM = Mathf.MoveTowards(currentRPM, gasTarget, changeRate * Time.deltaTime);
        }
        else
        {
            // lock engine RPM based on current gear ratio
            float gearTopSpeed = Mathf.Max(1f, currentPreset.gears[currentGearIndex].topSpeed);
            float targetRPM = (wheelSpeedKPH / gearTopSpeed) * currentPreset.redlineRPM;

            // prevent RPM from bogging down completely during a slide
            if (isSliding && Mathf.Abs(moveInput) > 0)
            {
                targetRPM = Mathf.Max(targetRPM, currentPreset.redlineRPM * 0.6f);
            }

            currentRPM = Mathf.Lerp(currentRPM, targetRPM, 10.0f * Time.deltaTime);
            currentRPM = Mathf.Clamp(currentRPM, currentPreset.idleRPM, currentPreset.redlineRPM);
        }
    }

    void HandleGears()
    {
        if (currentSpeedKPH < 2f)
        {
            if (moveInput < -0.1f) currentGearIndex = 0;
            else if (moveInput > 0.1f && currentGearIndex == 0) currentGearIndex = 1;
        }

        if (currentPreset.transmissionMode == CarPreset.TransmissionMode.Automatic && !isClutching)
        {
            if (currentGearIndex >= 1)
            {
                if (currentRPM > currentPreset.redlineRPM * 0.95f && currentGearIndex < currentPreset.gears.Count - 1)
                    currentGearIndex++;
                else if (currentRPM < currentPreset.redlineRPM * 0.4f && currentGearIndex > 1)
                    currentGearIndex--;
            }
        }
    }

    void Move() //5
    {
        float accel = currentPreset.GetAcceleration(currentGearIndex, currentRPM);
        float totalTorque = (currentPreset.carMass * accel) * wheels[0].wheelCollider.radius;

        // clutch kick impulse
        if (wasClutchingLastFrame && !isClutching && currentRPM > 3000)
            clutchImpulseTimer = 0.5f;

        wasClutchingLastFrame = isClutching;

        if (clutchImpulseTimer > 0)
        {
            totalTorque *= 2.5f;
            clutchImpulseTimer -= Time.fixedDeltaTime;
        }

        float torquePerWheel = IsDriveWheelCount() > 0 ? totalTorque / IsDriveWheelCount() : 0;

        foreach (var wheel in wheels)
        {
            if (IsDriveWheel(wheel))
            {
                if (isClutching || isBraking)
                    wheel.wheelCollider.motorTorque = 0;
                else
                    wheel.wheelCollider.motorTorque = torquePerWheel * moveInput;
            }
        }
    }

    void Brake()
    {
        float pedalTorque = currentPreset.calculatedBrakeTorque;
        float handbrakeTorque = currentPreset.calculatedBrakeTorque * 2.0f;

        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.brakeTorque = 0;

            if (isBraking)
                wheel.wheelCollider.brakeTorque = pedalTorque;

            if (isHandbraking && wheel.axel == Axel.Rear)
                wheel.wheelCollider.brakeTorque += handbrakeTorque;

            // apply drag to simulate engine braking
            if (moveInput == 0 && !isBraking && !isHandbraking && !isClutching)
            {
                wheel.wheelCollider.brakeTorque = pedalTorque * 0.05f;
            }
        }
    }

    void Steer()
    {
        float speedFactor = isSliding ? 1.0f : currentPreset.steeringCurve.Evaluate(currentSpeedKPH);

        foreach (var wheel in wheels)
        {
            if (wheel.axel == Axel.Front)
            {
                float targetAngle = steerInput * currentPreset.turnSensitivity * currentPreset.maxSteerAngle * speedFactor;

                // counter-steer assist gives extra angle during drift
                if (isSliding) targetAngle *= 1.1f;

                wheel.wheelCollider.steerAngle = targetAngle;
            }
        }
    }

    void CalculateDriftState()
    {
        if (carRb.velocity.sqrMagnitude > 0.5f)
        {
            driftAngle = Vector3.Angle(transform.forward, carRb.velocity);
        }
        else
        {
            driftAngle = 0f;
        }

        // slide via burnout, clutch kick, or handbrake
        bool isWheelSpinning = (wheelSpeedKPH > currentSpeedKPH + 15f) && (Mathf.Abs(moveInput) > 0);
        bool isClutchKicking = (clutchImpulseTimer > 0);

        if (isWheelSpinning || isClutchKicking || (isHandbraking && currentSpeedKPH > 10f))
        {
            isSliding = true;
        }
        // natural inertia slide 
        else if (currentSpeedKPH > minSpeedForDrift)
        {
            if (!isSliding && driftAngle > enterDriftAngle) isSliding = true;
            else if (isSliding && driftAngle < exitDriftAngle) isSliding = false;
        }
        else
        {
            isSliding = false;
        }
    }

    void HandleDriftPhysics()
    {
        bool isReverseEntry = isSliding && driftAngle > 100f;

        if (isHandbraking)
        {
            SetGrip(currentPreset.frontWheels.sidewaysGrip * 1.0f, Axel.Front);
            SetGrip(currentPreset.rearWheels.sidewaysGrip * 0.5f, Axel.Rear);
        }
        else if (isSliding || clutchImpulseTimer > 0)
        {
            // reverse entry
            float frontGrip = currentPreset.frontWheels.sidewaysGrip;
            if (isReverseEntry) frontGrip *= 0.5f;
            SetGrip(frontGrip, Axel.Front);

            float driftGrip = currentPreset.rearWheels.sidewaysGrip * currentPreset.driftGripMultiplier;
            SetGrip(driftGrip, Axel.Rear);
        }
        else
        {
            SetGrip(currentPreset.frontWheels.sidewaysGrip, Axel.Front);
            SetGrip(currentPreset.rearWheels.sidewaysGrip, Axel.Rear);
        }
    }

    void SetGrip(float targetStiffness, Axel targetAxel)
    {
        foreach (var wheel in wheels)
        {
            if (wheel.axel == targetAxel)
            {
                WheelFrictionCurve friction = wheel.wheelCollider.sidewaysFriction;
                friction.stiffness = Mathf.Lerp(friction.stiffness, targetStiffness, 10f * Time.fixedDeltaTime);
                wheel.wheelCollider.sidewaysFriction = friction;
            }
        }
    }

    bool IsDriveWheel(Wheel wheel)
    {
        switch (currentPreset.driveType)
        {
            case DriveType.AWD: return true;
            case DriveType.RWD: return wheel.axel == Axel.Rear;
            case DriveType.FWD: return wheel.axel == Axel.Front;
            default: return false;
        }
    }

    int IsDriveWheelCount()
    {
        switch (currentPreset.driveType)
        {
            case DriveType.AWD: return 4;
            case DriveType.RWD: return 2;
            case DriveType.FWD: return 2;
            default: return 4;
        }
    }

    void AnimateWheelMeshes()
    {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheel.wheelModel.transform.position = pos;
            wheel.wheelModel.transform.rotation = rot;
        }
    }

    void DebugInfo()
    {
        string gearLabel = (currentGearIndex == 0) ? "R" : currentGearIndex.ToString();
        Debug.Log($"Gear: {gearLabel} | RPM: {(int)currentRPM} | Speed: {currentSpeedKPH}");
    }

    void OnDrawGizmos()
    {
        if (currentPreset != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(currentPreset.centerOfMass), 0.3f);
        }
    }
}