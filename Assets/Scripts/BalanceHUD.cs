using UnityEngine;
using UnityEngine.Serialization;

// The balance readout: sway, input, net force and momentum drawn from one origin inside a ring of radius
// MomentumLimit, with the fall meter filling around the ring. Heading frame, forward pointing up.
[RequireComponent(typeof(PlayerController))]
public class BalanceHUD : MonoBehaviour
{
    [Header("Layout")]
    // Screen pixels per unit of force.
    [SerializeField] float pixelsPerUnit = 90f;
    // Centre of the display in viewport coordinates (0,0 bottom-left).
    [SerializeField] Vector2 viewportCenter = new(0.5f, 0.22f);
    [SerializeField] float lineThickness = 3f;
    [SerializeField] float ringThickness = 2f;
    [SerializeField, Range(12, 96)] int ringSegments = 48;

    [Header("Colors")]
    [SerializeField] Color swayColor = new(1f, 0.45f, 0.15f, 0.9f);
    [SerializeField] Color inputColor = new(0.35f, 0.75f, 1f, 0.9f);
    [SerializeField, FormerlySerializedAs("resultantColor")] Color netForceColor = new(1f, 1f, 1f, 1f);
    [SerializeField] Color momentumColor = new(0.45f, 0.95f, 0.5f, 0.9f);
    [SerializeField] Color ringColor = new(1f, 1f, 1f, 0.35f);
    [SerializeField, FormerlySerializedAs("breachColor")] Color fallMeterColor = new(1f, 0.3f, 0.25f, 0.95f);
    [SerializeField] Color flashColor = new(1f, 0.2f, 0.2f, 0.55f);

    [Header("Fall Flash")]
    [SerializeField] float flashDuration = 0.45f;

    [Header("Fall Meter Sources")]
    // One labelled bar per fall meter source, below the ring.
    [SerializeField] bool showFactors = true;
    // Bar pixels per unit of charge; bars stop at maxFactorUnits.
    [SerializeField] float factorPixelsPerUnit = 60f;
    [SerializeField] float maxFactorUnits = 2f;
    [SerializeField] Color tiltColor = new(0.8f, 0.5f, 1f, 0.95f);
    [SerializeField] Color dropColor = new(1f, 0.85f, 0.3f, 0.95f);

    PlayerController playerController;
    BalanceController balanceController;
    float flashUntil;
    // Both white, tinted per draw via GUI.color. Lines are whiteTexture drawn into a rotated matrix.
    Texture2D whiteTexture;
    Texture2D discTexture;
    GUIStyle labelStyle;

