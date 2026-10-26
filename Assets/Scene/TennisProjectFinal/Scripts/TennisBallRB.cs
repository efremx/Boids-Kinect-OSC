using UnityEngine;

public class TennisBallRB : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("57g = real tennis ball. Lower = floatier, higher = heavier arc.")]
    public float mass            = 0.0057f;
    [Tooltip("Air resistance. Real tennis is ~0.02. Higher values kill speed quickly.")]
    public float linearDamping   = 0.02f;
    public float angularDamping  = 0.05f;

    [Header("Bounce Material")]
    [Range(0f, 1f)]
    [Tooltip("1 = perfectly elastic. 0.82 feels like a real hard-court tennis ball.")]
    public float bounciness      = 0.82f;
    [Range(0f, 1f)]
    public float dynamicFriction = 0.2f;
    [Range(0f, 1f)]
    public float staticFriction  = 0.2f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass           = mass;
        rb.linearDamping  = linearDamping;
        rb.angularDamping = angularDamping;
        rb.useGravity     = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Keep ball in the XY play plane
        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY;

        ApplyMaterial();
    }

    // Call this at runtime if you tweak values in the Inspector during Play mode
    public void ApplyMaterial()
    {
        var mat = new PhysicsMaterial("TennisBallMat");
        mat.bounciness      = bounciness;
        mat.bounceCombine   = PhysicsMaterialCombine.Maximum;
        mat.dynamicFriction = dynamicFriction;
        mat.staticFriction  = staticFriction;
        mat.frictionCombine = PhysicsMaterialCombine.Minimum; // don't let surfaces add friction on top
        GetComponent<Collider>().material = mat;
    }
}