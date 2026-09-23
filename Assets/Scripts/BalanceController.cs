using UnityEngine;

[DefaultExecutionOrder(-50)]
public class BalanceController : MonoBehaviour
{
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] FootController leftFoot;
    [SerializeField] FootController rightFoot;

    [SerializeField] float maxAngle = 15f;
    [SerializeField] float footCenterZ = 0f;
    [SerializeField] float manualPitch = 0f; // Temporary
    [SerializeField] float gain = 0f;
    [SerializeField] float standingHipHeight = 0.85f;

    // Where PlayerRig puts the ghost hips, measured up from the support plane.
    public float StandingHipHeight => standingHipHeight;

    // The ankle strategy: the small trim that handles COM errors too small to be worth a step.
    // Published rather than applied, so that LegDrive stays the only thing writing an ankle joint.
    // It composes into the foot rotation there.
    public Quaternion AnkleTrim { get; private set; } = Quaternion.identity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 local = transform.InverseTransformPoint(balanceSensor.COMPrediction);
        float error = local.z - footCenterZ;

        // Debug.Log("COM Velocity" + balanceSensor.COMVelocity);
        // Debug.Log("error: " + error);
        float pitch = Mathf.Clamp(gain * error, -maxAngle, maxAngle) + manualPitch;
        // Debug.Log("pitch: " + pitch);

        AnkleTrim = Quaternion.Euler(pitch, 0f, 0f);

    }
}
