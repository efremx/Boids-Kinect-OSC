using UnityEngine;

public class BallController : MonoBehaviour
{
    public Rigidbody rb;

    public Transform hand;
    public HandSimulator handSim;
    public PhoneSwing phone;

    public float hitRadius = 3f;
    public float powerMultiplier = 2f;
    public float maxSpeed = 20f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (phone.ConsumeSwing())
        {
            TryHit();
        }
    }

void TryHit() {
    // 1. HUGE HITBOX (Be generous!)
    if (Vector3.Distance(transform.position, hand.position) > .6f) return;

    // 2. PERFECT ARCADE RETURN
    float targetX = (transform.position.x < 0) ? 5.0f : -5.0f;
    Vector3 dir = (new Vector3(targetX, 0, 0) - transform.position);
    dir.y = 0;
    Vector3 launch = new Vector3(dir.normalized.x, 3.67f, dir.normalized.z).normalized;

    // 3. APPLY & RESET
    rb.linearVelocity = Vector3.zero;
    rb.linearVelocity = launch *3.5f; // Steady, readable speed

    // 4. JUICE (Feedback)
    // CameraShake(); 
    // PlaySound();
}
}