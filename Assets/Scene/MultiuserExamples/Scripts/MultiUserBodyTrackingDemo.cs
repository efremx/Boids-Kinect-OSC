using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using com.rfilkov.kinect;

public class MultiUserBodyTrackingDemo : MonoBehaviour
{
    [Tooltip("Maximum player slots to visualize. Set to 0 to use the manager's maximum body count.")]
    public int maxPlayers = 6;

    [Tooltip("Sphere diameter, in meters.")]
    public float markerDiameter = 0.18f;

    [Tooltip("How quickly visible markers move to their new tracked positions.")]
    public float positionSmoothing = 12f;

    private static readonly Color[] PlayerColors =
    {
        new Color(0.95f, 0.35f, 0.29f),
        new Color(0.15f, 0.72f, 0.95f),
        new Color(0.99f, 0.74f, 0.16f),
        new Color(0.37f, 0.86f, 0.49f),
        new Color(0.73f, 0.45f, 0.98f),
        new Color(1f, 0.55f, 0.24f),
    };

    private static readonly MarkerDefinition[] MarkerDefinitions =
    {
        new MarkerDefinition("Head", KinectInterop.JointType.Head, KinectInterop.JointType.Neck),
        new MarkerDefinition("Torso", KinectInterop.JointType.SpineChest, KinectInterop.JointType.SpineNaval),
        new MarkerDefinition("LeftHand", KinectInterop.JointType.HandLeft, KinectInterop.JointType.WristLeft),
        new MarkerDefinition("RightHand", KinectInterop.JointType.HandRight, KinectInterop.JointType.WristRight),
        new MarkerDefinition("LeftLeg", KinectInterop.JointType.KneeLeft, KinectInterop.JointType.AnkleLeft),
        new MarkerDefinition("RightLeg", KinectInterop.JointType.KneeRight, KinectInterop.JointType.AnkleRight),
    };

    private readonly Dictionary<int, PlayerVisuals> playerVisuals = new Dictionary<int, PlayerVisuals>();
    private KinectManager kinectManager;

    private void Update()
    {
        if (kinectManager == null)
        {
            kinectManager = KinectManager.Instance;
        }

        if (kinectManager == null || !kinectManager.IsInitialized())
        {
            SetAllPlayersVisible(false);
            return;
        }

        int playerSlots = maxPlayers > 0 ? Mathf.Min(maxPlayers, kinectManager.GetMaxBodyCount()) : kinectManager.GetMaxBodyCount();

        for (int playerIndex = 0; playerIndex < playerSlots; playerIndex++)
        {
            ulong userId = kinectManager.GetUserIdByIndex(playerIndex);

            if (userId != 0)
            {
                UpdatePlayerVisuals(playerIndex, userId);
            }
            else if (playerVisuals.TryGetValue(playerIndex, out PlayerVisuals visuals))
            {
                visuals.SetVisible(false);
            }
        }
    }

    private void OnDisable()
    {
        SetAllPlayersVisible(false);
    }

    private void SetAllPlayersVisible(bool isVisible)
    {
        foreach (PlayerVisuals visuals in playerVisuals.Values)
        {
            visuals.SetVisible(isVisible);
        }
    }

    private void UpdatePlayerVisuals(int playerIndex, ulong userId)
    {
        PlayerVisuals visuals = GetOrCreatePlayerVisuals(playerIndex);
        visuals.SetVisible(true);

        for (int markerIndex = 0; markerIndex < MarkerDefinitions.Length; markerIndex++)
        {
            MarkerDefinition markerDefinition = MarkerDefinitions[markerIndex];
            MarkerVisual markerVisual = visuals.Markers[markerIndex];

            if (TryGetJointPosition(userId, markerDefinition, out Vector3 targetPosition))
            {
                markerVisual.SetVisible(true);

                if (!markerVisual.HasValidPosition || positionSmoothing <= 0f)
                {
                    markerVisual.Transform.position = targetPosition;
                }
                else
                {
                    markerVisual.Transform.position = Vector3.Lerp(
                        markerVisual.Transform.position,
                        targetPosition,
                        Time.deltaTime * positionSmoothing);
                }

                markerVisual.HasValidPosition = true;
            }
            else
            {
                markerVisual.HasValidPosition = false;
                markerVisual.SetVisible(false);
            }
        }
    }

