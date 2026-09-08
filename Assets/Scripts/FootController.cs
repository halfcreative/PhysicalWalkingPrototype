using UnityEngine;

public class FootController : MonoBehaviour
{
    public bool IsGrounded { get; private set; }
    public float GroundHeight { get; private set; }

    private ConfigurableJoint ankle { get; set; }
    private Quaternion startLocalRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    void Awake()
    {
        ankle = this.GetComponent<ConfigurableJoint>();
        startLocalRotation = transform.localRotation;

    }

    // Update is called once per frame
    void Update()
    {

    }

    // Ground Check
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            IsGrounded = true;
            GroundHeight = collision.contacts[0].point.y;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            IsGrounded = false;
            GroundHeight = 0;
        }
    }

    public void SetAnkleTarget(float pitchDegrees)
    {
        // Debug.Log("Setting Ankle Target" + pitchDegrees);
        Quaternion desired = Quaternion.Euler(pitchDegrees, 0f, 0f) * startLocalRotation;
        ankle.targetRotation = Quaternion.Inverse(desired) * startLocalRotation;
    }
}
