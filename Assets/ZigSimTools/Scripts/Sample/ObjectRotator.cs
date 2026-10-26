using UnityEngine;
using ZigSimTools;
using Quaternion = UnityEngine.Quaternion;

public class ObjectRotator : MonoBehaviour
{
    public enum Player { P1, P2 }
    public Player player = Player.P1;

    private Quaternion targetRotation;

    void Start()
    {
        ZigSimDataManager.Instance.StartReceiving();

        System.Action<ZigSimTools.Quaternion> handler = (ZigSimTools.Quaternion q) =>
        {
            var newQut = new Quaternion((float)-q.x, (float)-q.z, (float)-q.y, (float)q.w);
            targetRotation = newQut * Quaternion.Euler(90f, 0, 0);
        };

        if (player == Player.P1)
            ZigSimDataManager.Instance.QuaternionCallBack_P1 += handler;
        else
            ZigSimDataManager.Instance.QuaternionCallBack_P2 += handler;
    }

    void Update()
    {
        transform.localRotation = targetRotation;
    }
}