using UnityEngine;

public static class QuaternionExtensions
{
    // The same rotation with w >= 0, so the angles below stay within ±180°.
    public static Quaternion WithPositiveW(this Quaternion q) =>
        q.w < 0f ? new Quaternion(-q.x, -q.y, -q.z, -q.w) : q;

    // Signed rotation about local X, degrees, ignoring the other axes. A knee's bend.
    public static float AngleAboutX(this Quaternion q)
    {
        q = q.WithPositiveW();
        return 2f * Mathf.Atan2(q.x, q.w) * Mathf.Rad2Deg;
    }

    // Signed rotation about local Y, degrees, ignoring the other axes. A leg bone's twist.
    public static float AngleAboutY(this Quaternion q)
    {
        q = q.WithPositiveW();
        return 2f * Mathf.Atan2(q.y, q.w) * Mathf.Rad2Deg;
    }

    // q with its twist about local Y (a leg bone's long axis) removed: the swing that's left.
    public static Quaternion WithoutTwistY(this Quaternion q)
    {
        float length = Mathf.Sqrt(q.y * q.y + q.w * q.w);
        if (length < 1e-6f) return q;
        Quaternion twist = new(0f, q.y / length, 0f, q.w / length);
        return q * Quaternion.Inverse(twist);
    }
}
