using UnityEngine;

// The balance readout, ported from DrunkWalkHome: sway, input, their resultant and the body's momentum
// drawn from one origin, inside a ring of radius FallLimit, with the breach filling around the ring.
// Game UI there, not a debug overlay: the ring is what makes the fall legible, turning the sway from
// noise you fight into a force you read. Drawn in the heading's frame with forward pointing up, so "the
// sway is pushing me right" reads as a line to the right.
[RequireComponent(typeof(PlayerController))]
public class BalanceHUD : MonoBehaviour
{
    [Header("Layout")]
    // Screen pixels per unit of force magnitude. The ring is drawn at FallLimit × this.
    [SerializeField] float pixelsPerUnit = 90f;
    // Centre of the display in viewport coordinates (0,0 = bottom-left, 1,1 = top-right).
    [SerializeField] Vector2 viewportCenter = new(0.5f, 0.22f);
    [SerializeField] float lineThickness = 3f;
    [SerializeField] float ringThickness = 2f;
    [SerializeField, Range(12, 96)] int ringSegments = 48;

    [Header("Colors")]
    [SerializeField] Color swayColor = new(1f, 0.45f, 0.15f, 0.9f);       // orange: what's done to you
    [SerializeField] Color inputColor = new(0.35f, 0.75f, 1f, 0.9f);      // blue: what you're asking for
    [SerializeField] Color resultantColor = new(1f, 1f, 1f, 1f);          // white: the force that results
    [SerializeField] Color momentumColor = new(0.45f, 0.95f, 0.5f, 0.9f); // green: where you're actually going
    [SerializeField] Color ringColor = new(1f, 1f, 1f, 0.35f);
    [SerializeField] Color breachColor = new(1f, 0.3f, 0.25f, 0.95f);     // red: the fall charging
    [SerializeField] Color flashColor = new(1f, 0.2f, 0.2f, 0.55f);

    [Header("Fall Flash")]
    [SerializeField] float flashDuration = 0.45f;

    [Header("Breach Factors")]
    // One labelled bar per breach source below the ring, so what is filling it can be read while testing.
    [SerializeField] bool showFactors = true;
    // Bar pixels per unit of charge (breach per second); bars stop at maxFactorUnits.
    [SerializeField] float factorPixelsPerUnit = 60f;
    [SerializeField] float maxFactorUnits = 2f;
    [SerializeField] Color tiltColor = new(0.8f, 0.5f, 1f, 0.95f);   // violet: the pelvis tipping
    [SerializeField] Color dropColor = new(1f, 0.85f, 0.3f, 0.95f);  // yellow: the centre of mass sinking

    PlayerController controller;
    BalanceController balance;
    float flashUntil;
    Texture2D pixel;
    GUIStyle labelStyle;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        balance = GetComponent<BalanceController>();
        // One white pixel, tinted per draw via GUI.color. GUI.DrawTexture can't stroke a line, so every
        // line here is this texture drawn into a rotated matrix.
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    void OnEnable()
    {
        if (controller != null) controller.Fell += OnFell;
    }

    void OnDisable()
    {
        if (controller != null) controller.Fell -= OnFell;
    }

