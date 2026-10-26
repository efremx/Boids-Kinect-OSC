using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    // Room image and skeleton are both in raw sensor image coordinates, independent of avatar recentering.
    public class BoidRoomSetup : MonoBehaviour
    {
        public bool SetupMode = true;
        public bool mirrorImage = true;
        public bool showSkeleton = true;
        public bool useColor;
        public float depthRangeMeters = 6;
        public float previewRate = 10;
        GesturalBoidController controller;
        Texture2D depth;
        Color32[] pixels;
        readonly Vector2[] projected = new Vector2[32];
        readonly KinectInterop.TrackingState[] confidence = new KinectInterop.TrackingState[32];
        readonly bool[] visible = new bool[32];
        float nextPreview, lastDepthAt = -100, lastColorAt = -100, lastSkeletonAt = -100;
        ulong depthStamp, colorStamp, bodyStamp;
        float elapsed, frameTotal, peakFrame, fps, frameMs, worstMs;
        int frameCount;
        bool projectedColor;
        Vector2 scroll;
        bool advancedSetup, gripPending, gripHistory;
        float gripReadyAt, gripDeadline, gripStill;
        Vector3 lastGripHand, lastGripChest;
        Quaternion lastGripRotation;
        string gripStatus = "Hold the phone in a comfortable ready pose, blade pointing up. Room picking is optional.";

        void BeginGripSetup()
        {
            gripPending = true; gripHistory = false; gripStill = 0;
            gripReadyAt = Time.unscaledTime + 8; gripDeadline = gripReadyAt + 20;
        }

        void UpdateGripSetup()
        {
            if (!gripPending || controller == null) return;
            if (!SetupMode) { gripPending = false; gripStatus = "Setup canceled. Return to Setup to calibrate."; return; }
            float now = Time.unscaledTime;
            if (now < gripReadyAt) { gripStatus = "Step into position / ready pose in " + Mathf.CeilToInt(gripReadyAt-now) + "s"; return; }
            if (now > gripDeadline) { gripPending = false; gripStatus = "No stable capture. Check body/phone streams, then retry."; return; }
            var pose = controller.Pose;
            bool live = pose.BodyLive && pose.WeaponHandLive && pose.FrameAge < 0.2f && controller.Phone.HasOrientation;
            if (!live) { gripStill = 0; gripHistory = false; gripStatus = "Waiting for a fresh body, weapon hand and phone rotation."; return; }
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            Vector3 hand = pose.RawHand, chest = pose.RawChest;
            Quaternion rotation = controller.Phone.Orientation;
            bool stable = gripHistory && Vector3.Distance(hand,lastGripHand)/dt < 0.18f && Vector3.Distance(chest,lastGripChest)/dt < 0.12f && Quaternion.Angle(rotation,lastGripRotation)/dt < 25;
            lastGripHand = hand; lastGripChest = chest; lastGripRotation = rotation; gripHistory = true;
            gripStill = stable ? gripStill + Time.unscaledDeltaTime : 0;
            gripStatus = "Hold ready pose still / " + Mathf.RoundToInt(Mathf.Clamp01(gripStill/0.8f)*100) + "%";
            if (gripStill < 0.8f) return;
            controller.Phone.Calibrate(false); pose.ScheduleRecenter(0);
            gripPending = false;
            gripStatus = "Captured. Tab to Play and check reach. C: recalibrate phone lying screen-up on table.";
        }
        static readonly int[,] Bones = {
            {0,1},{1,2},{2,3},{3,4}, {2,5},{5,6},{6,7},{7,8},{8,9},
            {2,10},{10,11},{11,12},{12,13},{13,14},
            {0,15},{15,16},{16,17},{17,18}, {0,19},{19,20},{20,21},{21,22}
        };
        void Start() { controller = GetComponent<GesturalBoidController>(); }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) SetupMode = !SetupMode;
            UpdateGripSetup();
            float dt = Time.unscaledDeltaTime;
            frameTotal += dt; peakFrame = Mathf.Max(peakFrame, dt); frameCount++; elapsed += dt;
            if (elapsed >= 0.75f)
            {
                fps = frameCount / Mathf.Max(frameTotal, 0.001f); frameMs = frameTotal * 1000 / frameCount; worstMs = peakFrame * 1000;
                elapsed = frameTotal = peakFrame = 0; frameCount = 0;
            }
            var manager = KinectManager.Instance;
            if (manager == null || !manager.IsInitialized()) return;
            ulong ds = manager.GetDepthFrameTime(0), cs = manager.GetColorFrameTime(0);
            if (ds != 0 && ds != depthStamp) { depthStamp = ds; lastDepthAt = Time.unscaledTime; }
            if (cs != 0 && cs != colorStamp) { colorStamp = cs; lastColorAt = Time.unscaledTime; }
            if (!SetupMode) return;
            var data = manager.GetSensorData(0);
            if (data == null) return;
            if (Time.unscaledTime >= nextPreview && data?.depthImage != null && data.depthImageWidth > 0 && data.depthImageHeight > 0)
            {
                nextPreview = Time.unscaledTime + 1 / Mathf.Max(1, previewRate);
                int width = Mathf.Min(320, data.depthImageWidth);
                int height = Mathf.Max(1, Mathf.RoundToInt(width * (float)data.depthImageHeight / data.depthImageWidth));
                if (depth == null || depth.width != width || depth.height != height)
                {
                    if (depth != null) Destroy(depth);
                    depth = new Texture2D(width, height, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear };
                    pixels = new Color32[width * height];
                }
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int index = (y * data.depthImageHeight / height) * data.depthImageWidth + x * data.depthImageWidth / width;
                    float distance = index < data.depthImage.Length ? data.depthImage[index] * 0.001f : 0;
                    float shade = 1 - Mathf.Clamp01(distance / Mathf.Max(0.5f, depthRangeMeters));
                    pixels[(height - 1 - y) * width + x] = distance <= 0 ? new Color(0.025f, 0.025f, 0.035f) : new Color(shade, shade, shade);
                }
                depth.SetPixels32(pixels); depth.Apply(false);
            }
            bool color = useColor && manager.GetColorImageTex(0) != null;
            if (projectedColor != color)
            {
                projectedColor = color; bodyStamp = 0;
                for (int i = 0; i < visible.Length; i++) visible[i] = false;
            }
            ulong stamp = manager.GetBodyFrameTimestamp();
            if (manager.userManager == null || stamp == 0 || stamp == bodyStamp) return;
            bodyStamp = stamp;
            ulong id = manager.GetUserIdByIndex(controller.playerIndex);
            if (id == 0) return; // Retain last projection, explicitly colored as stale below.
            int imageWidth = color ? manager.GetColorImageWidth(0) : data.depthImageWidth;
            int imageHeight = color ? manager.GetColorImageHeight(0) : data.depthImageHeight;
            if (imageWidth < 1 || imageHeight < 1) return;
            for (int i = 0; i < 32; i++)
            {
                confidence[i] = manager.GetJointTrackingState(id, i);
                visible[i] = confidence[i] != KinectInterop.TrackingState.NotTracked;
                if (!visible[i]) continue;
                var point = manager.GetJointKinectPosition(id, i, false);
                var image = color ? manager.MapSpacePointToColorCoords(0, point) : manager.MapSpacePointToDepthCoords(0, point);
                projected[i] = new Vector2(image.x / imageWidth, image.y / imageHeight);
                visible[i] = Finite(projected[i]) && projected[i].x >= 0 && projected[i].x <= 1 && projected[i].y >= 0 && projected[i].y <= 1;
            }
            lastSkeletonAt = Time.unscaledTime;
        }
        static bool Finite(Vector2 p) => !float.IsNaN(p.sqrMagnitude) && !float.IsInfinity(p.sqrMagnitude);
        static string Age(float time) => time < 0 ? "none" : (Time.unscaledTime - time).ToString("F1") + "s";
        public string Performance => fps.ToString("F0") + " FPS / " + frameMs.ToString("F1") + " ms avg / " + worstMs.ToString("F1") + " ms peak";
        void OnGUI()
        {
            if (controller == null) return;
            if (!SetupMode)
            {
                GUI.Label(new Rect(12, Screen.height - 26, Screen.width - 24, 24), "Tab: room setup | " + Performance + " / flock CPU " + controller.LastFlockMilliseconds.ToString("F2") + " ms");
                return;
            }
            int previousDepth = GUI.depth; GUI.depth = -50;
            var oldColor = GUI.color; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = oldColor;
            GUI.skin.label.wordWrap = true;
            GUI.Label(new Rect(12, 8, Screen.width - 24, 42), "ROOM SETUP / actual Kinect image\nTab: return to Play | green: tracked / yellow: inferred / red: held, stale skeleton");
            float sidebar = Mathf.Min(290, Screen.width * 0.34f);
            Rect available = new Rect(12, 54, Screen.width - sidebar - 36, Mathf.Max(80, Screen.height - 95));
            var manager = KinectManager.Instance;
            bool ready = manager != null && manager.IsInitialized();
            bool color = useColor && ready && manager.GetColorImageTex(0) != null;
            Texture image = color ? manager.GetColorImageTex(0) : depth;
            bool flipX = mirrorImage ^ (color && manager.GetColorImageScale(0).x < 0);
            bool flipY = color && manager.GetColorImageScale(0).y < 0;
            if (image != null)
            {
                float ratio = (float)image.width / image.height;
                float width = Mathf.Min(available.width, available.height * ratio), height = width / ratio;
                Rect rect = new Rect(available.center.x - width / 2, available.center.y - height / 2, width, height);
                GUI.DrawTextureWithTexCoords(rect, image, new Rect(flipX ? 1 : 0, flipY ? 1 : 0, flipX ? -1 : 1, flipY ? -1 : 1));
                DrawGuides(rect);
                if (showSkeleton && projectedColor == color) DrawSkeleton(rect, flipX, color && !flipY);
                if (ready && controller.Calibration != null)
                {
                    controller.Calibration.DrawImageGuides(rect, flipX, color && !flipY, manager, color);
                    if (!color) controller.Calibration.HandleImageClick(rect, flipX, false, manager, Time.unscaledTime - lastDepthAt < 0.5f);
                }
                GUI.Label(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 24), (color ? "COLOR" : "DEPTH / closer = brighter") + " / " + image.width + " x " + image.height);
            }
            else GUI.Label(available, "Waiting for Kinect depth image.\nEnable the Kinect Rig before Play and check the sensor connection. The room image does not require a tracked body.");
            GUILayout.BeginArea(new Rect(Screen.width - sidebar - 12, 54, sidebar, Screen.height - 66), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("1 / POSITION THE SENSOR");
            GUILayout.Label("See floor, walls and ceiling here. Keep feet and raised hands visible. Black depth pixels mean no usable depth, not empty space.");
            mirrorImage = GUILayout.Toggle(mirrorImage, "Mirror room image (selfie view)");
            showSkeleton = GUILayout.Toggle(showSkeleton, "Skeleton over room image");
            useColor = GUILayout.Toggle(useColor, "Use color image when available");
            if (useColor && !color) GUILayout.Label("Showing depth. Color capture is off: before Play, set KinectManager > Get Color Frames to Color Texture. No sensor restart is performed automatically.");
            GUILayout.Label("Depth range: " + depthRangeMeters.ToString("F1") + "m"); depthRangeMeters = GUILayout.HorizontalSlider(depthRangeMeters, 2, 10);
            GUILayout.Space(8);
            GUILayout.Label("2 / PLAYER + WEAPON SETUP");
            GUILayout.Label(gripStatus);
            if (GUILayout.Button(gripPending ? "Restart 8-second ready-pose capture" : "Calibrate player + phone in 8 seconds")) BeginGripSetup();
            if (gripPending && GUILayout.Button("Cancel capture")) { gripPending = false; gripStatus = "Capture canceled; previous alignment retained."; }
            GUILayout.Label("Neutral capture makes the blade point up. Hold the phone the way you want to grip the sword; move it to test reach after capture.");
            GUILayout.Label(controller.Pose.Status);
            if (controller.Pose.CenterStatus.Length > 0) GUILayout.Label(controller.Pose.CenterStatus);
            if (GUILayout.Button("Center in 8 seconds / step back")) controller.Pose.ScheduleRecenter();
            GUILayout.Label("Room mirroring is separate from avatar mirroring. Recentring the avatar does not move this room image or its skeleton.");
            GUILayout.Space(8);
            advancedSetup = GUILayout.Toggle(advancedSetup, "Advanced room setup (optional)");
            if (advancedSetup && controller.Calibration != null)
            {
                controller.Calibration.DrawControls();
                if (controller.Calibration.Ready) GUILayout.Label("Saved floor alignment active; recenter shifts X/Z only. Without floor picking, the floor is estimated from chest height. Both paths retain full depth movement.");
                if (controller.Calibration.BoundsReady && controller.Pose.BodyLive) GUILayout.Label(controller.Calibration.Inside(controller.Pose.RawChest) ? "BODY INSIDE PLAY AREA" : "BODY OUTSIDE PLAY AREA");
            }
            GUILayout.Space(8);
            GUILayout.Label("STREAM HEALTH");
            GUILayout.Label("Depth: " + Age(lastDepthAt) + " / color: " + Age(lastColorAt) + "\nBody projection: " + Age(lastSkeletonAt));
            GUILayout.Label(Performance);
            GUILayout.Label("Flock: " + (SetupMode ? "paused in Setup" : controller.LastFlockMilliseconds.ToString("F2") + " ms") + " / " + controller.boidCount + " birds/points");
            GUILayout.Label("Phone packets: " + controller.Phone.PacketCount + " / " + controller.Phone.RotationSource);
            if (GUILayout.Button("Play / resume flock (Tab)")) SetupMode = false;
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUI.Label(new Rect(12, Screen.height - 30, Screen.width - 24, 28), "Check feet, raised hands and sideways reach. The image is the sensor's real view; the guides are visual references, not detected room geometry.");
            GUI.depth = previousDepth;
        }
        void DrawSkeleton(Rect rect, bool flipX, bool flipY)
        {
            bool stale = !controller.Pose.BodyLive || Time.unscaledTime - lastSkeletonAt > controller.Pose.jointGraceSeconds;
            for (int i = 0; i < Bones.GetLength(0); i++)
            {
                int a = Bones[i, 0], b = Bones[i, 1]; if (!visible[a] || !visible[b]) continue;
                Color tint = stale ? new Color(1, 0.25f, 0.2f, 0.65f) : confidence[a] == KinectInterop.TrackingState.Inferred || confidence[b] == KinectInterop.TrackingState.Inferred ? Color.yellow : Color.green;
                Line(ImagePoint(rect, projected[a], flipX, flipY), ImagePoint(rect, projected[b], flipX, flipY), tint, 3);
            }
        }
        static Vector2 ImagePoint(Rect r, Vector2 uv, bool flipX, bool flipY) => new Vector2(r.x + (flipX ? 1 - uv.x : uv.x) * r.width, r.y + (flipY ? 1 - uv.y : uv.y) * r.height);
        static void DrawGuides(Rect rect)
        {
            var tint = new Color(1, 1, 1, 0.2f);
            for (int i = 1; i < 4; i++)
            {
                float x = rect.x + rect.width * i / 4, y = rect.y + rect.height * i / 4;
                Line(new Vector2(x, rect.y), new Vector2(x, rect.yMax), tint, 1);
                Line(new Vector2(rect.x, y), new Vector2(rect.xMax, y), tint, 1);
            }
        }
        public static void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; Vector2 delta = b - a;
            GUI.color = color; GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix; GUI.color = oldColor;
        }
        void OnDestroy() { if (depth != null) Destroy(depth); }
    }
}
