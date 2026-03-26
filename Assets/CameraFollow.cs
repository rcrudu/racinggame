using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    public Transform carTarget;
    public Rigidbody carRb; 

    [Header("Settings")]
    public Vector3 moveOffset = new Vector3(0, 5, -10); 
    public Vector3 lookAtOffset = new Vector3(0, 1, 0); 

    [Header("Smoothing (Lower is Faster)")]
    public float followSmoothTime = 0.1f;
    public float rotSmoothness = 5f;

    [Header("Speed Effects")]
    public bool useSpeedFX = true;
    public float baseFOV = 60f;
    public float maxFOV = 85f;
    public float maxSpeedForFX = 150f; 

    private Vector3 currentVelocity;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (carRb == null && carTarget != null)
            carRb = carTarget.GetComponent<Rigidbody>();
    }


    void LateUpdate()
    {
        if (carTarget == null) return;

        FollowTarget();
        HandleSpeedFX();
    }

    [Header("Lag Compensation")]
    [Tooltip("Pushes the target point forward to cancel out lag. Try 0.1 to 0.5")]
    public float velocityDamping = 0.2f;

    void FollowTarget()
    {
        Vector3 targetPos = carTarget.TransformPoint(moveOffset);

        if (carRb != null)
        {
            targetPos += carRb.velocity * velocityDamping;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, followSmoothTime);

        Vector3 direction = (carTarget.position + lookAtOffset) - transform.position;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotSmoothness * Time.deltaTime);
        }
    }

    void HandleSpeedFX()
    {
        if (!useSpeedFX || cam == null || carRb == null) return;

        float forwardSpeed = Vector3.Dot(carTarget.forward, carRb.velocity) * 3.6f;

        float speedFactor = Mathf.InverseLerp(0, maxSpeedForFX, Mathf.Abs(forwardSpeed));

        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedFactor);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 2f);
    }
}