    // Where the numbers come from. Unset, they're captured live from this character, which only works where
    // it's simulated; NetworkPlayer points a client's HUD at the readout the host sends it.
    public System.Func<BalanceReadout> ReadoutSource { get; set; }

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        balanceController = GetComponent<BalanceController>();
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        discTexture = CreateDiscTexture(128);
    }

    // A white disc with a one-pixel soft edge, filling the texture.
    static Texture2D CreateDiscTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float radius = size * 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = new Vector2(x + 0.5f - radius, y + 0.5f - radius).magnitude;
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance));
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    void OnEnable()
    {
        if (playerController != null) playerController.Fell += ShowFall;
    }

    void OnDisable()
    {
        if (playerController != null) playerController.Fell -= ShowFall;
    }

    void OnDestroy()
    {
        if (whiteTexture != null) Destroy(whiteTexture);
        if (discTexture != null) Destroy(discTexture);
    }

    // Flashes the ring. Called on the character's own Fell where it's simulated, and by NetworkPlayer on a
    // client when the host reports the fall.
    public void ShowFall() => flashUntil = Time.time + flashDuration;

    void OnGUI()
    {
        if (playerController == null) return;
        BalanceReadout readout = ReadoutSource?.Invoke() ?? BalanceReadout.Capture(playerController, balanceController);

        Vector2 origin = new(
            Screen.width * viewportCenter.x,
            Screen.height * (1f - viewportCenter.y));   // GUI space is y-down

        float ringRadius = readout.MomentumLimit * pixelsPerUnit;

        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;

        if (Time.time < flashUntil)
        {
            float t = Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(flashDuration, 1e-4f));
            Color color = flashColor;
            color.a *= t;
            DrawDisc(origin, ringRadius, color);
        }

        DrawRing(origin, ringRadius, ringColor);

        if (readout.FallMeterFraction > 0.001f)
            DrawArc(origin, ringRadius, fallMeterColor, ringThickness * 2f, readout.FallMeterFraction);

        DrawVector(origin, readout.Sway, swayColor);
        DrawVector(origin, readout.MoveInput, inputColor);
        DrawVector(origin, readout.Momentum, momentumColor, tipMarker: true);
        // Last, so it's never hidden.
        DrawVector(origin, readout.NetForce, netForceColor);

        if (showFactors) DrawFactors(readout, origin + new Vector2(-ringRadius, ringRadius + 12f));

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    // The fall meter as a percentage, then each source's live charge as a bar.
    void DrawFactors(BalanceReadout readout, Vector2 topLeft)
    {
        labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        const float rowHeight = 16f;
        const float labelWidth = 150f;

        GUI.color = Color.white;
        GUI.Label(new Rect(topLeft.x, topLeft.y, 300f, rowHeight),
                  $"fall meter {readout.FallMeterFraction * 100f:F0}%", labelStyle);

        DrawFactorRow(1, $"momentum {readout.Momentum.magnitude:F2}/{readout.MomentumLimit:F2}",
                      readout.MomentumCharge, momentumColor);
        DrawFactorRow(2, $"tilt {readout.PelvisTilt:F0}°", readout.TiltCharge, tiltColor);
        DrawFactorRow(3, $"drop CoM {readout.CenterOfMassHeight:F2} m", readout.DropCharge, dropColor);
        // Not a source: how hard the input pushes against the lean.
        DrawFactorRow(4, $"counter-steer {readout.CounterSteer * 100f:F0}%", readout.CounterSteer, inputColor);

        void DrawFactorRow(int row, string label, float charge, Color color)
        {
            Vector2 at = topLeft + new Vector2(0f, rowHeight * row);
            GUI.color = Color.white;
            GUI.Label(new Rect(at.x, at.y, labelWidth, rowHeight), label, labelStyle);
            float length = Mathf.Min(charge, maxFactorUnits) * factorPixelsPerUnit;
            if (length < 0.5f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x + labelWidth, at.y + rowHeight * 0.25f, length, rowHeight * 0.5f), whiteTexture);
        }
    }

    // x right, y forward (up on screen).
    void DrawVector(Vector2 origin, Vector2 vector, Color color, bool tipMarker = false)
    {
        if (vector.sqrMagnitude < 1e-6f) return;
        Vector2 end = origin + new Vector2(vector.x, -vector.y) * pixelsPerUnit;
        DrawLine(origin, end, color, lineThickness);

        if (!tipMarker) return;
        float size = lineThickness * 2.5f;
        GUI.color = color;
        GUI.DrawTexture(new Rect(end.x - size * 0.5f, end.y - size * 0.5f, size, size), whiteTexture);
    }

    void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 1e-4f) return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, from);
        GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, from);
    }

    void DrawRing(Vector2 center, float radius, Color color)
    {
        float step = 2f * Mathf.PI / ringSegments;
        Vector2 previous = center + new Vector2(radius, 0f);
        for (int i = 1; i <= ringSegments; i++)
        {
            float angle = i * step;
            Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            DrawLine(previous, next, color, ringThickness);
            previous = next;
        }
    }

    // Fills `fraction` of the ring clockwise from the top.
    void DrawArc(Vector2 center, float radius, Color color, float thickness, float fraction)
    {
        int segments = Mathf.Max(1, Mathf.CeilToInt(ringSegments * Mathf.Clamp01(fraction)));
        float sweep = 2f * Mathf.PI * Mathf.Clamp01(fraction);
        float step = sweep / segments;
        Vector2 previous = center + new Vector2(0f, -radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector2 next = center + new Vector2(Mathf.Sin(angle) * radius, -Mathf.Cos(angle) * radius);
            DrawLine(previous, next, color, thickness);
            previous = next;
        }
    }

    void DrawDisc(Vector2 center, float radius, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), discTexture);
    }
}
