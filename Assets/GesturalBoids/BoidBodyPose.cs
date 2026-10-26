using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    // One mapped pose drives both avatar and particles; never mix sensor and scene coordinates.
    public class BoidBodyPose : MonoBehaviour
    {
        public bool autoCenter = true;
        public float smoothing = 22;
        public float jointGraceSeconds = 0.7f;
        public bool previewOnly;
        public Vector3 StageChest = new Vector3(0, 1.35f, 0);
        public bool BodyLive { get; private set; }
        public bool WeaponHandLive { get; private set; }
        public bool OtherHandLive { get; private set; }
        public bool EverTracked { get; private set; }
        public string Status { get; private set; } = "Preview / no body acquired";
        public Vector3 RawChest { get; private set; }
        public Vector3 RawHand { get; private set; }
        public Vector3 SensorChest { get; private set; }
        public bool HasSensorPosition { get; private set; }
        public float DepthFrameAge => Time.unscaledTime - lastDepthAt;
        public int VisibleJoints { get; private set; }
        public float FrameAge => Time.unscaledTime - lastFrameAt;
        public Vector3[] Joints { get; } = new Vector3[(int)KinectInterop.JointType.Count];
        readonly Vector3[] targets = new Vector3[(int)KinectInterop.JointType.Count];
        readonly float[] seenAt = new float[(int)KinectInterop.JointType.Count];
        readonly Vector3[] samples = new Vector3[(int)KinectInterop.JointType.Count];
        readonly bool[] valid = new bool[(int)KinectInterop.JointType.Count];
        Vector3 origin;
        bool centered;
        ulong frameStamp, userId;
        float lastFrameAt = -100;
        float lastDepthAt = -100;
        ulong depthStamp;
        bool seeded;
        float recenterAt = -1;
        bool waitingForCenter;
        BoidRoomCalibration calibration;
        public float CenterCountdown => recenterAt < 0 ? 0 : Mathf.Max(0, recenterAt - Time.unscaledTime);
        public bool CenterPending => recenterAt >= 0 || waitingForCenter;
        public string CenterStatus => CenterCountdown > 0 ? "Step into position / centering in " + Mathf.CeilToInt(CenterCountdown) + "s" : waitingForCenter ? "Centering armed / waiting for a live body" : "";
        public void ScheduleRecenter(float seconds = 8)
        {
            recenterAt = Time.unscaledTime + seconds;
            waitingForCenter = false;
        }

        public Vector3 Joint(KinectInterop.JointType joint) => Joints[(int)joint];
        public bool JointLive(KinectInterop.JointType joint) => Time.unscaledTime - seenAt[(int)joint] < jointGraceSeconds;
        public void Recenter() { centered = false; }
        void Awake()
        {
            for (int i = 0; i < seenAt.Length; i++) seenAt[i] = -100;
            Preview(null);
        }

        public void Sample(GesturalBoidController controller)
        {
            if (calibration == null) calibration = GetComponent<BoidRoomCalibration>();
            if (recenterAt >= 0 && Time.unscaledTime >= recenterAt)
            {
                recenterAt = -1; waitingForCenter = true;
            }
            var manager = controller.useKinect && !previewOnly ? KinectManager.Instance : null;
            bool ready = manager != null && manager.IsInitialized() && manager.userManager != null;
            if (ready)
            {
                ulong depth = manager.GetDepthFrameTime(0);
                if (depth != 0 && depth != depthStamp) { depthStamp = depth; lastDepthAt = Time.unscaledTime; }
                // Keep the selected identity when player slots change during short occlusions.
                if (userId == 0 || !manager.IsUserTracked(userId)) userId = manager.GetUserIdByIndex(controller.playerIndex);
                ulong stamp = manager.GetBodyFrameTimestamp();
                if (userId != 0 && stamp != frameStamp)
                {
                    frameStamp = stamp;
                    if (manager.IsJointTracked(userId, KinectInterop.JointType.SpineChest))
                    {
                        SensorChest = manager.GetJointKinectPosition(userId, KinectInterop.JointType.SpineChest, false);
                        HasSensorPosition = true;
                    }
                    for (int i = 0; i < samples.Length; i++)
                    {
                        valid[i] = manager.GetJointTrackingState(userId, i) != KinectInterop.TrackingState.NotTracked;
                        samples[i] = valid[i] ? manager.GetJointPosition(userId, i) : Vector3.zero;
                    }
                    ApplyFrame(samples, valid, controller);
                }
            }
            UpdateLiveness(controller);
            if (previewOnly || !EverTracked) Preview(controller);
            else
            {
                float blend = 1 - Mathf.Exp(-smoothing * Time.deltaTime);
                for (int i = 0; i < Joints.Length; i++) Joints[i] = Vector3.Lerp(Joints[i], targets[i], blend);
            }
            if (previewOnly) Status = "Mouse preview enabled (P to return to Kinect)";
            else if (BodyLive) Status = "Kinect live / " + VisibleJoints + " joints" + (WeaponHandLive ? " / hand live" : " / hand held");
            else if (EverTracked) Status = "Tracking lost / holding last pose / step back into sensor view";
            else Status = ready ? "Waiting for a body / preview" : "Kinect not ready / preview";
        }

        // Also used by the deterministic playback check, which feeds partial and missing joint frames.
        public void ApplyFrame(Vector3[] raw, bool[] tracked, GesturalBoidController controller)
        {
            int chest = (int)KinectInterop.JointType.SpineChest;
            if (waitingForCenter && tracked[chest] && Finite(raw[chest])) { centered = false; waitingForCenter = false; }
            int weapon = (int)(controller.leftHanded ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight);
            if (!centered && tracked[chest])
            {
                origin = raw[chest]; centered = true;
                if (calibration != null && calibration.Ready) calibration.CenterPlayer(raw[chest]);
            }
            if (!centered) return;
            VisibleJoints = 0;
            for (int i = 0; i < Joints.Length; i++)
            {
                if (!tracked[i] || !Finite(raw[i])) continue;
                var point = MapToStage(raw[i], controller);
                targets[i] = point;
                if (seenAt[i] < 0) Joints[i] = point;
                seenAt[i] = Time.unscaledTime;
                VisibleJoints++;
            }
            if (tracked[chest]) { RawChest = raw[chest]; EverTracked = true; }
            if (tracked[weapon]) RawHand = raw[weapon];
            lastFrameAt = Time.unscaledTime;
            UpdateLiveness(controller);
        }
        void UpdateLiveness(GesturalBoidController controller)
        {
            BodyLive = EverTracked && JointLive(KinectInterop.JointType.SpineChest) && !previewOnly;
            WeaponHandLive = BodyLive && JointLive(controller.leftHanded ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight);
            OtherHandLive = BodyLive && JointLive(controller.leftHanded ? KinectInterop.JointType.HandRight : KinectInterop.JointType.HandLeft);
        }
        public Vector3 MapToStage(Vector3 point, GesturalBoidController controller)
        {
            if (calibration != null && calibration.Ready)
            {
                point = calibration.ToStage(point);
                if (controller.mirrorX) point.x = -point.x;
                return point;
            }
            if (autoCenter) point -= origin;
            if (controller.mirrorX) point.x = -point.x;
            point = Vector3.Scale(point, controller.trackingScale);
            if (autoCenter) return StageChest + point;
            return point + controller.trackingOffset;
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.sqrMagnitude) && !float.IsInfinity(p.sqrMagnitude);
        void Preview(GesturalBoidController controller)
        {
            if (!seeded || previewOnly || !EverTracked)
            {
                Set(KinectInterop.JointType.Pelvis, new Vector3(0, 0.92f, 0));
                Set(KinectInterop.JointType.SpineNaval, new Vector3(0, 1.1f, 0));
                Set(KinectInterop.JointType.SpineChest, StageChest);
                Set(KinectInterop.JointType.Neck, new Vector3(0, 1.53f, 0));
                Set(KinectInterop.JointType.Head, new Vector3(0, 1.7f, 0));
                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? -1 : 1;
                    Set(side == 0 ? KinectInterop.JointType.ShoulderLeft : KinectInterop.JointType.ShoulderRight, new Vector3(sign * 0.23f, 1.4f, 0));
                    Set(side == 0 ? KinectInterop.JointType.ElbowLeft : KinectInterop.JointType.ElbowRight, new Vector3(sign * 0.4f, 1.17f, 0));
                    Set(side == 0 ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight, new Vector3(sign * 0.55f, 1.4f, -0.1f));
                    Set(side == 0 ? KinectInterop.JointType.HipLeft : KinectInterop.JointType.HipRight, new Vector3(sign * 0.13f, 0.9f, 0));
                    Set(side == 0 ? KinectInterop.JointType.KneeLeft : KinectInterop.JointType.KneeRight, new Vector3(sign * 0.15f, 0.5f, 0));
                    Set(side == 0 ? KinectInterop.JointType.AnkleLeft : KinectInterop.JointType.AnkleRight, new Vector3(sign * 0.17f, 0.1f, 0));
                    Set(side == 0 ? KinectInterop.JointType.FootLeft : KinectInterop.JointType.FootRight, new Vector3(sign * 0.17f, 0.06f, -0.13f));
                }
                if (controller != null)
                {
                    var hand = controller.leftHanded ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight;
                    var other = controller.leftHanded ? KinectInterop.JointType.HandRight : KinectInterop.JointType.HandLeft;
                    var pointer = Input.mousePosition;
                    if (Camera.main != null && pointer.x > 0 && pointer.y > 0 && pointer.x < Screen.width && pointer.y < Screen.height)
                    {
                        var ray = Camera.main.ScreenPointToRay(pointer);
                        if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) Set(hand, ray.GetPoint(distance));
                    }
                    if (Input.GetKey(KeyCode.Q)) Set(other, Joint(hand) + Vector3.left * 0.2f);
                }
                Set(KinectInterop.JointType.WristLeft, Joint(KinectInterop.JointType.HandLeft));
                Set(KinectInterop.JointType.WristRight, Joint(KinectInterop.JointType.HandRight));
                seeded = true;
            }
        }
        void Set(KinectInterop.JointType joint, Vector3 p) { Joints[(int)joint] = targets[(int)joint] = p; }
    }
}
