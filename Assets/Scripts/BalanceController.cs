using UnityEngine;

public class BalanceController : MonoBehaviour
{
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] FootController leftFoot;
    [SerializeField] FootController rightFoot;

    [SerializeField] float maxAngle = 15f;
    [SerializeField] float footCenterZ = 0f;
    [SerializeField] float manualPitch = 0f; // Temporary
    [SerializeField] float gain = 0f;
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

        leftFoot.SetAnkleTarget(pitch);
        rightFoot.SetAnkleTarget(pitch);

    }
}
