using UnityEngine;
using ZigSimTools;

public class PhoneSwing : MonoBehaviour
{
    public enum Player { P1, P2 }
    public Player player = Player.P1;

    public float threshold = 1.5f;
    public float cooldown = 0.25f;

    public float lastSwingPower { get; private set; }
    public float lastSwingSide { get; private set; }

    private float lastSwingTime = 0f;
    private bool swingBuffered = false;

    public bool ConsumeSwing()
    {
        if (swingBuffered)
        {
            swingBuffered = false;
            return true;
        }
        return false;
    }

    void Start()
    {
        System.Action<Accel> handler = (a) =>
        {
            float mag = new Vector3((float)a.x, (float)a.y, (float)a.z).magnitude;
            lastSwingSide = (float)a.x;

            if (mag > threshold && Time.time - lastSwingTime > cooldown)
            {
                swingBuffered = true;
                lastSwingTime = Time.time;
                lastSwingPower = mag;
                Debug.Log($"[{player}] SWING: " + mag);
            }
        };

        if (player == Player.P1)
            ZigSimDataManager.Instance.AccelCallBack_P1 += handler;
        else
            ZigSimDataManager.Instance.AccelCallBack_P2 += handler;
    }
}