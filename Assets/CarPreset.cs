using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MustangGT_Preset", menuName = "DOTC/Spec Preset")]
public class CarPreset : ScriptableObject
{
    [Header("Engine Setup")] //2
    public float idleRPM = 800f;
    public float redlineRPM = 6100f;

    [Tooltip("Torque in Newton Meters")]
    public AnimationCurve engineTorqueCurve = new AnimationCurve(
        new Keyframe(0, 280),
        new Keyframe(2000, 360),
        new Keyframe(3500, 405),
        new Keyframe(4600, 417),
        new Keyframe(6000, 372),
        new Keyframe(6800, 100)
    );

    public AnimationCurve powerCurveDisplay = new AnimationCurve();
    public float peakHorsepower;
    public float peakTorque;

    [Header("Transmission Setup")]
    public float finalDriveRatio = 3.55f;
    public float[] gearRatios = new float[] { 3.38f, 2.00f, 1.32f, 1.00f, 0.67f };

    [Tooltip("Radius in Meters")]
    public float calibratedWheelRadius = 0.345f;

    public enum TransmissionMode { Automatic, Manual }

    [Header("Physical Properties")]
    public TransmissionMode transmissionMode = TransmissionMode.Manual;

    public float engineInertia = 6500f;
    public float engineDrag = 2500f;
    public float carMass = 1500f;

    [Header("Braking & Handling")]
    public float brakeDistance = 35f;
    public float calculatedBrakeTorque;

    [Header("Steering & Drift")]
    public float turnSensitivity = 0.8f;
    public float maxSteerAngle = 40.0f;
    public Vector3 centerOfMass = new Vector3(0, -0.3f, 0.23f);
    public CarController.DriveType driveType = CarController.DriveType.RWD;

    public AnimationCurve steeringCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(100f, 0.5f), new Keyframe(200f, 0.3f));

    [Range(0.1f, 1f)]
    public float driftGripMultiplier = 0.55f;

    [System.Serializable]
    public struct WheelSettings
    {
        public float suspensionDistance;
        public float springRate;
        public float damper;
        public float forwardGrip;
        public float sidewaysGrip;
    }

    public WheelSettings frontWheels = new WheelSettings
    {
        suspensionDistance = 0.35f,
        springRate = 32000,
        damper = 3500,
        forwardGrip = 1.0f,
        sidewaysGrip = 1.0f
    };

    public WheelSettings rearWheels = new WheelSettings
    {
        suspensionDistance = 0.35f,
        springRate = 28000,
        damper = 3500,
        forwardGrip = 1.2f,
        sidewaysGrip = 1.0f
    };

    [HideInInspector]
    public List<GearData> gears = new List<GearData>();

    [System.Serializable]
    public struct GearData
    {
        public string name;
        public float topSpeed;
        public List<RPMPoint> accelerationCurve;
    }

    [System.Serializable]
    public struct RPMPoint
    {
        public float rpm;
        public float acceleration;
        public RPMPoint(float r, float a) { rpm = r; acceleration = a; }
    }

    private void OnValidate()
    {
        BakeTorqueToAcceleration();
    }

    private void OnEnable()
    {
        if (gears == null || gears.Count == 0) BakeTorqueToAcceleration();
    }

    public void BakeTorqueToAcceleration()
    {
        gears.Clear();
        CalculateDyno();

        // required to stop the car from 100 kph (27.78 m/s) over the target brakeDistance.
        float targetSpeedMs = 27.78f;
        float requiredDecel = (targetSpeedMs * targetSpeedMs) / (2 * Mathf.Max(1f, brakeDistance));
        calculatedBrakeTorque = (carMass * requiredDecel * calibratedWheelRadius);

        GenerateGear(-1, 3.5f, "Reverse");

        for (int i = 0; i < gearRatios.Length; i++)
        {
            GenerateGear(i + 1, gearRatios[i], $"Gear {i + 1}");
        }
    }

    void GenerateGear(int gearIndex, float gearRatio, string gearName)
    {
        GearData newGear = new GearData();
        newGear.name = gearName;
        newGear.accelerationCurve = new List<RPMPoint>();

        float rpmStep = 250f;
        for (float rpm = idleRPM; rpm <= redlineRPM; rpm += rpmStep)
        {
            // raw engine torque -> forward acceleration 
            // bakes drivetrain math 
            float engineTorque = engineTorqueCurve.Evaluate(rpm);
            float wheelTorque = engineTorque * gearRatio * finalDriveRatio;
            float force = wheelTorque / calibratedWheelRadius;
            float acceleration = force / carMass;

            newGear.accelerationCurve.Add(new RPMPoint(rpm, acceleration));
        }

        // calculates theoretical max speed in this gear
        float circumference = 2 * Mathf.PI * calibratedWheelRadius;
        float totalRatio = gearRatio * finalDriveRatio;
        if (totalRatio == 0) totalRatio = 1;
        float speedMetersPerMin = redlineRPM * circumference / totalRatio;
        newGear.topSpeed = (speedMetersPerMin * 60) / 1000f;

        gears.Add(newGear);
    }

    void CalculateDyno()
    {
        peakTorque = 0;
        peakHorsepower = 0;
        powerCurveDisplay = new AnimationCurve();

        for (float rpm = 0; rpm <= redlineRPM; rpm += 100f)
        {
            float torque = engineTorqueCurve.Evaluate(rpm);

            float hp = (torque * rpm) / 7120.9f;

            if (torque > peakTorque) peakTorque = torque;
            if (hp > peakHorsepower) peakHorsepower = hp;

            powerCurveDisplay.AddKey(rpm, hp);
        }
    }

    public float GetAcceleration(int gearIndex, float currentRPM)
    {
        int listIndex = gearIndex;
        if (gearIndex == 0) listIndex = 0;

        if (listIndex < 0 || listIndex >= gears.Count) return 0f;

        var points = gears[listIndex].accelerationCurve;
        if (points == null || points.Count == 0) return 0f;

        if (currentRPM <= points[0].rpm) return points[0].acceleration;
        if (currentRPM >= points[points.Count - 1].rpm) return points[points.Count - 1].acceleration;

        // Linearly interpolates between our baked 250-RPM steps to get a smooth acceleration value
        for (int i = 0; i < points.Count - 1; i++)
        {
            RPMPoint pA = points[i];
            RPMPoint pB = points[i + 1];
            if (currentRPM >= pA.rpm && currentRPM < pB.rpm)
            {
                float t = (currentRPM - pA.rpm) / (pB.rpm - pA.rpm);
                return Mathf.Lerp(pA.acceleration, pB.acceleration, t);
            }
        }
        return 0f;
    }
}