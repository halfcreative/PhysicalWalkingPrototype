using UnityEngine;

public static class JointExtensions
{
    // Sets the joint's target so its body sits at `desiredWorld` relative to `referenceFrame`. The drive
    // applies that relative rotation against the real connected body, so passing the connected body reaches
    // the world rotation, while passing another frame (a ghost bone) holds a joint angle instead.
    // targetRotation is inverted and relative to the start pose; this assumes axis = X and
    // secondaryAxis = Y (an identity joint basis), as on every joint here.
    public static void SetTargetWorldRotation(
        this ConfigurableJoint joint,
        Quaternion desiredWorld,
        Quaternion startLocalRotation,
        Transform referenceFrame)
    {
        Quaternion desiredLocal = Quaternion.Inverse(referenceFrame.rotation) * desiredWorld;
        joint.targetRotation = Quaternion.Inverse(desiredLocal) * startLocalRotation;
    }

    // The body's rotation relative to its connected body. Capture at Awake, before physics moves anything,
    // to get the start pose SetTargetWorldRotation measures against.
    public static Quaternion LocalRotation(this ConfigurableJoint joint) =>
        Quaternion.Inverse(joint.connectedBody.transform.rotation) * joint.transform.rotation;

    // Spring scaled by `scale`, damper by its square root, which keeps the damping ratio.
    public static JointDrive Scaled(this JointDrive drive, float scale)
    {
        drive.positionSpring *= scale;
        drive.positionDamper *= Mathf.Sqrt(scale);
        return drive;
    }
}
