using UnityEngine;

public static class JointExtensions
{
    // Drives the joint toward a world-space rotation. targetRotation is inverted and relative to the start
    // pose; this assumes axis = X and secondaryAxis = Y (an identity joint basis), as on every joint here.
    // Pass the connected body's current transform, never a cached pose, so a lagging parent doesn't push
    // the child's target out of place.
    public static void SetTargetWorldRotation(
        this ConfigurableJoint joint,
        Quaternion desiredWorld,
        Quaternion startLocalRotation,
        Transform connectedBody)
    {
        Quaternion desiredLocal = Quaternion.Inverse(connectedBody.rotation) * desiredWorld;
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
