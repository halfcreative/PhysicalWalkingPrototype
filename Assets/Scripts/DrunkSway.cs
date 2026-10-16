using UnityEngine;

/// <summary>
/// The involuntary drunk sway — one body-local 2D force vector (x = right, y = forward) the controller
/// sums with player input. The project's single noise source: every drunk effect derives from the
/// resultant downstream, never from a second generator. Polar rather than Cartesian so direction and
/// magnitude are separately tunable — rotation rate governs how much time the player has to read a
/// lurch, and needs its own knob. Plain C#: all tuning stays serialized on PlayerController.
/// Ported unchanged from DrunkWalkHome.
/// </summary>
public class DrunkSway
{
    // Noise field frequencies. The direction wanders slower than the magnitude so a lurch holds its
    // heading long enough to read, then swells and fades within it.
    private const float TurnNoiseFrequency = 0.35f;
    private const float MagnitudeNoiseFrequency = 0.55f;
    // Separate Perlin lanes: sampling one lane at an offset correlates them and biases the sway
    // diagonally.
    private const float MagnitudeNoiseLane = 100f;

    private float angleDeg;    // heading of the sway in body-local space; 0 = straight forward
    private float noiseTime;
    private Vector2 current;

    /// <summary>The live sway vector, body-local. Never exceeds the caller's magnitude budget.</summary>
    public Vector2 Current => current;

    // No constructor seeding: this class is held in a MonoBehaviour field initializer, which Unity runs
    // in the object's CONSTRUCTOR, where engine API (Random.Range) is illegal. Awake calls Reset().

    /// <summary>Advances the sway. <paramref name="maxMagnitude"/> is the ceiling at the CURRENT
    /// drunkenness. <paramref name="rotationRate"/> (deg/s at full deflection) is the difficulty lever;
    /// the <paramref name="responseSpeed"/> ease is exponential, so the feel is framerate-independent.
    /// </summary>
    public Vector2 Tick(float maxMagnitude, float rotationRate, float responseSpeed, float dt)
    {
        maxMagnitude = Mathf.Max(0f, maxMagnitude);
        float ease = 1f - Mathf.Exp(-responseSpeed * dt);

        // Sober: no sway at all, so the resultant is exactly the input.
        if (maxMagnitude <= 1e-5f)
        {
            current = Vector2.Lerp(current, Vector2.zero, ease);
            if (current.sqrMagnitude < 1e-8f) current = Vector2.zero;
            return current;
        }

        noiseTime += dt;

        // Signed steering of the heading. Perlin re-centered to [-1, 1] so the sway wanders and
        // reverses rather than spinning at a constant rate — a constant spin reads as a machine.
        float turn = Mathf.PerlinNoise(noiseTime * TurnNoiseFrequency, 0f) * 2f - 1f;
        angleDeg += turn * rotationRate * dt;

        // Magnitude rides its own lane of the same field, unsigned: the sway swells and fades but
        // never flips sign (a sign flip is the heading's job, and doing both teleports the vector).
        float swell = Mathf.PerlinNoise(noiseTime * MagnitudeNoiseFrequency, MagnitudeNoiseLane);

        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 target = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (swell * maxMagnitude);

        current = Vector2.Lerp(current, target, ease);

        // Capped on the EASED vector, not just the target: when the budget drops, the trailing vector
        // would otherwise sit above the new ceiling for a moment.
        float mag = current.magnitude;
        if (mag > maxMagnitude) current *= maxMagnitude / mag;

        return current;
    }

    /// <summary>Clears the sway and re-seeds its phase.</summary>
    public void Reset()
    {
        current = Vector2.zero;
        angleDeg = Random.Range(0f, 360f);
        // Perlin returns 0.5 at the origin — an unseeded start gives every run the same opening lurch.
        noiseTime = Random.Range(0f, 1000f);
    }
}
