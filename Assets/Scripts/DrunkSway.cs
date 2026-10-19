using UnityEngine;

// The involuntary drunk sway: one 2D force in the heading's frame (x right, y forward), the only noise
// source in the project. Polar, so direction and magnitude tune separately: the rotation rate sets how
// long the player has to read a lurch. Tuning lives on PlayerController.
public class DrunkSway
{
    // The direction wanders slower than the magnitude, so a lurch holds its direction while it swells.
    const float TurnNoiseFrequency = 0.35f;
    const float MagnitudeNoiseFrequency = 0.55f;
    // Separate Perlin row for the magnitude, so it doesn't correlate with the direction.
    const float MagnitudeNoiseRow = 100f;

    float angleDegrees;    // 0 = straight forward
    float noiseTime;

    public Vector2 Current { get; private set; }

    // No constructor seeding: this is created in a field initializer, where Random is not allowed.
    // PlayerController.Awake calls Reset.

    // Advances the sway. maxMagnitude is the ceiling at the current drunkenness; rotationRate is deg/s at
    // full deflection.
    public Vector2 Tick(float maxMagnitude, float rotationRate, float responseSpeed, float dt)
    {
        maxMagnitude = Mathf.Max(0f, maxMagnitude);
        float ease = Smoothing.Factor(responseSpeed, dt);

        // Sober: decay to exactly zero.
        if (maxMagnitude <= 1e-5f)
        {
            Current = Vector2.Lerp(Current, Vector2.zero, ease);
            if (Current.sqrMagnitude < 1e-8f) Current = Vector2.zero;
            return Current;
        }

        noiseTime += dt;

        // Signed, so the direction wanders and reverses rather than spinning steadily.
        float turn = Mathf.PerlinNoise(noiseTime * TurnNoiseFrequency, 0f) * 2f - 1f;
        angleDegrees += turn * rotationRate * dt;

        // Unsigned: the sway swells and fades but never flips; flipping is the direction's job.
        float swell = Mathf.PerlinNoise(noiseTime * MagnitudeNoiseFrequency, MagnitudeNoiseRow);

        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 target = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * (swell * maxMagnitude);
        Vector2 current = Vector2.Lerp(Current, target, ease);

        // Capped after easing too, so a dropping ceiling takes effect at once.
        float magnitude = current.magnitude;
        if (magnitude > maxMagnitude) current *= maxMagnitude / magnitude;

        Current = current;
        return Current;
    }

    // Clears the sway and picks a random starting phase.
    public void Reset()
    {
        Current = Vector2.zero;
        angleDegrees = Random.Range(0f, 360f);
        noiseTime = Random.Range(0f, 1000f);
    }
}
