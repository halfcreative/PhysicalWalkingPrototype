using System;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class BalanceController : MonoBehaviour
{
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] FootController leftFoot;
    [SerializeField] FootController rightFoot;
    [SerializeField] Rigidbody pelvis;

    [SerializeField] float maxAngle = 15f;
    [SerializeField] float footCenterZ = 0f;
    [SerializeField] float gain = 0f;
    [SerializeField] float standingHipHeight = 0.85f;

    // Real N·m per radian and N·m·s per radian, applied with ForceMode.Force. Gravity tips the upper
    // body over the hips at ~103 N·m/rad, and the hip drives don't resist that, so the spring must
    // clear it with margin. 70 is critical damping for the ~300 left over against the ~4.1 kg·m²
    // upper body. See resume-here §5.1.
    [SerializeField] float uprightSpring = 400f;
    [SerializeField] float uprightDamper = 70f;

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
        float pitch = Mathf.Clamp(gain * error, -maxAngle, maxAngle);
        // Debug.Log("pitch: " + pitch);

        AnkleTrim = Quaternion.Euler(pitch, 0f, 0f);

        HoldPelvisUpright();
    }

    // A PD torque that tips the pelvis back toward vertical. AddTorque is legitimate here and nowhere
    // else in this system: the pelvis is the root body, with no joint above it to absorb the torque.
    // Anything behind a joint goes through targetRotation.
    //
    // Reads the Rigidbody's rotation rather than the transform's, which can be interpolated away from
    // the physics pose. Cross gives sin(tilt) about the correcting axis, so it weakens past 90° — by
    // then the character has already fallen, so that's fine for now.
    void HoldPelvisUpright()
    {
        Vector3 axisErr = Vector3.Cross(pelvis.rotation * Vector3.up, Vector3.up);
        pelvis.AddTorque(axisErr * uprightSpring - pelvis.angularVelocity * uprightDamper, ForceMode.Force);
    }
}
