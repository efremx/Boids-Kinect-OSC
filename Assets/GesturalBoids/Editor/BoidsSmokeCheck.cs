using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using extOSC;
using extOSC.Core;
using com.rfilkov.kinect;

namespace GesturalBoids.Editor
{
    // Batch entry point: omit -quit; this check exits after exercising actual Play mode.
    [InitializeOnLoad]
    public static class BoidsSmokeCheck
    {
        const string Key = "GesturalBoids.Smoke";
        const int TestPort = 18777;
        static double started;
        static double wallStarted;
        static int stage;
        static bool failed;
        static Vector3 heldHand;
        static BoidsSmokeCheck()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged += StateChanged;
            Application.logMessageReceived += Log;
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/GesturalBoids/GesturalBoids.unity");
            foreach (var manager in UnityEngine.Object.FindObjectsByType<KinectManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) manager.gameObject.SetActive(false);
            UnityEngine.Object.FindFirstObjectByType<GesturalBoidController>().useKinect = false;
            UnityEngine.Object.FindFirstObjectByType<GesturalBoidController>().useBirdEncounter = false; // Legacy receiver/particle smoke check; not a combat test.
            UnityEngine.Object.FindFirstObjectByType<BoidPhoneInput>().port = TestPort;
            SessionState.SetBool(Key, true);
            EditorApplication.playModeStateChanged -= StateChanged;
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.EnterPlaymode();
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            var room = UnityEngine.Object.FindFirstObjectByType<BoidRoomSetup>();
            if (room != null) room.SetupMode = false;
            started = Time.time;
            wallStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        static void Send(string sensorData)
        {
            using (var sender = new UdpClient())
            {
                var bytes = Encoding.UTF8.GetBytes("{\"sensordata\":" + sensorData + "}");
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, TestPort));
            }
        }
        static void SendOSC(string address, params float[] values)
        {
            var message = new OSCMessage(address);
            foreach (float value in values) message.AddValue(OSCValue.Float(value));
            var bundle = new OSCBundle(); bundle.Packets.Add(message);
            using (var sender = new UdpClient())
            {
                var bytes = OSCConverter.Pack(bundle);
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, TestPort));
            }
        }
        static void Tick()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<GesturalBoidController>();
            if (controller == null) { Finish(false, "Controller missing"); return; }
            double elapsed = Time.time - started;
            try
            {
                if (stage == 0 && elapsed > 1)
                {
                    Capture("Swarm");
                    Send("{\"accel\":{\"x\":0,\"y\":0,\"z\":0}}");
                    Send("{\"accel\":{\"x\":3,\"y\":0,\"z\":0}}");
                    Send("{\"accel\":{\"x\":3,\"y\":0,\"z\":0}}");
                    stage++; started = Time.time; return;
                }
                if (stage == 1 && elapsed > 1)
                {
                    if (controller.form != GesturalBoidController.Form.Sword) throw new Exception("JSON snap did not select Sword exactly once");
                    // Above-threshold packets after cooldown must still require a low sample to rearm.
                    Send("{\"accel\":{\"x\":3,\"y\":0,\"z\":0}}");
                    stage++; started = Time.time; return;
                }
                if (stage == 2 && elapsed > 1)
                {
                    if (controller.form != GesturalBoidController.Form.Sword) throw new Exception("Snap rearmed without settling");
                    Capture("Sword");
                    Send("{\"accel\":{\"x\":0,\"y\":0,\"z\":0}}");
                    Send("{\"accel\":{\"x\":3,\"y\":0,\"z\":0},\"quaternion\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}");
                    stage++; started = Time.time; return;
                }
                if (stage == 3 && elapsed > 1)
                {
                    if (controller.form != GesturalBoidController.Form.Orb) throw new Exception("Second snap did not select Orb");
                    Capture("Orb");
                    controller.Release(); stage++; started = Time.time; return;
                }
                if (stage == 4 && elapsed > 2)
                {
                    var particles = new ParticleSystem.Particle[controller.boidCount];
                    int count = controller.GetComponent<ParticleSystem>().GetParticles(particles);
                    if (count != controller.boidCount) throw new Exception("Unexpected live particle count: " + count);
                    foreach (var p in particles)
                        if (float.IsNaN(p.position.sqrMagnitude) || float.IsInfinity(p.position.sqrMagnitude)) throw new Exception("Non-finite particle position");
                    SendOSC("/ZIGSIM/test-phone/quaternion", 0, 0, 0.7071068f, 0.7071068f);
                    stage++; started = Time.time; return;
                }
                if (stage == 5 && elapsed > 0.3f)
                {
                    if (!controller.Phone.HasOrientation || controller.Phone.LastPacketFormat != "OSC" || Quaternion.Angle(Quaternion.identity, controller.Phone.Orientation) < 80) throw new Exception("OSC bundle quaternion did not rotate weapon");
                    controller.Phone.Calibrate();
                    if (Quaternion.Angle(Quaternion.identity, controller.Phone.Orientation) > 0.1f) throw new Exception("Phone calibration failed");
                    stage++; started = Time.time; return;
                }
                if (stage == 6 && elapsed > 1.1f)
                {
                    SendOSC("/zigsim/test-phone/gyro", 0, 0, 2);
                    stage++; started = Time.time; return;
                }
                if (stage == 7 && elapsed > 0.4f)
                {
                    if (!controller.Phone.HasOrientation || !controller.Phone.RotationSource.StartsWith("gyro") || Quaternion.Angle(Quaternion.identity, controller.Phone.Orientation) < 10) throw new Exception("Gyro-only OSC did not rotate weapon");
                    TestPartialPose(controller);
                    stage++; started = Time.time; return;
                }
                if (stage == 8 && elapsed > 1)
                {
                    if (controller.BodyTracked || Vector3.Distance(controller.Hand, heldHand) > 0.01f) throw new Exception("Lost body fell back to preview or did not expire");
                    controller.Phone.enabled = false;
                    using (var probe = new UdpClient(TestPort)) { }
                    Finish(!failed, "forms/burst; JSON snaps; OSC bundles/quaternion/calibration/gyro; partial joints; 1:1 mapping; lost-pose hold; avatar; socket shutdown");
                }
                if (EditorApplication.timeSinceStartup - wallStarted > 60) Finish(false, "Timed out");
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void TestPartialPose(GesturalBoidController controller)
        {
            controller.Pose.smoothing = 10000;
            var points = new Vector3[32]; var tracked = new bool[32];
            int chest = (int)KinectInterop.JointType.SpineChest;
            int hand = (int)(controller.leftHanded ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight);
            points[chest] = new Vector3(2, 1.2f, 3); points[hand] = points[chest] + Vector3.right * 0.5f;
            tracked[chest] = tracked[hand] = true; // Free hand is occluded: must not drop the body.
            controller.Pose.ApplyFrame(points, tracked, controller); controller.Pose.Sample(controller);
            if (!controller.Pose.BodyLive || !controller.Pose.WeaponHandLive || controller.Pose.OtherHandLive) throw new Exception("Independent joint tracking failed");
            if (Vector3.Distance(controller.Pose.Joint(KinectInterop.JointType.SpineChest), controller.Pose.StageChest) > 0.01f) throw new Exception("Chest recentering failed");
            var before = controller.Pose.Joints[hand];
            points[hand].x += 0.5f;
            controller.Pose.ApplyFrame(points, tracked, controller); controller.Pose.Sample(controller);
            heldHand = controller.Pose.Joints[hand];
            if (Mathf.Abs(Vector3.Distance(before, heldHand) - 0.5f) > 0.01f) throw new Exception("Hand mapping is not 1:1");
            if (controller.GetComponent<BoidAvatar>() == null) throw new Exception("Avatar missing");
        }
        static void Finish(bool success, string detail)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Debug.Log("BOIDS_SMOKE_" + (success ? "PASS " : "FAIL ") + detail);
            EditorApplication.Exit(success ? 0 : 1);
        }
        static void Capture(string form)
        {
            var target = new RenderTexture(960, 540, 24);
            var camera = Camera.main;
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes("Logs/Boids" + form + ".png", texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
