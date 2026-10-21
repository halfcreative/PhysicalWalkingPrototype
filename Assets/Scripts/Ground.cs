using UnityEngine;

// What the character stands on, for the balance and gait code: the ground under a point, and step faces.
// A face is where the ground changes height abruptly, as up or down a step; a slope changes it gradually
// and is just ground to walk on.
public static class Ground
{
    // Ground is searched for from this far above a reference height to this far below it, m: past knee
    // height (0.5 m) both ways. A search ending level with the ground below a step, or starting level with
    // the top of one, misses it.
    const float SearchUp = 0.7f;
    const float SearchDown = 0.7f;
    // Halvings when finding where the ground crosses a height between two points.
    const int CrossingSearchSteps = 6;
    // A face makes at least half its height change within this distance either side of its middle, m.
    const float FaceSharpness = 0.05f;
    // A smaller height change isn't a face, m.
    const float MinFaceHeight = 0.03f;

    static int mask;

    // Everything but the character's own bodies.
    public static int Mask => mask != 0 ? mask : mask = ~LayerMask.GetMask("PlayerBody");

    // The ground under `point`, searched for around the height `near`.
    public static bool At(Vector3 point, float near, out Vector3 ground, out Vector3 normal)
    {
        bool found = Physics.Raycast(new Vector3(point.x, near + SearchUp, point.z), Vector3.down,
                                     out RaycastHit hit, SearchUp + SearchDown, Mask, QueryTriggerInteraction.Ignore);
        ground = found ? hit.point : point;
        normal = found ? hit.normal : Vector3.up;
        return found;
    }

    // The ground's height under `point`; the bottom of the search when there's none in reach.
    public static float HeightAt(Vector3 point, float near) =>
        At(point, near, out Vector3 ground, out _) ? ground.y : near - SearchDown;

    // How far from `from` along `direction`, within `length`, the ground crosses `height`: going up through
    // it if `rising`, down through it otherwise.
    public static float Crossing(Vector3 from, Vector3 direction, float length, float near, float height, bool rising)
    {
        float before = 0f, after = length;
        for (int i = 0; i < CrossingSearchSteps; i++)
        {
            float middle = (before + after) * 0.5f;
            bool past = (HeightAt(from + direction * middle, near) >= height) == rising;
            if (past) after = middle;
            else before = middle;
        }
        return (before + after) * 0.5f;
    }

    // Whether the ground between `from` and `to` changes height at a face, and how far from `from` (flat) its
    // middle is.
    public static bool FaceBetween(Vector3 from, Vector3 to, float near, out float distance)
    {
        distance = 0f;
        Vector3 path = (to - from).Flat();
        float length = path.magnitude;
        if (length < 1e-4f) return false;

        Vector3 direction = path / length;
        float start = HeightAt(from, near);
        float change = HeightAt(to, near) - start;
        if (Mathf.Abs(change) < MinFaceHeight) return false;

        distance = Crossing(from, direction, length, near, start + change * 0.5f, change > 0f);
        float before = HeightAt(from + direction * (distance - FaceSharpness), near);
        float after = HeightAt(from + direction * (distance + FaceSharpness), near);
        return (after - before) * Mathf.Sign(change) >= Mathf.Abs(change) * 0.5f;
    }
}
