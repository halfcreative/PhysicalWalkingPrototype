using UnityEngine;

public class FootController : MonoBehaviour
{
    public bool IsGrounded { get; private set; }
    public float GroundHeight { get; private set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

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
}