    void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }

    void OnFell() => flashUntil = Time.time + flashDuration;

    void OnGUI()
    {
        if (controller == null) return;

        Vector2 origin = new(
            Screen.width * viewportCenter.x,
            Screen.height * (1f - viewportCenter.y));   // GUI space is y-down

        float ringRadius = controller.FallLimit * pixelsPerUnit;

        Color prevColor = GUI.color;
        Matrix4x4 prevMatrix = GUI.matrix;

        if (Time.time < flashUntil)
        {
            float t = Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(flashDuration, 1e-4f));
            Color c = flashColor;
            c.a *= t;
            DrawDisc(origin, ringRadius, c);
        }

        DrawRing(origin, ringRadius, ringColor);

        // Breach meter: crossing the ring is survivable, so without this the fall would arrive with no
        // visible cause. The charge IS the warning.
        float breach = controller.BreachFraction;
        if (breach > 0.001f)
            DrawArc(origin, ringRadius, breachColor, ringThickness * 2f, breach);

        // x = the heading's right, y = its forward, which points UP on screen (so y negates into GUI's
        // y-down space). Momentum is in the same units as the forces, so the gap between the white force
        // and the green travel IS the momentum lag; it gets a tip marker as well as its own hue, since four
        // lines from one origin is past what colour alone separates.
        DrawVector(origin, controller.Sway, swayColor);
        DrawVector(origin, controller.SteerInput, inputColor);
        DrawVector(origin, controller.Momentum, momentumColor, tipMarker: true);
        // Drawn last so the fall-critical vector is never hidden by the others.
        DrawVector(origin, controller.Resultant, resultantColor);

        if (showFactors) DrawFactors(origin + new Vector2(-ringRadius, ringRadius + 12f));

        GUI.matrix = prevMatrix;
        GUI.color = prevColor;
    }

    // Below the ring: the breach as a percentage, then each source's live charge as a bar with its reading.
    void DrawFactors(Vector2 topLeft)
    {
        labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        const float rowHeight = 16f;
        const float labelWidth = 150f;

        GUI.color = Color.white;
        GUI.Label(new Rect(topLeft.x, topLeft.y, 300f, rowHeight), $"breach {controller.BreachFraction * 100f:F0}%", labelStyle);

        DrawFactorRow(topLeft + new Vector2(0f, rowHeight), $"momentum {controller.Momentum.magnitude:F2}/{controller.FallLimit:F2}",
                      controller.MomentumCharge, momentumColor);
        DrawFactorRow(topLeft + new Vector2(0f, rowHeight * 2f), $"tilt {controller.PelvisTilt:F0}°",
                      controller.TiltCharge, tiltColor);
        DrawFactorRow(topLeft + new Vector2(0f, rowHeight * 3f), $"drop CoM {controller.CenterOfMassHeight:F2} m",
                      controller.DropCharge, dropColor);
        // Not a breach source: how hard the player's input is pushing back against the lean, 0–1.
        if (balance != null)
            DrawFactorRow(topLeft + new Vector2(0f, rowHeight * 4f), $"counter {balance.Counter * 100f:F0}%",
                          balance.Counter, inputColor);

        void DrawFactorRow(Vector2 at, string label, float charge, Color color)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(at.x, at.y, labelWidth, rowHeight), label, labelStyle);
            float length = Mathf.Min(charge, maxFactorUnits) * factorPixelsPerUnit;
            if (length < 0.5f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x + labelWidth, at.y + rowHeight * 0.25f, length, rowHeight * 0.5f), pixel);
        }
    }

    void DrawVector(Vector2 origin, Vector2 force, Color color, bool tipMarker = false)
    {
        if (force.sqrMagnitude < 1e-6f) return;
        Vector2 end = origin + new Vector2(force.x, -force.y) * pixelsPerUnit;
        DrawLine(origin, end, color, lineThickness);

        if (!tipMarker) return;
        float s = lineThickness * 2.5f;
        GUI.color = color;
        GUI.DrawTexture(new Rect(end.x - s * 0.5f, end.y - s * 0.5f, s, s), pixel);
    }

    void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 1e-4f) return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, from);
        GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), pixel);
        GUIUtility.RotateAroundPivot(-angle, from);
    }

    void DrawRing(Vector2 center, float radius, Color color)
    {
        float step = 2f * Mathf.PI / ringSegments;
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= ringSegments; i++)
        {
            float a = i * step;
            Vector2 next = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            DrawLine(prev, next, color, ringThickness);
            prev = next;
        }
    }

    // Radial meter: sweeps clockwise from the top of the ring, filling `fraction` of it. GUI space is
    // y-down, so (sin, −cos) reads clockwise on screen.
    void DrawArc(Vector2 center, float radius, Color color, float thickness, float fraction)
    {
        int segments = Mathf.Max(1, Mathf.CeilToInt(ringSegments * Mathf.Clamp01(fraction)));
        float sweep = 2f * Mathf.PI * Mathf.Clamp01(fraction);
        float step = sweep / segments;
        Vector2 prev = center + new Vector2(0f, -radius);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * step;
            Vector2 next = center + new Vector2(Mathf.Sin(a) * radius, -Mathf.Cos(a) * radius);
            DrawLine(prev, next, color, thickness);
            prev = next;
        }
    }

    // Filled flash: concentric rings, cheap enough at this radius and avoids shipping a disc texture.
    void DrawDisc(Vector2 center, float radius, Color color)
    {
        for (float r = radius; r > 0f; r -= ringThickness)
            DrawRing(center, r, color);
    }
}
