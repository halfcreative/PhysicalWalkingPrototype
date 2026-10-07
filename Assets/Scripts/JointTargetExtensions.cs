using UnityEngine;

// A ConfigurableJoint's targetRotation is not the rotation you want. It is expressed in the
// joint's own basis, inverted, and relative to the pose the joint was authored in. This converts
// a plain world-space rotation into that, so the formula has to be right in exactly one place.
//
// On this rig every joint has axis = X and secondaryAxis = Y, which makes the joint basis
// identity. That is why there is no worldToJointSpace term here, unlike every copy of the
// community SetTargetRotationLocal helper. Change either axis on a joint and this stops holding.
public static class JointTargetExtensions
{
    // startLocalRotation is the driven body's rotation relative to its connected body, captured at
    // Awake before physics has moved anything. It is identity on this rig, so it currently cancels out — pass it anyway,
    // so that authoring a real bind pose later doesn't silently make this wrong.
    //
    // connectedBody must be read fresh each tick, never cached. If the thigh is lagging its
    // target, a shin target computed against where the thigh ACTUALLY is still puts the shin in
    // the right world place; the chain corrects itself instead of compounding error down the leg.
    public static void SetTargetWorldRotation(
        this ConfigurableJoint joint,
        Quaternion desiredWorld,
        Quaternion startLocalRotation,
        Transform connectedBody)
    {
        Quaternion desiredLocal = Quaternion.Inverse(connectedBody.rotation) * desiredWorld;
        joint.targetRotation = Quaternion.Inverse(desiredLocal) * startLocalRotation;
    }
}