    private PlayerVisuals GetOrCreatePlayerVisuals(int playerIndex)
    {
        if (playerVisuals.TryGetValue(playerIndex, out PlayerVisuals visuals))
        {
            return visuals;
        }

        Color playerColor = PlayerColors[playerIndex % PlayerColors.Length];
        GameObject playerRoot = new GameObject($"Player_{playerIndex}");
        playerRoot.transform.SetParent(transform, false);

        visuals = new PlayerVisuals(playerRoot);

        for (int markerIndex = 0; markerIndex < MarkerDefinitions.Length; markerIndex++)
        {
            MarkerDefinition markerDefinition = MarkerDefinitions[markerIndex];
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = markerDefinition.Name;
            sphere.transform.SetParent(playerRoot.transform, false);
            sphere.transform.localScale = Vector3.one * markerDiameter;

            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
            {
                Destroy(sphereCollider);
            }

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                sphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
                sphereRenderer.receiveShadows = false;
                sphereRenderer.material.color = playerColor;
            }

            visuals.Markers[markerIndex] = new MarkerVisual(sphere.transform, sphere);
            visuals.Markers[markerIndex].SetVisible(false);
        }

        visuals.SetVisible(false);
        playerVisuals[playerIndex] = visuals;
        return visuals;
    }

    private bool TryGetJointPosition(ulong userId, MarkerDefinition markerDefinition, out Vector3 jointPosition)
    {
        if (TryGetJointPosition(userId, markerDefinition.PrimaryJoint, out jointPosition))
        {
            return true;
        }

        return markerDefinition.FallbackJoint != markerDefinition.PrimaryJoint &&
               TryGetJointPosition(userId, markerDefinition.FallbackJoint, out jointPosition);
    }

    private bool TryGetJointPosition(ulong userId, KinectInterop.JointType joint, out Vector3 jointPosition)
    {
        jointPosition = Vector3.zero;

        if (kinectManager == null || !kinectManager.IsJointTracked(userId, joint))
        {
            return false;
        }

        jointPosition = kinectManager.GetJointPosition(userId, joint);
        return true;
    }

    private readonly struct MarkerDefinition
    {
        public MarkerDefinition(string name, KinectInterop.JointType primaryJoint, KinectInterop.JointType fallbackJoint)
        {
            Name = name;
            PrimaryJoint = primaryJoint;
            FallbackJoint = fallbackJoint;
        }

        public string Name { get; }
        public KinectInterop.JointType PrimaryJoint { get; }
        public KinectInterop.JointType FallbackJoint { get; }
    }

    private sealed class MarkerVisual
    {
        public MarkerVisual(Transform markerTransform, GameObject markerObject)
        {
            Transform = markerTransform;
            GameObject = markerObject;
            HasValidPosition = false;
        }

        public Transform Transform { get; }
        public GameObject GameObject { get; }
        public bool HasValidPosition { get; set; }

        public void SetVisible(bool isVisible)
        {
            if (GameObject.activeSelf != isVisible)
            {
                GameObject.SetActive(isVisible);
            }
        }
    }

    private sealed class PlayerVisuals
    {
        public PlayerVisuals(GameObject rootObject)
        {
            RootObject = rootObject;
            Markers = new MarkerVisual[MarkerDefinitions.Length];
        }

        public GameObject RootObject { get; }
        public MarkerVisual[] Markers { get; }

        public void SetVisible(bool isVisible)
        {
            if (RootObject.activeSelf != isVisible)
            {
                RootObject.SetActive(isVisible);
            }
        }
    }
}
