using UnityEngine;

// One per leg, and the ONLY writer of that leg's three joint targets. It reads the pose the ghost
// was solved into and hands it to the drives — that handoff is the whole of "the physical leg
// follows the ghost".
//
// The ghost pose read here was solved during the PREVIOUS tick: the Animator evaluates after this
// component runs. That tick of latency is deliberate (Part 1) — the alternative is guessing at
// where Unity puts the animation pass relative to ours. The joint target is a setpoint, not a
// teleport, so a tick-old target on a foot moving at 0.3 m/s is about 6 mm of error.
//
// PlayerRig places the ghost hips before this runs (−40 vs 10), so the ghost bones' WORLD
// rotations are current with the body even though their joint angles are a tick old. The leg's
// orientation never lags the pelvis.
[DefaultExecutionOrder(10)]
public class LegDrive : MonoBehaviour
{
    [Header("Physical")]
    [SerializeField] Transform thigh, shin, foot;

    [Header("Ghost")]
    [SerializeField] Transform ghostThigh, ghostShin, ghostFoot;

    [Header("Trim")]
    [SerializeField] BalanceController balanceController;

    ConfigurableJoint hip, knee, ankle;
    Quaternion thighStart, shinStart, footStart;

    void Awake()
    {
        if (!ReferencesAssigned())
        {
            Debug.LogError("LegDrive: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        hip = thigh.GetComponent<ConfigurableJoint>();
        knee = shin.GetComponent<ConfigurableJoint>();
        ankle = foot.GetComponent<ConfigurableJoint>();

        if (!JointsUsable())
        {
            enabled = false;
            return;
        }

        // Captured before physics has moved anything, which is what makes these the start pose the
        // drives measure against. Identity on this rig, so they currently cancel — see
        // JointTargetExtensions for why they are passed anyway.
        thighStart = thigh.localRotation;
        shinStart = shin.localRotation;
        footStart = foot.localRotation;
    }

    // The entire component. Each joint gets the ghost bone's world rotation, resolved against the
    // connected body's CURRENT pose rather than a cached one, so a lagging thigh doesn't push the
    // shin's target out of place as well.
    void FixedUpdate()
    {
        hip.SetTargetWorldRotation(
            ghostThigh.rotation, thighStart, hip.connectedBody.transform);

        knee.SetTargetWorldRotation(
            ghostShin.rotation, shinStart, knee.connectedBody.transform);

        // The ankle trim composes HERE, on the right of the ghost rotation so it applies about the
        // foot's own axis. Composing it into the one write is what keeps this the sole writer of
        // the ankle — BalanceController decides the trim, it does not apply it.
        ankle.SetTargetWorldRotation(
            ghostFoot.rotation * balanceController.AnkleTrim, footStart, ankle.connectedBody.transform);
    }

    bool ReferencesAssigned() =>
        thigh && shin && foot && ghostThigh && ghostShin && ghostFoot && balanceController;

    bool JointsUsable()
    {
        (ConfigurableJoint joint, Transform body)[] chain =
            { (hip, thigh), (knee, shin), (ankle, foot) };
        bool ok = true;

        foreach ((ConfigurableJoint joint, Transform body) in chain)
        {
            if (joint == null)
            {
                Debug.LogError($"LegDrive: {body.name} has no ConfigurableJoint.", body);
                ok = false;
            }
            else if (joint.connectedBody == null)
            {
                Debug.LogError($"LegDrive: {body.name}'s joint has no connected body — there is " +
                               "nothing to resolve its target against.", body);
                ok = false;
            }
        }

        return ok;
    }
}
