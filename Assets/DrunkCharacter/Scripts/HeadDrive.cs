using UnityEngine;

// The only writer of the neck's target. Sober, the head holds the horizon and faces the heading whatever
// the chest does. Drunk, it rides the chest more (drunkHeadLean) and the neck goes soft and underdamped,
// so the head trails a lurch and swings past it.
[DefaultExecutionOrder(10)]   // after TorsoLean (−55) has set the chest target
public class HeadDrive : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    // On the head, connected to the torso.
    [SerializeField] ConfigurableJoint neck;

    [Header("Gaze")]
    // How far a fully drunk head gives up the horizon and rides the chest, 0–1.
    [SerializeField, Range(0f, 1f)] float drunkHeadLean = 0.6f;

    [Header("Neck")]
    // Drive spring, N·m/rad, sober and fully drunk. Lower lolls more; much lower and the head's flailing
    // knocks the body over.
    [SerializeField] float soberSpring = 120f;
    [SerializeField] float drunkSpring = 70f;
    // Damping ratio, sober and fully drunk. Below 1 overshoots.
    [SerializeField] float soberDamping = 1f;
    [SerializeField] float drunkDamping = 0.5f;

    Transform torso;
    // The head's rotation relative to the torso at spawn: the joint's start pose, and the drunk rest pose.
    Quaternion neckStart;
    // The head's rotation in the spawn heading's frame.
    Quaternion restInHeading;
    float maxForce;
    // Inertia about the neck, kg·m², for turning a damping ratio into a damper.
    float headInertia;
    // The drive last written; NaN forces the first write.
    float appliedSpring = float.NaN, appliedDamper = float.NaN;

    void Awake()
    {
        if (!playerController || !neck || !neck.connectedBody)
        {
            Debug.LogError("HeadDrive: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        torso = neck.connectedBody.transform;
        neckStart = neck.LocalRotation();
        restInHeading = Quaternion.Inverse(playerController.SpawnHeading) * neck.transform.rotation;
        maxForce = neck.slerpDrive.maximumForce;

        // About its centre plus the parallel-axis term to the neck, treating the head as near-spherical.
        Rigidbody head = neck.GetComponent<Rigidbody>();
        Vector3 tensor = head.inertiaTensor;
        float lever = (head.worldCenterOfMass - neck.transform.TransformPoint(neck.anchor)).magnitude;
        headInertia = Mathf.Max(tensor.x, tensor.z) + head.mass * lever * lever;
    }

    void FixedUpdate()
    {
        float drunk = Mathf.Clamp01(playerController.DrunkAmount);

        float spring = Mathf.Lerp(soberSpring, drunkSpring, drunk);
        float dampingRatio = Mathf.Lerp(soberDamping, drunkDamping, drunk);
        if (playerController.IsFallen) spring *= playerController.FallenStiffness;
        float damper = 2f * dampingRatio * Mathf.Sqrt(spring * headInertia);

        // Written only on change: assigning slerpDrive wakes the body.
        if (!Mathf.Approximately(spring, appliedSpring) || !Mathf.Approximately(damper, appliedDamper))
        {
            appliedSpring = spring;
            appliedDamper = damper;
            neck.slerpDrive = new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = maxForce,
            };
        }

        Quaternion levelGaze = playerController.Heading * restInHeading;
        Quaternion onChest = torso.rotation * neckStart;
        Quaternion target = Quaternion.Slerp(levelGaze, onChest, drunkHeadLean * drunk);
        neck.SetTargetWorldRotation(target, neckStart, torso);
    }
}
