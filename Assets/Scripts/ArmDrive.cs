using UnityEngine;

// One per arm, and the only writer of its shoulder and elbow targets. The arm hangs in the heading's frame
// (gravity keeps it down while the chest leans), swings against the stride, and is thrown about by the sway.
// While its grab is held, it reaches along the camera's aim instead, on a stiffer drive (see HandGrab).
//
// The upper arm is authored turned ±90° about its length, so its joint's X limit is abduction.
[DefaultExecutionOrder(10)]   // after FootPlacement (0) has moved the feet
public class ArmDrive : MonoBehaviour
{
    // Fore/aft foot spread at which the stride swing is full, m.
    const float FullStrideSpread = 0.3f;
    // Walking speed at which the stride swing is full, m/s; it fades out below.
    const float FullSwingSpeed = 0.65f;

    [Header("References")]
    [SerializeField] PlayerController playerController;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] ConfigurableJoint shoulder;   // on the upper arm, connected to the torso
    [SerializeField] ConfigurableJoint elbow;      // on the forearm, connected to the upper arm
    [SerializeField] FootPlacement leftFoot, rightFoot;
    // −1 for the left arm, +1 for the right.
    [SerializeField] float side = -1f;

    [Header("Rest")]
    // Degrees the arm hangs out from the side, and the elbow's rest bend.
    [SerializeField] float restSplay = 8f;
    [SerializeField] float restElbowBend = 15f;

    [Header("Stride Swing")]
    // Degrees fore and aft at a full stride, opposite this side's leg.
    [SerializeField] float swingScale = 35f;
    [SerializeField] float swingResponse = 8f;

    [Header("Counterbalance")]
    // Degrees per unit of sideways sway: the arm on the side it pushes toward flies up, the other rises less.
    [SerializeField] float raiseScale = 60f;
    [SerializeField] float counterRaiseScale = 22f;
    // Degrees the arms reach fore/aft per unit of sway along the heading.
    [SerializeField] float reachScale = 16f;
    // How quickly the arm follows the sway, 1/s. Low reads as a slow drunken wave.
    [SerializeField] float counterResponse = 6f;
    // Most the arm rises from hanging, degrees.
    [SerializeField] float maxRaise = 100f;
    // Extra elbow bend per degree of raise.
    [SerializeField] float elbowBendPerRaise = 0.3f;

    [Header("Reach")]
    // Drive stiffness while reaching, as a multiple of the authored drive, so the arm can hold weight.
    [SerializeField] float reachStiffness = 4f;
    // Elbow bend while reaching, degrees.
    [SerializeField] float reachElbowBend = 10f;

    Transform torso, upperArm, forearm;
    Quaternion shoulderStart, elbowStart;
    // Each segment's rest rotation in the spawn heading's frame.
    Quaternion upperArmRest, forearmRest;
    JointDrive shoulderDrive, elbowDrive;
    float appliedStiffness = 1f;

    float strideSwing;
    Vector2 smoothedSway;

    void Awake()
    {
        if (!playerController || !balanceSensor || !shoulder || !elbow || !leftFoot || !rightFoot ||
            !shoulder.connectedBody || !elbow.connectedBody)
        {
            Debug.LogError("ArmDrive: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        torso = shoulder.connectedBody.transform;
        upperArm = shoulder.transform;
        forearm = elbow.transform;

        shoulderStart = shoulder.LocalRotation();
        elbowStart = elbow.LocalRotation();

        Quaternion fromSpawnHeading = Quaternion.Inverse(playerController.SpawnHeading);
        upperArmRest = fromSpawnHeading * upperArm.rotation;
        forearmRest = fromSpawnHeading * forearm.rotation;

        shoulderDrive = shoulder.slerpDrive;
        elbowDrive = elbow.slerpDrive;
    }

    void FixedUpdate()
    {
        bool fallen = playerController.IsFallen;
        CharacterInput input = playerController.CurrentInput;
        bool reaching = !fallen && (side < 0f ? input.GrabLeft : input.GrabRight);

        // Written only on change: assigning slerpDrive wakes the body.
        float stiffness = fallen ? playerController.FallenStiffness : reaching ? reachStiffness : 1f;
        if (!Mathf.Approximately(stiffness, appliedStiffness))
        {
            appliedStiffness = stiffness;
            shoulder.slerpDrive = shoulderDrive.Scaled(stiffness);
            elbow.slerpDrive = elbowDrive.Scaled(stiffness);
        }

        float dt = Time.fixedDeltaTime;
        Quaternion heading = playerController.Heading;
        Vector3 right = heading * Vector3.right;
        Vector3 forward = heading * Vector3.forward;

        // Opposite the legs: right foot ahead swings the left arm forward.
        float spread = Vector3.Dot(rightFoot.GroundPoint - leftFoot.GroundPoint, forward) / FullStrideSpread;
        float speedFade = Mathf.Clamp01(balanceSensor.CenterOfMassVelocity.Flat().magnitude / FullSwingSpeed);
        float swingTarget = fallen ? 0f : -side * Mathf.Clamp(spread, -1f, 1f) * swingScale * speedFade;
        strideSwing = Mathf.Lerp(strideSwing, swingTarget, Smoothing.Factor(swingResponse, dt));

        // The sway only: the player's input isn't something shoving the body.
        smoothedSway = Vector2.Lerp(smoothedSway, fallen ? Vector2.zero : playerController.Sway,
                                    Smoothing.Factor(counterResponse, dt));

        float abduction, pitch, bend;
        if (reaching)
        {
            // From hanging, swung forward to the aim: level at a level look, down when looking down.
            pitch = Mathf.Clamp(90f - input.LookPitch, 0f, 170f);
            abduction = 0f;
            bend = reachElbowBend;
        }
        else
        {
            // Positive when the sway pushes toward this arm's side.
            float towardThisSide = side * smoothedSway.x;
            float raise = Mathf.Min(maxRaise, raiseScale * Mathf.Max(0f, towardThisSide)
                                            + counterRaiseScale * Mathf.Max(0f, -towardThisSide));
            abduction = restSplay + raise;
            pitch = strideSwing + reachScale * smoothedSway.y;
            bend = restElbowBend + elbowBendPerRaise * raise;
        }

        // Out to this side about the heading's forward, then fore/aft about its right.
        Quaternion upperArmTarget = Quaternion.AngleAxis(-pitch, right)
                                  * Quaternion.AngleAxis(side * abduction, forward)
                                  * heading * upperArmRest;
        shoulder.SetTargetWorldRotation(upperArmTarget, shoulderStart, torso);

        // The elbow bends relative to where the upper arm actually is, so the forearm trails it.
        Quaternion upperArmOffset = upperArm.rotation * Quaternion.Inverse(heading * upperArmRest);
        Quaternion forearmTarget = upperArmOffset * Quaternion.AngleAxis(-bend, right) * heading * forearmRest;
        elbow.SetTargetWorldRotation(forearmTarget, elbowStart, upperArm);
    }
}
