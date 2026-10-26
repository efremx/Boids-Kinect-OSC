using UnityEngine;
using com.rfilkov.components;

public class HandSimulator : MonoBehaviour
{
    [Tooltip("0 = Player 1, 1 = Player 2")]
    public int playerIndex = 0;

    public Vector3 velocity { get; private set; }

    private Vector3 lastPos;
    private Transform rightHandTarget;

    void Start()
    {
        lastPos = transform.position;
    }

    void Update()
    {
        // Retry finding the hand every frame until found
        if (rightHandTarget == null)
        {
            rightHandTarget = FindNewAvatarHand();
        }

        if (rightHandTarget != null)
        {
            transform.position = rightHandTarget.position;
            transform.rotation = rightHandTarget.rotation; // optional, keeps orientation
        }
        else
        {
            // Fallback: mouse control
            Vector3 mouse = Input.mousePosition;
            mouse.z = 10f;
            transform.position = Camera.main.ScreenToWorldPoint(mouse);
        }

        velocity = (transform.position - lastPos) / Time.deltaTime;
        lastPos = transform.position;
    }

    Transform FindNewAvatarHand()
    {
        // Find all AvatarControllers in the scene
        AvatarController[] avatars = FindObjectsByType<AvatarController>(FindObjectsSortMode.None);

        foreach (AvatarController ac in avatars)
        {
            if (ac.playerIndex == playerIndex)
            {
                // Search recursively for the right hand bone
                // Common humanoid bone names — adjust if your rig uses different names
                Transform hand = FindBoneRecursive(ac.transform, "Finger.L_end");

                if (hand != null) return hand;
            }
        }

        return null;
    }

    Transform FindBoneRecursive(Transform parent, string boneName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains(boneName))
                return child;
        }
        return null;
    }
}