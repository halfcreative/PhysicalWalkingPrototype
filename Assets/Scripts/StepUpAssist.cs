using UnityEngine;

// Helper forces, not joint physics, for climbing onto higher ground. The leg drives alone can't do it: the
// pelvis sags as a leg lifts high and the hip trails a fast target, so the foot stays too low to clear a
// step's edge, and the stance leg can't push the body up onto it.
//   Foot: during a step-up swing, each real foot is pulled toward its ankle target.
//   Pelvis: while the planted feet are either side of a step's face or a foot is stepping up, the pelvis is
//   lifted (upward only) toward standing height above the higher foot.
// Both are capped spring-dampers. Level ground and slopes are untouched. Host only, like the rest of the
// simulation.
[DefaultExecutionOrder(15)]   // after FootPlacement (0) has moved this tick's targets
public class StepUpAssist : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] BalanceController balanceController;
    [SerializeField] Rigidbody pelvis;
    [SerializeField] FootPlacement leftFoot, rightFoot;
    // The physical feet's ankle joints (on the foot, connected to the shin): the ankle is their anchor.
    [SerializeField] ConfigurableJoint leftAnkle, rightAnkle;

    [Header("Foot")]
    // N per metre the real ankle is off its target, N·s per m/s of foot velocity, and the most force, N.
    [SerializeField] float footSpring = 2000f;
    [SerializeField] float footDamper = 60f;
    [SerializeField] float footMaxForce = 250f;

    [Header("Pelvis")]
    // N per metre the pelvis is below standing height over the higher foot, N·s per m/s, and the most lift, N.
    [SerializeField] float pelvisSpring = 3000f;
    [SerializeField] float pelvisDamper = 300f;
    [SerializeField] float pelvisMaxLift = 250f;

    Rigidbody leftBody, rightBody;

    // Whether the pelvis lift acted this tick.
    public bool IsLifting { get; private set; }

    void Awake()
    {
        if (!playerController || !balanceController || !pelvis || !leftFoot || !rightFoot || !leftAnkle || !rightAnkle)
        {
            Debug.LogError("StepUpAssist: unassigned references — disabling.", this);
            enabled = false;
            return;
        }
        leftBody = leftAnkle.GetComponent<Rigidbody>();
        rightBody = rightAnkle.GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        IsLifting = false;
        if (playerController.IsFallen) return;
        AssistFoot(leftFoot, leftAnkle, leftBody);
        AssistFoot(rightFoot, rightAnkle, rightBody);
        LiftPelvis();
    }

    void AssistFoot(FootPlacement placement, ConfigurableJoint ankle, Rigidbody body)
    {
        if (!placement.IsSteppingUp) return;

        Vector3 ankleNow = ankle.transform.TransformPoint(ankle.anchor);
        Vector3 force = (placement.AnkleTarget - ankleNow) * footSpring - body.linearVelocity * footDamper;
        body.AddForceAtPosition(Vector3.ClampMagnitude(force, footMaxForce), ankleNow, ForceMode.Force);
    }

    void LiftPelvis()
    {
        float left = leftFoot.GroundPoint.y, right = rightFoot.GroundPoint.y;
        bool straddling = !leftFoot.IsStepping && !rightFoot.IsStepping &&
                          Ground.FaceBetween(leftFoot.GroundPoint, rightFoot.GroundPoint, Mathf.Min(left, right), out _);
        if (!straddling && !leftFoot.IsSteppingUp && !rightFoot.IsSteppingUp) return;

        // The higher planted foot; a stepping foot's ground point is mid-air, so it doesn't count.
        float higher = float.MinValue;
        if (!leftFoot.IsStepping) higher = Mathf.Max(higher, left);
        if (!rightFoot.IsStepping) higher = Mathf.Max(higher, right);
        if (higher == float.MinValue) return;

        float target = higher + balanceController.StandingHipHeight;
        float lift = (target - pelvis.position.y) * pelvisSpring - pelvis.linearVelocity.y * pelvisDamper;
        if (lift <= 0f) return;
        pelvis.AddForce(Vector3.up * Mathf.Min(lift, pelvisMaxLift), ForceMode.Force);
        IsLifting = true;
    }
}
