using System;
using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    public class BoidRoomCalibration : MonoBehaviour
    {
        const string SaveKey = "GesturalBirds.RoomCalibration.v1";
        [Serializable] class Settings
        {
            public int version = 1;
            public bool floorSet, boundsSet;
            public Vector3 floorA, floorB, floorC;
            public float scale = 1, left = -1.2f, right = 1.2f, near = 0, far = 2;
        }
        Settings settings = new Settings();
        enum Pick { None, Floor, Measure, Bounds }
        Pick picking;
        readonly Vector3[] picks = new Vector3[3];
        readonly Vector3[] measure = new Vector3[2];
        int count;
        bool measured;
        Quaternion rotation = Quaternion.identity;
        Vector3 center, playerOffset;
        public bool Ready => settings.floorSet;
        public bool BoundsReady => Ready && settings.boundsSet;
        public float Scale => settings.scale;
        public string Message { get; private set; } = "Mark three clear floor points, spaced apart.";
        public bool Picking => picking != Pick.None;
        public Vector3 ToRoom(Vector3 world) => rotation * (world - center) * settings.scale;
        public Vector3 ToStage(Vector3 world) => ToRoom(world) - playerOffset;
        public Vector3 ToWorld(Vector3 room) => center + Quaternion.Inverse(rotation) * room / settings.scale;
        public void CenterPlayer(Vector3 world)
        {
            var point = ToRoom(world); playerOffset = new Vector3(point.x, 0, point.z);
        }
        public bool Inside(Vector3 world)
        {
            var p = ToRoom(world);
            return BoundsReady && p.x >= settings.left && p.x <= settings.right && p.z >= settings.near && p.z <= settings.far;
        }
        void Awake() { Load(); }
        bool BuildFloor()
        {
            Vector3 normal = Vector3.Cross(settings.floorB - settings.floorA, settings.floorC - settings.floorA);
            if (normal.magnitude < 0.02f) return false;
            normal.Normalize(); if (normal.y < 0) normal = -normal;
            if (Vector3.Dot(normal, Vector3.up) < 0.5f) return false;
            center = (settings.floorA + settings.floorB + settings.floorC) / 3;
            rotation = Quaternion.FromToRotation(normal, Vector3.up);
            return true;
        }
        void Begin(Pick mode)
        {
            picking = mode; count = 0;
            var setup = GetComponent<BoidRoomSetup>(); setup.useColor = false;
            Message = mode == Pick.Floor ? "Click 3 floor points forming a broad triangle. Avoid furniture/edges." : mode == Pick.Measure ? "Click the two floor markers that you placed 1 meter apart." : "Click two opposite corners of the clear floor play area.";
        }
        public void DrawControls()
        {
            GUILayout.Space(8); GUILayout.Label("ROOM CALIBRATION");
            GUILayout.Label(Message);
            if (GUILayout.Button("Mark floor / 3 image clicks")) Begin(Pick.Floor);
            if (GUILayout.Button("Check 1m markers / 2 image clicks")) Begin(Pick.Measure);
            GUI.enabled = Ready;
            if (GUILayout.Button("Mark play bounds / 2 floor clicks")) Begin(Pick.Bounds);
            GUI.enabled = true;
            if (Picking && GUILayout.Button("Cancel picking")) { picking = Pick.None; count = 0; Message = "Picking canceled; prior calibration retained."; }
            if (measured)
            {
                float meters = Vector3.Distance(measure[0], measure[1]);
                GUILayout.Label("Marker distance: " + meters.ToString("F3") + " sensor meters / " + (meters * settings.scale).ToString("F3") + " stage units");
                if (GUILayout.Button("Set these markers to exactly 1 unit"))
                {
                    if (meters < 0.25f || meters > 4 || !IsNumber(meters)) Message = "That distance is implausible for 1m markers. Select the markers again.";
                    else
                    {
                        settings.scale = 1 / meters; settings.boundsSet = false; playerOffset = Vector3.zero;
                        Message = "Scale adjusted from selected markers. Re-mark bounds, then save.";
                    }
                }
            }
            GUILayout.Label("Room scale: " + settings.scale.ToString("F3") + " units/m" + (Ready ? " / floor aligned" : " / no floor yet"));
            if (GUILayout.Button("Use physical 1:1 scale")) { settings.scale = 1; settings.boundsSet = false; playerOffset = Vector3.zero; Message = "1:1 scale selected. Re-mark bounds if needed."; }
            if (BoundsReady) GUILayout.Label("Play area: " + (settings.right - settings.left).ToString("F2") + " x " + (settings.far - settings.near).ToString("F2") + " stage units");
            GUI.enabled = Ready;
            if (GUILayout.Button("Save room calibration"))
            {
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(settings)); PlayerPrefs.Save();
                Message = "Room saved locally. R recenters the player only. Recalibrate if you move the sensor.";
            }
            GUI.enabled = true;
            if (GUILayout.Button("Reload saved room")) Load();
            if (GUILayout.Button("Use temporary chest-centered mapping"))
            { settings.floorSet = false; picking = Pick.None; GetComponent<BoidBodyPose>().ScheduleRecenter(); Message = "Floor mapping disabled for this session. Saved room retained."; }
        }
        public void HandleImageClick(Rect rect, bool flipX, bool flipY, KinectManager manager, bool freshDepth)
        {
            if (!Picking || Event.current.type != EventType.MouseDown || Event.current.button != 0 || !rect.Contains(Event.current.mousePosition)) return;
            Event.current.Use();
            if (!freshDepth) { Message = "Depth frame is stale. Wait for live depth before marking."; return; }
            var data = manager.GetSensorData(0); if (data?.depthImage == null) return;
            Vector2 uv = new Vector2((Event.current.mousePosition.x - rect.x) / rect.width, (Event.current.mousePosition.y - rect.y) / rect.height);
            if (flipX) uv.x = 1 - uv.x; if (flipY) uv.y = 1 - uv.y;
            int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * (data.depthImageWidth - 1)), 0, data.depthImageWidth - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(uv.y * (data.depthImageHeight - 1)), 0, data.depthImageHeight - 1);
            ushort value = manager.GetDepthForPixel(0, x, y);
            if (value == 0) { Message = "No depth at that pixel. Pick a clear floor surface."; return; }
            Vector3 world = manager.MapDepthPointToSpaceCoords(0, new Vector2(x, y), value, true);
            if (!Finite(world)) { Message = "Invalid depth point. Choose another surface."; return; }
            picks[count++] = world;
            int required = picking == Pick.Floor ? 3 : 2;
            if (count < required) { Message = "Point " + count + "/" + required + " selected. Choose the next point."; return; }
            if (picking == Pick.Floor)
            {
                var previous = JsonUtility.ToJson(settings);
                settings.floorA = picks[0]; settings.floorB = picks[1]; settings.floorC = picks[2];
                if (!BuildFloor())
                {
                    settings = JsonUtility.FromJson<Settings>(previous); if (Ready) BuildFloor();
                    Message = "Those points do not describe a plausible floor triangle. Try points farther apart on the floor.";
                }
                else { settings.floorSet = true; settings.boundsSet = false; playerOffset = Vector3.zero; Message = "Floor aligned. Check meter markers, choose bounds, then save."; }
            }
            else if (picking == Pick.Measure)
            { measure[0] = picks[0]; measure[1] = picks[1]; measured = true; Message = "Compare the measured distance with your physical 1m markers. Scale is unchanged until you choose to adjust it."; }
            else
            {
                Vector3 a = ToRoom(picks[0]), b = ToRoom(picks[1]);
                if (Mathf.Abs(a.y) > 0.15f || Mathf.Abs(b.y) > 0.15f) Message = "Bounds points must be on the calibrated floor (within 15cm).";
                else if (Mathf.Abs(a.x - b.x) < 0.3f || Mathf.Abs(a.z - b.z) < 0.3f) Message = "Choose opposite corners of a larger rectangle.";
                else
                {
                    settings.left = Mathf.Min(a.x, b.x); settings.right = Mathf.Max(a.x, b.x);
                    settings.near = Mathf.Min(a.z, b.z); settings.far = Mathf.Max(a.z, b.z);
                    settings.boundsSet = true; Message = "Green play area marked. Check full reach inside it, then save. Bounds are guides, not enforced walls.";
                }
            }
            picking = Pick.None; count = 0;
        }
        void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                var candidate = JsonUtility.FromJson<Settings>(PlayerPrefs.GetString(SaveKey));
                if (candidate == null || candidate.version != 1 || !candidate.floorSet || !IsNumber(candidate.scale) || candidate.scale < 0.25f || candidate.scale > 4 || !Finite(candidate.floorA) || !Finite(candidate.floorB) || !Finite(candidate.floorC)) throw new ArgumentException();
                if (candidate.boundsSet && (!IsNumber(candidate.left) || !IsNumber(candidate.right) || !IsNumber(candidate.near) || !IsNumber(candidate.far) || candidate.right <= candidate.left || candidate.far <= candidate.near)) throw new ArgumentException();
                var previous = settings; settings = candidate;
                if (!BuildFloor()) { settings = previous; if (Ready) BuildFloor(); throw new ArgumentException(); }
                playerOffset = Vector3.zero; picking = Pick.None; Message = "Saved room loaded. Recalibrate if the sensor position/angle changed.";
            }
            catch (ArgumentException) { Message = "Saved calibration is invalid; mark the floor again."; }
        }
        static bool IsNumber(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector3 p) => IsNumber(p.sqrMagnitude);
        public void DrawImageGuides(Rect rect, bool flipX, bool flipY, KinectManager manager, bool color)
        {
            if (Ready)
            {
                Mark(settings.floorA, "F1", Color.white); Mark(settings.floorB, "F2", Color.white); Mark(settings.floorC, "F3", Color.white);
                if (BoundsReady)
                {
                    Vector3[] corners = { new Vector3(settings.left, 0, settings.near), new Vector3(settings.right, 0, settings.near), new Vector3(settings.right, 0, settings.far), new Vector3(settings.left, 0, settings.far) };
                    for (int i = 0; i < 4; i++) if (Project(ToWorld(corners[i]), out var a) && Project(ToWorld(corners[(i + 1) % 4]), out var b)) BoidRoomSetup.Line(a, b, Color.green, 3);
                }
            }
            if (measured) { Mark(measure[0], "A", Color.cyan); Mark(measure[1], "B", Color.cyan); }
            for (int i = 0; i < count; i++) Mark(picks[i], (i + 1).ToString(), Color.yellow);
            bool Project(Vector3 world, out Vector2 point)
            {
                point = Vector2.zero; var data = manager.GetSensorData(0); if (data == null) return false;
                Vector3 sensor = manager.GetSensorToWorldMatrix(0).inverse.MultiplyPoint3x4(world);
                var scale = manager.GetSensorSpaceScale(0);
                sensor = new Vector3(sensor.x / scale.x, sensor.y / scale.y, sensor.z / scale.z);
                if (sensor.z <= 0) return false;
                Vector2 pixel = color ? manager.MapSpacePointToColorCoords(0, sensor) : manager.MapSpacePointToDepthCoords(0, sensor);
                int width = color ? manager.GetColorImageWidth(0) : data.depthImageWidth, height = color ? manager.GetColorImageHeight(0) : data.depthImageHeight;
                if (width <= 0 || height <= 0) return false;
                Vector2 uv = new Vector2(pixel.x / width, pixel.y / height);
                if (!IsNumber(uv.sqrMagnitude) || uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return false;
                point = new Vector2(rect.x + (flipX ? 1 - uv.x : uv.x) * rect.width, rect.y + (flipY ? 1 - uv.y : uv.y) * rect.height); return true;
            }
            void Mark(Vector3 world, string label, Color tint)
            {
                if (!Project(world, out var point)) return;
                var previous = GUI.color; GUI.color = tint;
                GUI.DrawTexture(new Rect(point.x - 4, point.y - 4, 8, 8), Texture2D.whiteTexture);
                GUI.Label(new Rect(point.x + 6, point.y - 10, 32, 22), label); GUI.color = previous;
            }
        }
    }
}
