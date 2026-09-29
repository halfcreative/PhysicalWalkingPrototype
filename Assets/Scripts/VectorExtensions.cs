using UnityEngine;

public static class VectorExtensions
{
    /// <summary>
    /// Projects a vector onto the horizontal XZ plane by zeroing the Y component.
    /// </summary>
    public static Vector3 Flat(this Vector3 v) => new(v.x, 0f, v.z);
}
