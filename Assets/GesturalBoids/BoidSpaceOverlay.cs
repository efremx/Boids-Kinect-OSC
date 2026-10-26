using UnityEngine;
using UnityEngine.Rendering;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    public class BoidSpaceOverlay : MonoBehaviour
    {
        public bool showOverlay = true;
        public bool showDepth = true;
        public float nearDistance = 1.2f;
        public float farDistance = 3.5f;
        public float playWidth = 2.4f;
        public float playCenterX;
        public float estimatedHorizontalFov = 70;
        public float estimatedVerticalFov = 60;
        public float mapRange = 5;
        GesturalBoidController controller;
        Texture2D depthTexture;
        readonly Color32[] pixels = new Color32[160 * 120];
        Material lines;
        float nextDepth;
        bool calibratedLens;
        float horizontalFov, verticalFov;
        Vector2 scroll;
        float centerAreaAt = -1;
        const float MapWidth = 270, MapHeight = 200;

        void Start()
        {
            controller = GetComponent<GesturalBoidController>();
            lines = new Material(Shader.Find("Hidden/Internal-Colored"));
            lines.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); lines.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            lines.SetInt("_Cull", (int)CullMode.Off); lines.SetInt("_ZWrite", 0);
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.O)) showOverlay = !showOverlay;
            if (centerAreaAt >= 0 && Time.unscaledTime >= centerAreaAt && controller.Pose.BodyLive)
            { playCenterX = controller.Pose.SensorChest.x; centerAreaAt = -1; }
            horizontalFov = estimatedHorizontalFov; verticalFov = estimatedVerticalFov; calibratedLens = false;
            var manager = KinectManager.Instance;
            var data = manager != null && manager.IsInitialized() ? manager.GetSensorData(0) : null;
            var intr = data?.depthCamIntr;
            if (intr != null && intr.hFOV > 1 && intr.vFOV > 1)
            { horizontalFov = intr.hFOV; verticalFov = intr.vFOV; calibratedLens = true; }
            if (controller.Room != null && controller.Room.SetupMode) return;
            if (!showOverlay || !showDepth || Time.unscaledTime < nextDepth || data?.depthImage == null || data.depthImageWidth < 1 || data.depthImageHeight < 1) return;
            nextDepth = Time.unscaledTime + 0.15f;
            if (depthTexture == null) depthTexture = new Texture2D(160, 120, TextureFormat.RGB24, false);
            for (int y = 0; y < 120; y++)
                for (int x = 0; x < 160; x++)
                {
                    int sx = x * data.depthImageWidth / 160, sy = y * data.depthImageHeight / 120;
                    int index = sy * data.depthImageWidth + sx;
                    float z = index < data.depthImage.Length ? data.depthImage[index] * 0.001f : 0;
                    pixels[(119 - y) * 160 + x] = z <= 0 ? Color.black : Color.Lerp(new Color(0.9f, 0.95f, 1), new Color(0.05f, 0.16f, 0.3f), Mathf.Clamp01(z / mapRange));
                }
            depthTexture.SetPixels32(pixels); depthTexture.Apply(false);
        }
        void OnGUI()
        {
            if (!showOverlay || controller == null || (controller.Room != null && controller.Room.SetupMode)) return;
            float width = Mathf.Min(310, Screen.width * 0.38f);
            GUILayout.BeginArea(new Rect(Screen.width - width - 8, 8, width, Screen.height - 16), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("PHYSICAL SPACE / O to hide");
            GUILayout.Label("Top view: sensor at bottom / meters");
            Rect map = GUILayoutUtility.GetRect(MapWidth, MapHeight, GUILayout.ExpandWidth(true));
            DrawMap(map);
            GUILayout.Label((calibratedLens ? "Sensor lens" : "Estimated lens") + ": " + horizontalFov.ToString("F0") + " x " + verticalFov.ToString("F0") + " degrees");
            float z = controller.Pose.HasSensorPosition ? controller.Pose.SensorChest.z : (nearDistance + farDistance) * 0.5f;
            GUILayout.Label("Coverage at " + z.ToString("F2") + "m: ~" + (2 * z * Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2)).ToString("F2") + "m wide");
            GUILayout.Label(controller.Pose.HasSensorPosition ? "Body sensor X/Z: " + controller.Pose.SensorChest.x.ToString("F2") + " / " + z.ToString("F2") + "m" : "No body position acquired yet");
            bool savedBounds = controller.Calibration != null && controller.Calibration.BoundsReady;
            GUILayout.Label(savedBounds ? "Cyan = lens estimate / saved floor bounds shown in Room Setup (Tab)" : "Cyan = lens estimate / green = temporary sensor-space play area");
            GUILayout.Label(controller.Calibration != null && controller.Calibration.Ready ? "Map uses sensor meters. 3D grid = 0.5 stage units; saved floor mapping is active." : "Map uses physical meters. 3D grid = 0.5 stage units; 3D depth follows the compressed avatar mapping.");
            GUILayout.Label("Lens coverage does not guarantee body detection.");
            if (!savedBounds)
            {
            GUILayout.Label("Near: " + nearDistance.ToString("F1") + "m"); nearDistance = GUILayout.HorizontalSlider(nearDistance, 0.5f, 4);
            GUILayout.Label("Far: " + farDistance.ToString("F1") + "m"); farDistance = GUILayout.HorizontalSlider(farDistance, nearDistance + 0.2f, 6);
            GUILayout.Label("Play width: " + playWidth.ToString("F1") + "m"); playWidth = GUILayout.HorizontalSlider(playWidth, 0.5f, 5);
            if (GUILayout.Button("Center play area in 8 seconds"))
            { centerAreaAt = Time.unscaledTime + 8; controller.Pose.ScheduleRecenter(); }
            }
            showDepth = GUILayout.Toggle(showDepth, "Raw depth preview (closer = brighter)");
            if (showDepth)
            {
                var rect = GUILayoutUtility.GetRect(160, 120, GUILayout.ExpandWidth(true));
                if (depthTexture != null) GUI.DrawTexture(rect, depthTexture, ScaleMode.ScaleToFit);
                else GUI.Label(rect, "No raw depth image available");
            }
            GUILayout.Label("Depth age: " + Age(controller.Pose.DepthFrameAge) + " / body age: " + Age(controller.Pose.FrameAge));
            GUILayout.Label("Depth live + body stale = body tracker lost you.\nBoth stale = check sensor/stream.");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        static string Age(float age) => age > 90 ? "--" : age.ToString("F1") + "s";
        Vector2 Point(Rect rect, float x, float z) => new Vector2(rect.center.x + x / (mapRange * 2) * rect.width, rect.yMax - 10 - z / mapRange * (rect.height - 20));
        void DrawMap(Rect rect)
        {
            GUI.BeginGroup(rect); var r = new Rect(0, 0, rect.width, rect.height);
            for (int m = 0; m <= (int)mapRange; m++)
            {
                DrawLine(Point(r, -mapRange, m), Point(r, mapRange, m), new Color(0.22f, 0.25f, 0.3f));
                if (m > 0) GUI.Label(new Rect(2, Point(r, 0, m).y - 14, 35, 18), m + "m");
            }
            for (int m = -(int)mapRange; m <= (int)mapRange; m++) DrawLine(Point(r, m, 0), Point(r, m, mapRange), new Color(0.22f, 0.25f, 0.3f));
            float extent = mapRange * Mathf.Tan(horizontalFov * Mathf.Deg2Rad * 0.5f);
            DrawLine(Point(r, 0, 0), Point(r, -extent, mapRange), Color.cyan); DrawLine(Point(r, 0, 0), Point(r, extent, mapRange), Color.cyan);
            bool savedBounds = controller.Calibration != null && controller.Calibration.BoundsReady;
            if (!savedBounds)
            {
            var a = Point(r, playCenterX - playWidth / 2, nearDistance); var b = Point(r, playCenterX + playWidth / 2, farDistance);
            DrawLine(a, new Vector2(b.x, a.y), Color.green); DrawLine(new Vector2(b.x, a.y), b, Color.green);
            DrawLine(b, new Vector2(a.x, b.y), Color.green); DrawLine(new Vector2(a.x, b.y), a, Color.green);
            }
            var center = Point(r, 0, 0); GUI.Label(new Rect(center.x - 23, center.y - 8, 70, 20), "SENSOR");
            if (controller.Pose.HasSensorPosition)
            {
                var p = controller.Pose.SensorChest; var dot = Point(r, p.x, p.z);
                var oldColor = GUI.color; GUI.color = controller.Pose.BodyLive ? Color.yellow : Color.red;
                GUI.DrawTexture(new Rect(dot.x - 4, dot.y - 4, 8, 8), Texture2D.whiteTexture); GUI.color = oldColor;
                bool inside = savedBounds ? controller.Calibration.Inside(controller.Pose.RawChest) : p.z >= nearDistance && p.z <= farDistance && Mathf.Abs(p.x - playCenterX) <= playWidth / 2;
                GUI.Label(new Rect(8, 0, r.width - 8, 20), controller.Pose.BodyLive ? (inside ? "BODY / inside play area" : "BODY / outside play area") : "LAST BODY / tracking lost");
            }
            GUI.EndGroup();
        }
        static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var matrix = GUI.matrix; var previous = GUI.color; var delta = b - a;
            GUI.color = color; GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y, delta.magnitude, 1), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = previous;
        }
        void OnRenderObject()
        {
            if (!showOverlay || lines == null || controller?.Pose == null || (controller.Room != null && controller.Room.SetupMode) || Camera.current != Camera.main) return;
            lines.SetPass(0); GL.PushMatrix(); GL.MultMatrix(Matrix4x4.identity); GL.Begin(GL.LINES);
            // Front-facing stage ruler is in the same space as the avatar (0.5 stage units per square).
            GL.Color(new Color(0.2f, 0.45f, 0.55f, 0.25f));
            for (float x = -3; x <= 3; x += 0.5f) { GL.Vertex3(x, 0, 0.25f); GL.Vertex3(x, 3, 0.25f); }
            for (float y = 0; y <= 3; y += 0.5f) { GL.Vertex3(-3, y, 0.25f); GL.Vertex3(3, y, 0.25f); }
            var manager = KinectManager.Instance;
            if (manager != null && manager.IsInitialized())
            {
                var matrix = manager.GetSensorToWorldMatrix(0);
                var scale = manager.GetSensorSpaceScale(0);
                Vector3 sensor = controller.Pose.MapToStage(matrix.MultiplyPoint3x4(Vector3.zero), controller);
                GL.Color(new Color(0, 1, 1, 0.45f));
                var corners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    var raw = new Vector3((i % 2 == 0 ? -1 : 1) * farDistance * Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2), (i < 2 ? -1 : 1) * farDistance * Mathf.Tan(verticalFov * Mathf.Deg2Rad / 2), farDistance);
                    raw.x *= scale.x; raw.y *= scale.y;
                    corners[i] = controller.Pose.MapToStage(matrix.MultiplyPoint3x4(raw), controller);
                    GL.Vertex(sensor); GL.Vertex(corners[i]);
                }
                int[] order = { 0, 1, 3, 2, 0 };
                for (int i = 0; i < 4; i++) { GL.Vertex(corners[order[i]]); GL.Vertex(corners[order[i + 1]]); }
            }
            GL.End(); GL.PopMatrix();
        }
        void OnDestroy() { if (depthTexture != null) Destroy(depthTexture); if (lines != null) Destroy(lines); }
    }
}
