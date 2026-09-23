using UnityEngine;

// Temporary Step 4 scaffolding. It proves SetTargetWorldRotation actually servos a physical joint
// before LegDrive exists to consume it — so that when the leg later does something wrong, the
// drive math is already known-good and only the ghost-reading is in question.
//
// Put it on a leg body, scrub testEuler in the inspector while playing, then delete the component
// once Step 5 lands. Nothing should ever depend on this.
[DefaultExecutionOrder(10)]
public class JointDriveTester : MonoBehaviour
{
    [Tooltip("World-space rotation to drive this body to. Not a joint angle — the joint limits " +
             "are in a different space and different units of meaning.")]
    [SerializeField] Vector3 testEuler;

    ConfigurableJoint joint;
    Transform connectedBody;
    Quaternion startLocalRotation;

    void Awake()
    {
        // Once LegDrive exists it owns every leg joint target, and a second writer on one joint is
        // a confusing thing to debug. This component is the disposable one, so it yields.
        if (FindAnyObjectByType<LegDrive>() != null)
        {
            Debug.LogWarning("JointDriveTester: LegDrive is in the scene, so this is now a second " +
                             "writer on a leg joint — disabling itself. Step 4 is over; delete it.", this);
            enabled = false;
            return;
        }

        joint = GetComponent<ConfigurableJoint>();

        if (joint == null || joint.connectedBody == null)
        {
            Debug.LogError("JointDriveTester: needs a ConfigurableJoint with a connected body.", this);
            enabled = false;
            return;
        }

        connectedBody = joint.connectedBody.transform;
        startLocalRotation = transform.localRotation;

        // Seed the field from the pose the body is already in, so entering play mode doesn't snap
        // the leg somewhere before you have touched anything. Scrub from here.
        testEuler = transform.rotation.eulerAngles;
    }

    void FixedUpdate() =>
        joint.SetTargetWorldRotation(Quaternion.Euler(testEuler), startLocalRotation, connectedBody);
}
