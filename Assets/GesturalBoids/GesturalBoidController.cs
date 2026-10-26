using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    [RequireComponent(typeof(BoidPhoneInput))]
    public class GesturalBoidController : MonoBehaviour
    {
        public enum Form { Swarm, Sword, Orb }
        public Form form;
        [Range(1, 1200)] public int boidCount = 20;
        public bool useBirdEncounter = true;
        public BirdRestorationEncounter Encounter { get; private set; }
        public float speed = 3.5f;
        public float formationStrength = 22f;
        public float damping = 7f;
        public float neighborRadius = 0.55f;
        public float boidSize = 0.045f;
        public bool useKinect = true;
        public int playerIndex;
        public bool leftHanded;
        public bool mirrorX = false;
        public Vector3 trackingOffset = new Vector3(0, 0, -2);
        public Vector3 trackingScale = Vector3.one;
        public bool enablePoseMoves = true;
        public float chargeSeconds = 1.2f;
        public float releaseDistance = 0.65f;
        public Material particleMaterial;
        [Range(0, 1)] public float swarmHandInfluence = 0.85f;
        public BoidBodyPose Pose { get; private set; }
        public BoidRoomSetup Room { get; private set; }
        public BoidRoomCalibration Calibration { get; private set; }
        public float LastFlockMilliseconds { get; private set; }
        public bool BodyTracked { get; private set; }
        public float Charge { get; private set; }
        public Vector3 Hand { get; private set; }
        public Vector3 OtherHand { get; private set; }
        public Vector3 Body { get; private set; }
        public BoidPhoneInput Phone { get; private set; }
        Vector3[] positions, velocities, nextVelocities, seeds;
        ParticleSystem.Particle[] particles;
        ParticleSystem system;
        Material ownedMaterial;
        float burstUntil, poseLockUntil;
        Vector3 burstOrigin, burstDirection;
        bool charged;
        Vector3 previousAnchor;
        bool anchorReady;
        Form previousForm;
        Vector2 panelScroll;
        bool showPanel = true, showDetails;
        string phonePortText;

        void Awake()
        {
            Application.runInBackground = true;
            Phone = GetComponent<BoidPhoneInput>();
            Calibration = GetComponent<BoidRoomCalibration>();
            if (Calibration == null) Calibration = gameObject.AddComponent<BoidRoomCalibration>();
            Pose = GetComponent<BoidBodyPose>();
            if (Pose == null) Pose = gameObject.AddComponent<BoidBodyPose>();
            if (GetComponent<BoidAvatar>() == null) gameObject.AddComponent<BoidAvatar>();
            if (GetComponent<BoidSpaceOverlay>() == null) gameObject.AddComponent<BoidSpaceOverlay>();
            Room = GetComponent<BoidRoomSetup>();
            if (Room == null) Room = gameObject.AddComponent<BoidRoomSetup>();
            if (useBirdEncounter)
            {
                boidCount = 20;
                var oldParticles = GetComponent<ParticleSystem>();
                if (oldParticles != null) oldParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Encounter = GetComponent<BirdRestorationEncounter>();
                if (Encounter == null) Encounter = gameObject.AddComponent<BirdRestorationEncounter>();
                Encounter.Initialize(this);
                return;
            }
            boidCount = Mathf.Clamp(boidCount, 64, 1200);
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.maxParticles = boidCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 10000;
            main.startSpeed = 0;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleMaterial == null)
            {
                ownedMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
                renderer.sharedMaterial = ownedMaterial;
            }
            else renderer.sharedMaterial = particleMaterial;
            positions = new Vector3[boidCount]; velocities = new Vector3[boidCount];
            nextVelocities = new Vector3[boidCount]; seeds = new Vector3[boidCount];
            particles = new ParticleSystem.Particle[boidCount];
            var random = new System.Random(371);
            for (int i = 0; i < positions.Length; i++)
            {
                seeds[i] = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
                positions[i] = new Vector3(0, 1.4f, 0) + seeds[i];
                velocities[i] = Vector3.Cross(seeds[i], Vector3.up);
            }
            system.Play();
        }
        void OnEnable() { GetComponent<BoidPhoneInput>().Snapped += CycleForm; }
        void OnDisable() { if (Phone != null) Phone.Snapped -= CycleForm; }
        void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
        public void CycleForm()
        {
            if (Encounter != null)
            {
                if ((Room == null || !Room.SetupMode) && Encounter.CanCallSword) Encounter.EquipSword();
                return;
            }
            SetForm((Form)(((int)form + 1) % 3));
        }
        public void EnterPresentationMode()
        {
            showPanel = false;
            var overlay = GetComponent<BoidSpaceOverlay>();
            if (overlay != null) overlay.showOverlay = false;
        }
        public void SetForm(Form value)
        {
            if (Encounter != null)
            {
                if (Room != null && Room.SetupMode) return;
                if (value == Form.Sword) Encounter.EquipSword();
                return;
            }
            form = value; Charge = 0; charged = false;
        }

        void Update()
        {
            ReadBody();
            if (Input.GetKeyDown(KeyCode.Space)) CycleForm();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetForm(Form.Swarm);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetForm(Form.Sword);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetForm(Form.Orb);
            if (Input.GetKeyDown(KeyCode.C)) Phone.Calibrate();
            if (Input.GetKeyDown(KeyCode.H)) showPanel = !showPanel;
            if (Input.GetKeyDown(KeyCode.R)) Pose.ScheduleRecenter();
            if (Input.GetKeyDown(KeyCode.M)) { mirrorX = !mirrorX; Pose.ScheduleRecenter(); }
            if (Input.GetKeyDown(KeyCode.P)) Pose.previewOnly = !Pose.previewOnly;
            if (Room != null && Room.SetupMode) { if (Encounter != null) Encounter.Suspend(); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            if (Encounter != null)
            {
                long encounterStart = System.Diagnostics.Stopwatch.GetTimestamp();
                Encounter.Tick(dt);
                LastFlockMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - encounterStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                return;
            }
            bool closeHands = Vector3.Distance(Hand, OtherHand) < 0.35f;
            bool poseValid = BodyTracked && Pose.WeaponHandLive && Pose.OtherHandLive;
            bool charging = Input.GetKey(KeyCode.Q) || (enablePoseMoves && poseValid && closeHands && form == Form.Orb);
            if (Time.time >= poseLockUntil)
            {
                if (charging) { Charge = Mathf.Clamp01(Charge + dt / Mathf.Max(0.1f, chargeSeconds)); charged |= Charge >= 1; }
                else if (charged && ((poseValid && Vector3.Distance(Hand, OtherHand) > releaseDistance) || Input.GetKeyUp(KeyCode.Q))) Release();
                else if (!charged) Charge = Mathf.MoveTowards(Charge, 0, dt);
            }
            if (Input.GetKeyDown(KeyCode.F)) Release();
            long flockStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Simulate(dt);
            LastFlockMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - flockStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        void ReadBody()
        {
            Pose.Sample(this);
            BodyTracked = Pose.BodyLive;
            Body = Pose.Joint(KinectInterop.JointType.SpineChest);
            Hand = Pose.Joint(leftHanded ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight);
            OtherHand = Pose.Joint(leftHanded ? KinectInterop.JointType.HandRight : KinectInterop.JointType.HandLeft);
        }
        public void Release()
        {
            burstOrigin = (Hand + OtherHand) * 0.5f;
            burstDirection = Phone.HasOrientation ? Phone.Orientation * Vector3.forward : Vector3.forward;
            burstUntil = Time.time + 1.25f; poseLockUntil = Time.time + 1.7f;
            Charge = 0; charged = false;
        }
        void Simulate(float dt)
        {
            float t = Time.time;
            var rotation = Phone.HasOrientation ? Phone.Orientation : Quaternion.identity;
            bool burst = t < burstUntil;
            var anchor = form == Form.Swarm ? Vector3.Lerp(Body, Hand, swarmHandInfluence) : Hand;
            // Carry the formation with its anchor, leaving spring forces to shape it rather than lag behind the hand.
            if (anchorReady && previousForm == form && !burst)
            {
                var displacement = anchor - previousAnchor;
                for (int i = 0; i < positions.Length; i++) positions[i] += displacement;
            }
            previousAnchor = anchor; previousForm = form; anchorReady = true;
            float radiusSquared = neighborRadius * neighborRadius;
            // Double buffer so flock steering is independent of iteration order.
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 separation = Vector3.zero, alignment = Vector3.zero, center = Vector3.zero;
                int neighbors = 0;
                if (form == Form.Swarm && !burst)
                {
                    for (int j = 0; j < positions.Length; j++)
                    {
                        if (i == j) continue;
                        var delta = positions[i] - positions[j];
                        float d = delta.sqrMagnitude;
                        if (d > radiusSquared) continue;
                        separation += delta / Mathf.Max(d, 0.003f);
                        alignment += velocities[j]; center += positions[j]; neighbors++;
                    }
                }
                var seed = seeds[i];
                Vector3 target;
                if (burst)
                {
                    float travel = (1.25f - (burstUntil - t)) * 7;
                    target = burstOrigin + burstDirection * (travel + seed.z) + new Vector3(seed.x, seed.y, 0) * 0.22f;
                }
                else if (form == Form.Sword)
                {
                    // Allocate a broad crossguard and handle as well as a tapered blade.
                    float u = (i + 0.5f) / positions.Length;
                    Vector3 local;
                    if (u < 0.15f) local = new Vector3(seed.x * 0.42f, 0.08f + seed.y * 0.035f, seed.z * 0.045f);
                    else if (u < 0.25f) local = new Vector3(seed.x * 0.045f, -0.26f + (u - 0.15f) * 3, seed.z * 0.045f);
                    else { float h = (u - 0.25f) / 0.75f; local = new Vector3(seed.x * 0.13f * (1 - h), 0.12f + h * 1.65f, seed.z * 0.025f); }
                    target = Hand + rotation * local;
                }
                else if (form == Form.Orb)
                {
                    var spin = rotation * Quaternion.Euler(t * 65, t * 150, t * 35);
                    target = Hand + Vector3.up * 0.28f + spin * seed.normalized * (0.16f + Mathf.Abs(seed.z) * 0.16f) * (1 + Charge * 0.6f);
                }
                else
                {
                    var orbit = rotation * Quaternion.Euler(0, t * 35, Mathf.Sin(t * 0.4f) * 20);
                    target = anchor + orbit * seed * 1.2f;
                }
                Vector3 force = (target - positions[i]) * (form == Form.Swarm && !burst ? 3f : formationStrength);
                if (neighbors > 0) force += separation * 0.18f + (alignment / neighbors - velocities[i]) * 0.6f + (center / neighbors - positions[i]) * 0.4f;
                force += Vector3.Cross(seed, Vector3.up) * Mathf.Sin(t * 2 + i) * 0.35f;
                nextVelocities[i] = Vector3.ClampMagnitude((velocities[i] + force * dt) / (1 + damping * dt * (form == Form.Swarm && !burst ? 0.2f : 1)), burst ? 14 : speed);
            }
            Color tint = burst ? new Color(0.7f, 0.9f, 1) : form == Form.Sword ? new Color(1, 0.65f, 0.18f) : form == Form.Orb ? new Color(0.3f, 0.65f, 1) : new Color(0.25f, 1, 0.8f);
            for (int i = 0; i < positions.Length; i++)
            {
                velocities[i] = nextVelocities[i]; positions[i] += velocities[i] * dt;
                particles[i].position = positions[i]; particles[i].startSize = boidSize * (1 + Charge * 0.4f);
                particles[i].startColor = tint; particles[i].remainingLifetime = 1000; particles[i].startLifetime = 1000;
            }
            system.SetParticles(particles, positions.Length);
        }
        void OnGUI()
        {
            if (Room != null && Room.SetupMode) return;
            if (!showPanel)
            {
                if (GUI.Button(new Rect(8, 8, 180, 26), "Show controls / H")) showPanel = true;
                return;
            }
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginArea(new Rect(8, 8, Mathf.Min(320, Screen.width * 0.42f), Screen.height - 16), GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("GESTURAL BOIDS / " + form.ToString().ToUpperInvariant());
            if (Encounter != null) Encounter.DrawHUD();
            GUILayout.Label(Pose.Status);
            if (Pose.CenterStatus.Length > 0) GUILayout.Label(Pose.CenterStatus);
            GUILayout.Label(Phone.Status);
            GUILayout.Label("Phone packets: " + Phone.PacketCount + " / " + Phone.RotationSource);
            GUILayout.Label("Receiver: " + (Phone.Listening ? (Phone.LoopbackPassed ? "local self-check passed" : "listening / self-check pending") : "NOT LISTENING"));
            GUILayout.Label("ZigSim destination IP (use your LAN adapter):\n" + Phone.LocalAddresses);
            GUILayout.BeginHorizontal(); GUILayout.Label("UDP port", GUILayout.Width(64));
            phonePortText = GUILayout.TextField(phonePortText ?? Phone.port.ToString(), GUILayout.MinWidth(55));
            if (GUILayout.Button("Apply", GUILayout.Width(48)) && int.TryParse(phonePortText, out int selectedPort)) Phone.Reconnect(selectedPort);
            GUILayout.EndHorizontal();
            if (Phone.PacketCount == 0) GUILayout.Label("ZigSim: send to this PC, UDP, matching port, JSON or OSC. Enable sensors, then START streaming. Check phone local-network permission / same Wi-Fi / Windows private-network access.");
            GUILayout.Label("Last sender: " + Phone.LastSender);
            if (GUILayout.Button("Restart receiver / refresh addresses")) Phone.Reconnect(Phone.port);
            if (GUILayout.Button("Center in 8 seconds / step back")) Pose.ScheduleRecenter();
            if (GUILayout.Button("Calibrate phone / screen-up on table (C)")) Phone.Calibrate();
            if (GUILayout.Button("Mirror: " + (mirrorX ? "on" : "off"))) { mirrorX = !mirrorX; Pose.ScheduleRecenter(); }
            GUILayout.Label(Encounter != null ? "Space / 2: call sword from idle\nR: center / M: mirror / P: mouse preview\nTab: room / O: space overlay / H: controls" : "Space: form / 1-3: select / Q: charge / F: fire\nR: center / M: mirror / P: mouse\nO: space overlay / H: hide controls");
            showDetails = GUILayout.Toggle(showDetails, "Detailed sensor readings");
            if (showDetails)
            {
                GUILayout.Label("Hand sensor " + Pose.RawHand.ToString("F2") + " / stage " + Hand.ToString("F2"));
                GUILayout.Label("Format " + Phone.LastPacketFormat + " / address " + Phone.LastAddress);
                GUILayout.Label("Accel " + Phone.Acceleration.ToString("F2") + " / gyro " + Phone.Gyro.ToString("F2") + " / quaternions " + Phone.QuaternionPackets);
                GUILayout.Label("Packet preview: " + Phone.PacketPreview);
                GUILayout.Label("Charge " + Mathf.RoundToInt(Charge * 100) + "%");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
