using UnityEngine;

public class PhoneRacket : MonoBehaviour
{
    public enum Player { P1, P2 }
    public Player player = Player.P1;

    public PhoneSwing swingInput; // reference to PhoneSwing script
    public Rigidbody ball;

    public float baseForce = 100;
    public float sideForceMultiplier = 3f;

    void Update()
    {
        if (swingInput.ConsumeSwing())
        {
            Debug.Log($"[{player}] HIT");

            Vector3 dir = (ball.position - transform.position).normalized;

            // forward force (power)
            float power = swingInput.lastSwingPower;
            Vector3 force = dir * baseForce * power;

            // side spin (left/right from phone x accel)
            float side = swingInput.lastSwingSide;
            force += transform.right * side * sideForceMultiplier;

            ball.AddForce(force);
        }
    }
}