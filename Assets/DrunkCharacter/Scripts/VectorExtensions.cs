using UnityEngine;

public static class VectorExtensions
{
    // Projects onto the horizontal XZ plane.
    public static Vector3 Flat(this Vector3 v) => new(v.x, 0f, v.z);

    // Yaw-only rotation facing along this vector's horizontal part; identity if it's vertical.
    public static Quaternion ToYawRotation(this Vector3 forward)
    {
        forward = forward.Flat();
        return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Quaternion.identity;
    }
}
