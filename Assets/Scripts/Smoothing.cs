using UnityEngine;

public static class Smoothing
{
    // Lerp weight that closes a gap at `rate` per second, independent of the tick length.
    public static float Factor(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);
}
