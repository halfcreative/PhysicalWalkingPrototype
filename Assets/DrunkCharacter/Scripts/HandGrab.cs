using UnityEngine;

// One per hand. While its grab input is held, the hand grips the first thing its tip touches: a joint from
// the forearm's tip to that body, or to the world for static geometry, so a character can hang from a
// ledge, drag a prop or carry another player. ArmDrive reaches the arm along the aim meanwhile.
//
// Host only: the grip is part of the simulation, and clients see it through the pose stream. Letting go,
// falling or a reset releases it, and so does the held body going away, as a character's does when its
// player leaves. Pulled past breakForce the grip tears loose, and the hand can take hold again after
// RegrabDelay if the input is still held.
[DefaultExecutionOrder(20)]   // after ArmDrive (10) has aimed the arm this tick
public class HandGrab : MonoBehaviour
{
    const float RegrabDelay = 0.3f;
    const int MaxOverlaps = 16;

    [SerializeField] PlayerController playerController;
    // The forearm; the hand is its tip.
    [SerializeField] CapsuleCollider forearm;
    // −1 for the left hand, +1 for the right.
    [SerializeField] float side = -1f;
    // How far from the forearm's tip the hand can take hold, m.
    [SerializeField] float gripRadius = 0.08f;
    // Force that tears the grip loose, N. About two and a half times a character's 62 kg weight.
    [SerializeField] float breakForce = 1500f;

    readonly Collider[] overlaps = new Collider[MaxOverlaps];
    Collider[] ownColliders;
    Rigidbody forearmBody;
    FixedJoint grip;
    // Set while a grip exists, so a joint the engine broke can be told from one never made.
    bool gripping;
    // The grip holds a body, not the world. A joint whose body is destroyed holds the world instead, so
    // without this the hand would stay fixed where that body last was.
    bool holdsBody;
    float regrabAt;

    public bool IsHolding => grip != null;
    // What the hand holds; null when holding nothing or the world.
    public Rigidbody HeldBody => grip != null ? grip.connectedBody : null;

    // The forearm capsule's lower end, in world space.
    Vector3 HandPoint
    {
        get
        {
            Vector3 local = forearm.center + Vector3.down * (forearm.height * 0.5f);
            return forearm.transform.TransformPoint(local);
        }
    }

    bool InputHeld
    {
        get
        {
            CharacterInput input = playerController.CurrentInput;
            return side < 0f ? input.GrabLeft : input.GrabRight;
        }
    }

    void Awake()
    {
        if (!playerController || !forearm || !forearm.attachedRigidbody || forearm.direction != 1)
        {
            Debug.LogError("HandGrab: needs a PlayerController and a Y-axis forearm capsule with a Rigidbody — disabling.", this);
            enabled = false;
            return;
        }
        forearmBody = forearm.attachedRigidbody;
        ownColliders = playerController.GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        playerController.Fell += Release;
        playerController.CharacterReset += Release;
    }

    void OnDisable()
    {
        playerController.Fell -= Release;
        playerController.CharacterReset -= Release;
        Release();
    }

    void FixedUpdate()
    {
        if (gripping && grip == null)
        {
            // Broken by the engine at breakForce.
            gripping = false;
            regrabAt = Time.time + RegrabDelay;
        }

        if (playerController.IsFallen || !InputHeld || (holdsBody && HeldBody == null))
        {
            Release();
            return;
        }

        if (!gripping && Time.time >= regrabAt) TryGrip();
    }

    void TryGrip()
    {
        Vector3 hand = HandPoint;
        int count = Physics.OverlapSphereNonAlloc(hand, gripRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);

        Collider target = null;
        Vector3 contact = hand;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlaps[i];
            if (System.Array.IndexOf(ownColliders, candidate) >= 0) continue;

            // ClosestPoint needs a convex collider; for anything else, take hold where the hand is.
            bool convex = candidate is not MeshCollider mesh || mesh.convex;
            Vector3 point = convex ? candidate.ClosestPoint(hand) : hand;
            float distance = (point - hand).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            target = candidate;
            contact = point;
        }
        if (target == null) return;

        Rigidbody held = target.attachedRigidbody;
        grip = forearmBody.gameObject.AddComponent<FixedJoint>();
        grip.autoConfigureConnectedAnchor = false;
        grip.anchor = forearmBody.transform.InverseTransformPoint(contact);
        grip.connectedBody = held;
        // Without a body, the connected anchor is a world position: the hand holds onto the world.
        grip.connectedAnchor = held != null ? held.transform.InverseTransformPoint(contact) : contact;
        grip.breakForce = breakForce;
        grip.enableCollision = false;
        gripping = true;
        holdsBody = held != null;
    }

    void Release()
    {
        if (grip != null) Destroy(grip);
        grip = null;
        gripping = false;
        holdsBody = false;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || forearm == null) return;
        Gizmos.color = IsHolding ? Color.red : InputHeld ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(HandPoint, gripRadius);
    }
}
