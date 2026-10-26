using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    public class BirdRestorationEncounter : MonoBehaviour
    {
        const int Count = 20, SwordCount = 5;
        enum Phase { Roost, Assembly, Fight, Meditation, Integrating, Victory }
        enum Motion { Orbit, Climb, Warning, Dive, Recover, Returning, Falling, Grounded, Rising, Absorbing }
        class Bird
        {
            public Transform root, left, right;
            public Renderer[] renderers;
            public Vector3 previous, start;
            public bool restored;
            public Motion motion;
            public float progress;
            public Vector3 ground;
            public Vector3 flightVelocity;
            public float size;
        }
        public float blockRadius = 0.22f;
        public float assemblySeconds = 1.1f;
        public float maximumSwingBonus = 0.14f;
        public float meditationSeconds = 18;
        public bool allowStandingMeditation;
        public bool InProgress => phase != Phase.Roost && phase != Phase.Victory;
        public bool SwordEquipped => phase == Phase.Assembly || phase == Phase.Fight;
        public bool CanCallSword => phase == Phase.Roost;
        public int Restored { get; private set; }
        public int HitsReceived { get; private set; }
        public string Message { get; private set; } = "Twenty birds. One flock. Call five into a blade.";
        GesturalBoidController owner;
        readonly Bird[] birds = new Bird[Count];
        readonly List<Mesh> meshes = new List<Mesh>();
        MaterialPropertyBlock properties;
        Material ink, lineMaterial;
        Material roomMaterial;
        TrailRenderer swingTrail;
        Transform sword, content;
        LineRenderer warning;
        Phase phase;
        float clock, visualClock, phaseStart, nextAttack, flash, startAt = -1;
        bool hitFlash, swordHistory, suspended;
        float flockPulseAt = -100;
        int attacker = -1;
        Vector3 attackStart, attackTarget, previousTip;
        // A short, gently recurved profile: the visual blade and hit path use these same stations.
        static readonly Vector3[] BladeCurve = {
            new Vector3(0,0.11f,0),new Vector3(-0.015f,0.27f,0),new Vector3(-0.025f,0.45f,0),new Vector3(-0.005f,0.63f,0),
            new Vector3(0.045f,0.81f,0),new Vector3(0.12f,0.98f,0),new Vector3(0.21f,1.12f,0),new Vector3(0.29f,1.20f,0)
        };
        static readonly float[] BladeWidths = {0.065f,0.07f,0.075f,0.085f,0.10f,0.10f,0.065f,0};
        readonly Vector3[] previousCurve = new Vector3[BladeCurve.Length];
        float swingSpeed, standingHeight = 1.35f, meditation, stillFor, motionSpeed, whiteRoom;
        Vector3 previousBody, previousHand, previousOther;
        int absorbed;
        bool motionHistory;
        float floorLevel, meditationPreviewAt = -1;
        GUIStyle storyStyle;
        string meditationCue = "Sit or kneel, lower both hands, and become still.";
        Quaternion SwordRotation => owner.Phone.HasOrientation ? owner.Phone.Orientation : ArmRotation();
        Vector3 BladeTip => sword.TransformPoint(BladeCurve[BladeCurve.Length-1]);
        Vector3 FusionPoint(int index) => sword.TransformPoint(BladeCurve[Mathf.RoundToInt(index*(BladeCurve.Length-1f)/(SwordCount-1))]);
        bool RequiredTracking => !owner.Pose.CenterPending && (owner.Pose.previewOnly || !owner.useKinect || (owner.BodyTracked && (!SwordEquipped || (owner.Pose.WeaponHandLive && owner.Pose.FrameAge<0.2f))));

        public void Initialize(GesturalBoidController controller)
        {
            owner = controller;
            properties = new MaterialPropertyBlock();
            content = new GameObject("Twenty birds / restoration encounter").transform;
            content.SetParent(transform, false);
            ink = new Material(Shader.Find("GesturalBoids/BirdInk"));
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            Mesh body = MeshFrom(new Vector3[] { new Vector3(0,0.05f,0.18f), new Vector3(-0.05f,0,0), new Vector3(0,0,-0.16f), new Vector3(0.05f,0,0), new Vector3(0,-0.04f,0) }, new int[] {0,1,2,0,2,3,0,4,1,1,4,2,2,4,3,3,4,0});
            Mesh left = MeshFrom(new Vector3[] { Vector3.zero, new Vector3(-0.29f,0,-0.05f), new Vector3(-0.06f,0,-0.15f) }, new int[] {0,1,2});
            Mesh right = MeshFrom(new Vector3[] { Vector3.zero, new Vector3(0.06f,0,-0.15f), new Vector3(0.29f,0,-0.05f) }, new int[] {0,1,2});
            for (int i = 0; i < Count; i++)
            {
                var root = new GameObject("Bird " + (i + 1) + (i < SwordCount ? " / loyal sword bird" : " / flock")).transform;
                root.SetParent(content, false); root.localScale = Vector3.one * (0.65f + (i % 4) * 0.06f) * 1.25f;
                var bodyPart = Part("Body", root, body, Color.gray);
                var l = Part("Left wing", root, left, Color.gray); var r = Part("Right wing", root, right, Color.gray);
                birds[i] = new Bird { root = root, left = l, right = r, renderers = new[] { bodyPart.GetComponent<Renderer>(), l.GetComponent<Renderer>(), r.GetComponent<Renderer>() } };
                birds[i].size = root.localScale.x;
            }
            sword = new GameObject("Rigid sword / five loyal birds").transform; sword.SetParent(content, false);
            Part("Dark curved spine",sword,CurvedBlade(1.12f,0.027f),new Color(0.025f,0.025f,0.025f));
            Part("White curved cutting face",sword,CurvedBlade(1,0.03f),Color.white);
            Part("Small dark guard", sword, Extrude(new[] {new Vector2(-0.16f,0.025f),new Vector2(0.15f,0.025f),new Vector2(0.13f,0.09f),new Vector2(-0.14f,0.09f)},0.045f),new Color(0.025f,0.025f,0.025f));
            Part("Dark shaped grip",sword,Extrude(new[] {new Vector2(-0.045f,-0.22f),new Vector2(0.045f,-0.22f),new Vector2(0.035f,0.025f),new Vector2(-0.035f,0.025f)},0.04f),new Color(0.12f,0.12f,0.12f));
            Part("Flared pommel",sword,Extrude(new[] {new Vector2(-0.07f,-0.27f),new Vector2(0.07f,-0.27f),new Vector2(0.06f,-0.22f),new Vector2(-0.06f,-0.22f)},0.045f),new Color(0.06f,0.06f,0.06f));
            for(int side=-1;side<=1;side+=2)
            {
                var fuller=new GameObject("Inset blade line",typeof(LineRenderer)).GetComponent<LineRenderer>();fuller.transform.SetParent(sword,false);
                fuller.sharedMaterial=lineMaterial;fuller.useWorldSpace=false;fuller.positionCount=BladeCurve.Length-1;
                fuller.startWidth=fuller.endWidth=0.009f;fuller.startColor=fuller.endColor=new Color(0.12f,0.12f,0.12f);
                for(int i=0;i<BladeCurve.Length-1;i++)fuller.SetPosition(i,BladeCurve[i]+new Vector3(-BladeWidths[i]*0.2f,0,side*0.031f));
            }
            swingTrail = new GameObject("Swing trace",typeof(TrailRenderer)).GetComponent<TrailRenderer>();
            swingTrail.transform.SetParent(sword,false); swingTrail.transform.localPosition = BladeCurve[BladeCurve.Length-1];
            swingTrail.sharedMaterial = lineMaterial; swingTrail.time = 0.14f; swingTrail.startWidth = 0.07f; swingTrail.endWidth = 0;
            swingTrail.startColor = new Color(1,1,1,0.6f); swingTrail.endColor = new Color(1,1,1,0); swingTrail.emitting = false;
            roomMaterial = new Material(Shader.Find("GesturalBoids/ShadowRoom"));
            var backdrop = new GameObject("Shadow room / inner light",typeof(MeshFilter),typeof(MeshRenderer));
            backdrop.transform.SetParent(content,false);
            backdrop.GetComponent<MeshFilter>().sharedMesh = MeshFrom(new[] {new Vector3(-30,-20,8),new Vector3(30,-20,8),new Vector3(30,30,8),new Vector3(-30,30,8)},new[] {0,1,2,0,2,3});
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = roomMaterial;
            warning = new GameObject("Incoming dive / warning line", typeof(LineRenderer)).GetComponent<LineRenderer>();
            warning.transform.SetParent(content, false); warning.sharedMaterial = lineMaterial;
            warning.positionCount = 2; warning.startWidth = 0.016f; warning.endWidth = 0.006f;
            warning.startColor = warning.endColor = new Color(0.9f,0.02f,0.025f,0.8f);
            warning.enabled = false;
            if (Camera.main != null) Camera.main.backgroundColor = new Color(0.09f,0.09f,0.105f);
            ResetEncounter();
        }
        Transform Part(string name, Transform parent, Mesh mesh, Color tint)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = ink;
            Tint(renderer, tint); return part.transform;
        }
        void Tint(Renderer renderer, Color tint) { properties.SetColor("_Color", tint); renderer.SetPropertyBlock(properties); }
        void TintBird(Bird bird, Color tint) { foreach (var renderer in bird.renderers) Tint(renderer, tint); }
        public void ResetEncounter()
        {
            phase = Phase.Roost; Restored = HitsReceived = 0; attacker = -1; startAt = -1; clock = phaseStart = 0;
            swordHistory = suspended = false; flash = 0; sword.gameObject.SetActive(false); warning.enabled = false;
            flockPulseAt=-100;
            
            meditation = stillFor = whiteRoom = swingSpeed = 0; absorbed = 0; motionHistory = false; standingHeight = Mathf.Max(0.5f,owner.Body.y);
            meditationPreviewAt = -1; floorLevel = owner.Calibration.Ready ? 0 : owner.Body.y - 1.35f;
            if(swingTrail!=null){swingTrail.Clear();swingTrail.emitting=false;}
            var avatar=owner.GetComponent<BoidAvatar>();if(avatar!=null)avatar.Integration=0;
            for (int i = 0; i < Count; i++) { birds[i].restored = false; birds[i].motion = Motion.Orbit; birds[i].root.localScale=Vector3.one*birds[i].size;birds[i].flightVelocity=Vector3.zero; birds[i].root.gameObject.SetActive(true); birds[i].root.position = Orbit(i); }
            owner.form = GesturalBoidController.Form.Swarm;
            Message = "Twenty birds circle your chest. Call five into a blade.";
        }
        public void ScheduleStart() { if (!InProgress) startAt = Time.unscaledTime + 8; }
        public void Suspend()
        { suspended = true; swordHistory = false;  if (swingTrail != null) { swingTrail.emitting=false; swingTrail.Clear(); } if (warning != null) warning.enabled = false; }
        public void EquipSword()
        {
            if (InProgress) return;
            if (phase == Phase.Victory) ResetEncounter();
            startAt = -1; phase = Phase.Assembly; phaseStart = clock;
            owner.EnterPresentationMode();
            floorLevel = owner.Calibration.Ready ? 0 : owner.Body.y - 1.35f;
            sword.position = owner.Hand; sword.rotation = SwordRotation; sword.localScale = Vector3.one * 0.01f; sword.gameObject.SetActive(true);
            for (int i = 0; i < SwordCount; i++) birds[i].start = birds[i].root.position;
            owner.form = GesturalBoidController.Form.Sword;
            Message = "Five birds answer. The others are watching.";
        }
        Quaternion ArmRotation()
        {
            var elbow = owner.Pose.Joint(owner.leftHanded ? KinectInterop.JointType.ElbowLeft : KinectInterop.JointType.ElbowRight);
            var direction = owner.Hand - elbow;
            return direction.sqrMagnitude > 0.01f ? Quaternion.FromToRotation(Vector3.up, direction.normalized) : Quaternion.identity;
        }
        Vector3 Orbit(int i)
        {
            float angle = visualClock * (0.38f + (i % 5) * 0.045f) + i * 2.39996f;
            float radius = 0.95f + (i % 4) * 0.17f + Mathf.Sin(visualClock*0.45f+i)*0.18f;
            float level = 0.25f+(i%5-2)*0.30f;
            if(phase==Phase.Fight)
            {
                float t=clock-flockPulseAt;
                float pulse=t<0?0:t<0.14f?Mathf.SmoothStep(0,1,t/0.14f):1-Mathf.SmoothStep(0,1,(t-0.14f)/1.05f);
                radius+=pulse*0.38f;
                level+=pulse*0.08f;
            }
            if(phase==Phase.Meditation || phase==Phase.Integrating)level=0.55f+(i%4)*0.16f;
            return owner.Body + new Vector3(Mathf.Cos(angle)*radius,level+Mathf.Sin(visualClock*0.7f+i*1.7f)*0.18f,Mathf.Sin(angle)*radius*0.7f);
        }
        public void Tick(float dt)
        {
            if (owner == null) return;
            visualClock += dt; flash = Mathf.Max(0, flash - dt);
            if(meditationPreviewAt>=0&&Time.unscaledTime>=meditationPreviewAt)
            {
                float baseline=standingHeight;ResetEncounter();standingHeight=Mathf.Max(standingHeight,baseline);phase=Phase.Fight;floorLevel=owner.Calibration.Ready?0:owner.Body.y-1.35f;
                for(int i=SwordCount;i<Count;i++)DownBird(i,birds[i]);BeginMeditation();owner.EnterPresentationMode();
            }
            if (startAt >= 0 && Time.unscaledTime >= startAt) EquipSword();
            bool live = RequiredTracking;
            if (!live && InProgress) { Suspend(); return; }
            if (suspended)
            {
                if (attacker >= 0 && (birds[attacker].motion == Motion.Dive || birds[attacker].motion == Motion.Warning))
                { var bird = birds[attacker]; bird.motion = Motion.Climb; bird.progress = 0; bird.start = bird.root.position; }
                suspended = false;
            }
            clock += dt;
            if((phase==Phase.Roost||phase==Phase.Fight)&&owner.BodyTracked)standingHeight=Mathf.Max(standingHeight,owner.Body.y);
            sword.position=owner.Hand; sword.rotation=SwordRotation;
            float tipSpeed=swordHistory?Vector3.Distance(previousTip,BladeTip)/Mathf.Max(dt,0.001f):0;
            swingSpeed=Mathf.Lerp(swingSpeed,Mathf.Min(8,tipSpeed),1-Mathf.Exp(-12*dt));
            swingTrail.emitting=phase==Phase.Fight&&swingSpeed>1.2f;
            warning.enabled = false;
            UpdateRitual(dt);
            float assembled = Mathf.Clamp01((clock - phaseStart) / Mathf.Max(0.1f, assemblySeconds));
            if (phase == Phase.Assembly)
            {
                sword.localScale = Vector3.one * Mathf.SmoothStep(0.01f, 1, assembled);
                if (assembled >= 1)
                {
                    phase = Phase.Fight; phaseStart = clock; nextAttack = clock + 3.2f; sword.localScale = Vector3.one;
                    for (int i = 0; i < SwordCount; i++) birds[i].root.gameObject.SetActive(false);
                    Message = "A bird reddens, climbs, then dives. Strike through its path.";
                }
            }
            for (int i = 0; i < Count; i++)
            {
                var bird = birds[i]; if (!bird.root.gameObject.activeSelf) continue;
                bird.previous = bird.root.position;
                Color tint = bird.restored ? Color.white : new Color(0.62f,0.64f,0.65f);
                if (i < SwordCount && phase == Phase.Assembly)
                {
                    var target = FusionPoint(i);
                    bird.root.position = Vector3.Lerp(bird.start, target, Mathf.SmoothStep(0,1,assembled));
                    tint = Color.white;
                }
                else if (bird.motion == Motion.Falling || bird.motion == Motion.Grounded || bird.motion == Motion.Rising || bird.motion == Motion.Absorbing)
                    UpdateHealingBird(i,bird,dt);
                else if (bird.motion == Motion.Returning || (bird.motion == Motion.Recover && i != attacker))
                {
                    bird.progress += dt / 1.2f;
                    bird.root.position = Vector3.Lerp(bird.start, Orbit(i), Mathf.SmoothStep(0,1,bird.progress));
                    if (bird.progress >= 1) bird.motion = Motion.Orbit;
                }
                else if (i == attacker) UpdateAttacker(i, bird, dt);
                else bird.root.position = Vector3.SmoothDamp(bird.root.position,Orbit(i),ref bird.flightVelocity,0.32f,4,dt);
                if (i >= SwordCount && !bird.restored && phase == Phase.Fight)
                {
                    float corruption = Mathf.Clamp01((clock - phaseStart - (i - SwordCount) * 1.3f) / 3);
                    tint = Color.Lerp(tint, new Color(0.95f,0.015f,0.025f), corruption);
                }
                if (bird.restored) tint = bird.motion==Motion.Falling||bird.motion==Motion.Grounded ? new Color(0.3f,0.035f,0.045f) : Color.white;
                TintBird(bird, tint);
                Vector3 velocity = bird.root.position - bird.previous;
                if(bird.motion==Motion.Grounded)bird.root.rotation=Quaternion.Euler(0, i*37, 12);
                else if (velocity.sqrMagnitude > 0.000001f) bird.root.rotation = Quaternion.Slerp(bird.root.rotation,Quaternion.LookRotation(velocity, Mathf.Abs(Vector3.Dot(velocity.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up),1-Mathf.Exp(-8*dt));
                float wing = Mathf.Sin(visualClock * 11 + i * 2.3f) * 38;
                if(bird.motion==Motion.Grounded)wing=-65;
                bird.left.localRotation = Quaternion.Euler(0,0,wing); bird.right.localRotation = Quaternion.Euler(0,0,-wing);
            }
            // Circling birds remain hittable too, using the physical blade rather than an auto-cut.
            if(phase==Phase.Fight&&swingSpeed>1.3f)
            {
                for(int i=SwordCount;i<Count;i++)
                    if(!birds[i].restored&&HitsBlade(birds[i],StrikeRadius))DownBird(i,birds[i]);
            }
            if (phase == Phase.Fight && attacker < 0 && clock >= nextAttack)
            {
                for (int i = SwordCount; i < Count; i++)
                    if (!birds[i].restored && clock - phaseStart > (i - SwordCount) * 1.3f + 3)
                    { attacker = i; birds[i].motion = Motion.Climb; birds[i].progress = 0; birds[i].start = birds[i].root.position; Message = "A red bird breaks orbit. Ready your blade."; break; }
            }
            if (phase == Phase.Fight && Restored == Count - SwordCount) BeginMeditation();
            previousTip = BladeTip;
            for(int i=0;i<BladeCurve.Length;i++)previousCurve[i]=sword.TransformPoint(BladeCurve[i]);
            swordHistory = phase == Phase.Fight;
            UpdateAppearance();
        }
        void UpdateAttacker(int index, Bird bird, float dt)
        {
            float difficulty = Restored / 15f;
            bird.progress += dt;
            float side = index % 2 == 0 ? -1 : 1;
            if (bird.motion == Motion.Climb)
            {
                var perch = owner.Body + new Vector3(side * 1.15f, 1.45f, -0.12f);
                bird.root.position = Vector3.Lerp(bird.start, perch, Mathf.SmoothStep(0,1,bird.progress / 1.3f));
                if (bird.progress >= 1.3f) { bird.motion = Motion.Warning; bird.progress = 0; bird.start = bird.root.position; Message = "Incoming dive / strike across the red line."; }
            }
            else if (bird.motion == Motion.Warning)
            {
                bird.root.position = bird.start + Vector3.up * Mathf.Sin(visualClock * 5) * 0.035f;
                warning.enabled = true; warning.SetPosition(0, bird.root.position); warning.SetPosition(1, owner.Body - Vector3.up * 0.12f);
                if (bird.progress >= Mathf.Lerp(1.5f,0.9f,difficulty))
                { bird.motion = Motion.Dive; bird.progress = 0; attackStart = bird.root.position; attackTarget = owner.Body - Vector3.up * 0.12f; bird.start = attackTarget + (attackTarget - attackStart).normalized * 0.7f; }
            }
            else if (bird.motion == Motion.Dive)
            {
                float duration = Mathf.Lerp(1.2f,0.85f,difficulty);
                bird.root.position = Vector3.Lerp(attackStart, bird.start, bird.progress / duration);
                bool blocked = HitsBlade(bird,StrikeRadius);
                if (blocked)
                {
                    DownBird(index,bird);return;
                }
                if (SegmentDistance(bird.previous,bird.root.position,owner.Body - Vector3.up * 0.12f,owner.Body - Vector3.up * 0.12f) < 0.2f)
                { HitsReceived++; flash = 0.22f; hitFlash = true; Message = "Hit / recover and watch the next dive. No reset penalty."; Recover(bird,false); }
                else if (bird.progress >= duration) { Message = "Dodged / the bird remains corrupted."; Recover(bird,false); }
            }
            else if (bird.motion == Motion.Recover)
            {
                bird.root.position = Vector3.Lerp(bird.start, Orbit(index), Mathf.SmoothStep(0,1,bird.progress / 1.2f));
                if (bird.progress >= 1.2f) { bird.motion = Motion.Orbit; attacker = -1; nextAttack = clock + Mathf.Lerp(2.3f,1.1f,difficulty); }
            }
            if((bird.motion==Motion.Climb||bird.motion==Motion.Warning)&&swingSpeed>1.3f&&HitsBlade(bird,StrikeRadius))DownBird(index,bird);
        }
        float StrikeRadius => blockRadius + Mathf.InverseLerp(1.2f,4.5f,swingSpeed)*maximumSwingBonus;
        void DownBird(int index,Bird bird)
        {
            if(bird.restored)return;
            if(phase==Phase.Fight)flockPulseAt=clock;
            bird.restored=true;Restored++;bird.motion=Motion.Falling;bird.progress=0;bird.start=bird.root.position;
            float angle=index*2.39996f;
            bird.ground=new Vector3(owner.Body.x+Mathf.Cos(angle)*(0.7f+(index%3)*0.25f),floorLevel+0.045f,owner.Body.z+Mathf.Sin(angle)*0.7f);
            flash=0.12f;hitFlash=false;
            if(index==attacker){warning.enabled=false;attacker=-1;nextAttack=clock+1.8f;}
            Message="STRUCK / "+Restored+" of 15 grounded. Their story is not finished.";
        }
        void Recover(Bird bird, bool restored)
        { bird.motion = Motion.Recover; bird.progress = 0; bird.start = bird.root.position; }
        void BeginMeditation()
        {
            
            phase = Phase.Meditation; phaseStart=clock; meditation=stillFor=0; motionHistory=false;
            sword.gameObject.SetActive(false); warning.enabled = false; attacker = -1;
            for (int i = 0; i < SwordCount; i++)
            { var b = birds[i]; b.root.position = FusionPoint(i); b.start = b.root.position; b.progress = 0; b.restored = true; b.motion = Motion.Returning; b.root.gameObject.SetActive(true); }
            owner.form = GesturalBoidController.Form.Swarm;
            Message = "THE SHADOW WAITS / sit or kneel, rest your hands low, and become still.";
        }
        void UpdateRitual(float dt)
        {
            float bodySpeed=motionHistory?Vector3.Distance(owner.Body,previousBody)/Mathf.Max(dt,0.001f):0;
            float handSpeed=motionHistory?Mathf.Max(Vector3.Distance(owner.Hand,previousHand),Vector3.Distance(owner.OtherHand,previousOther))/Mathf.Max(dt,0.001f):0;
            motionSpeed=Mathf.Lerp(motionSpeed,Mathf.Max(bodySpeed,handSpeed*0.65f),1-Mathf.Exp(-4*dt));
            previousBody=owner.Body;previousHand=owner.Hand;previousOther=owner.OtherHand;motionHistory=true;
            if(phase==Phase.Meditation)
            {
                bool handsLive=owner.Pose.previewOnly||!owner.useKinect||(owner.Pose.WeaponHandLive&&owner.Pose.OtherHandLive);
                bool lowered=allowStandingMeditation||owner.Body.y<standingHeight-0.28f;
                bool resting=owner.Hand.y<owner.Body.y-0.12f&&owner.OtherHand.y<owner.Body.y-0.12f&&Vector3.Distance(owner.Hand,owner.OtherHand)<0.8f;
                bool calm=handsLive&&lowered&&resting&&motionSpeed<0.28f;
                stillFor=calm?stillFor+dt:0;
                meditationCue=!handsLive?"Keep both hands visible to Kinect.":!lowered?"Sit or kneel lower than your standing pose.":!resting?"Rest both hands low, near your lap.":motionSpeed>=0.28f?"Let your body and hands settle.":stillFor<2?"Settling into stillness...":"The injured birds are awakening.";
                if(calm&&stillFor>=2)meditation=Mathf.Clamp01(meditation+dt/Mathf.Max(5,meditationSeconds));
                else meditation=Mathf.Max(0,meditation-dt*0.01f);
                if(meditation>=1)
                {
                    phase=Phase.Integrating;phaseStart=clock;absorbed=0;
                    for(int i=0;i<Count;i++){birds[i].motion=Motion.Absorbing;birds[i].start=birds[i].root.position;birds[i].root.gameObject.SetActive(true);}
                    Message="INTEGRATION / what you fought was also yours.";
                }
            }
            else if(phase==Phase.Integrating&&absorbed==Count)
            {phase=Phase.Victory;phaseStart=clock;Message="WHOLE / shadow and light return to one body.";}
            else if(phase==Phase.Victory)whiteRoom=Mathf.SmoothStep(0,1,(clock-phaseStart)/4);
        }
        void UpdateHealingBird(int index,Bird bird,float dt)
        {
            bird.progress+=dt;
            if(bird.motion==Motion.Falling)
            {
                float t=Mathf.Clamp01(bird.progress/1.1f);
                bird.root.position=Vector3.Lerp(bird.start,bird.ground,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*0.18f;
                if(t>=1){bird.motion=Motion.Grounded;bird.progress=0;}
            }
            else if(bird.motion==Motion.Grounded)
            {
                bird.root.position=bird.ground;
                if(phase==Phase.Meditation&&meditation>=(index-SwordCount+1)/16f*0.9f)
                {bird.motion=Motion.Rising;bird.progress=0;bird.start=bird.ground;}
            }
            else if(bird.motion==Motion.Rising)
            {
                float t=Mathf.Clamp01(bird.progress/2);
                bird.root.position=Vector3.Lerp(bird.start,Orbit(index),Mathf.SmoothStep(0,1,t))+Vector3.up*Mathf.Sin(t*Mathf.PI)*0.3f;
                if(t>=1){bird.motion=Motion.Orbit;bird.flightVelocity=Vector3.zero;}
            }
            else if(bird.motion==Motion.Absorbing)
            {
                float t=Mathf.Clamp01((clock-phaseStart-index*0.17f)/2.3f);
                var spiral=new Vector3(Mathf.Cos(t*9+index),Mathf.Sin(t*9+index),0)*Mathf.Sin(t*Mathf.PI)*0.35f;
                bird.root.position=Vector3.Lerp(bird.start,owner.Body,Mathf.SmoothStep(0,1,t))+spiral;
                bird.root.localScale=Vector3.one*bird.size*Mathf.Max(0.001f,1-t);
                if(t>=1){bird.root.gameObject.SetActive(false);absorbed++;}
            }
        }
        void UpdateAppearance()
        {
            float integration=phase==Phase.Victory?1:phase==Phase.Integrating?Mathf.Clamp01((clock-phaseStart)/5.5f):phase==Phase.Meditation?meditation*0.22f:0;
            var avatar=owner.GetComponent<BoidAvatar>();if(avatar!=null)avatar.Integration=integration;
            roomMaterial.SetVector("_Center",owner.Body);
            roomMaterial.SetFloat("_Glow",phase==Phase.Victory?1:integration*0.7f);
            roomMaterial.SetFloat("_Whiten",whiteRoom);
            if(Camera.main!=null)Camera.main.backgroundColor=Color.Lerp(new Color(0.09f,0.09f,0.105f),Color.white,whiteRoom);
        }
        public void DrawHUD()
        {
            GUILayout.Label(phase == Phase.Roost ? "THE ROOST / 20 circling birds" : phase == Phase.Victory ? "WHOLE / the shadow integrated" : phase==Phase.Meditation?"STILLNESS / awaken the injured flock":phase==Phase.Integrating?"INTEGRATION / welcome them home":"THE BETRAYAL / face the shadow");
            GUILayout.Label(Message);
            if (SwordEquipped) GUILayout.Label("Grounded " + Restored + "/15 / hits received " + HitsReceived+" / swing reach +"+Mathf.RoundToInt((StrikeRadius-blockRadius)*100)+"cm");
            if(phase==Phase.Meditation)
            {GUILayout.Label(meditationCue+"\nStillness "+Mathf.RoundToInt(meditation*100)+"%");allowStandingMeditation=GUILayout.Toggle(allowStandingMeditation,"Allow still standing meditation instead");}
            if(phase==Phase.Integrating)GUILayout.Label(absorbed+" / 20 birds welcomed home");
            if (InProgress && (owner.Pose.CenterPending || !(owner.Pose.previewOnly || !owner.useKinect || (owner.BodyTracked && (!SwordEquipped || owner.Pose.WeaponHandLive))))) GUILayout.Label(owner.Pose.CenterPending ? "PAUSED / centering player" : "PAUSED / return to tracking view");
            if (startAt >= 0) GUILayout.Label("Sword call in " + Mathf.CeilToInt(Mathf.Max(0,startAt - Time.unscaledTime)) + "s / step into position");
            if(meditationPreviewAt>=0)GUILayout.Label("Ending preview in "+Mathf.CeilToInt(Mathf.Max(0,meditationPreviewAt-Time.unscaledTime))+"s / step back");
            if (!InProgress && GUILayout.Button(phase == Phase.Victory ? "New encounter in 8 seconds" : "Call sword in 8 seconds")) ScheduleStart();
            GUILayout.Label("Space / phone snap calls the sword. Move the blade directly through nearby birds. C: phone screen-up on table, top edge toward sensor. Injured birds wait for your stillness.");
            if(GUILayout.Button("Preview meditation ending in 8 seconds"))meditationPreviewAt=Time.unscaledTime+8;
            if (GUILayout.Button("Reset encounter to quiet flock")) ResetEncounter();
        }
        void OnGUI()
        {
            if (owner?.Room == null || owner.Room.SetupMode) return;
            var previous = GUI.color;
            if(flash>0)
            {
                GUI.color = hitFlash ? new Color(0.7f,0,0,flash * 0.8f) : new Color(1,1,1,flash * 0.6f);
                GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
            }
            GUI.color=whiteRoom>0.6f?Color.black:Color.white;
            if(storyStyle==null)storyStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=18};
            string caption=phase==Phase.Meditation?meditationCue+"  /  "+Mathf.RoundToInt(meditation*100)+"%":Message;
            if(InProgress&&!RequiredTracking)caption=owner.Pose.CenterPending?"Paused / centering player":"Paused / return to Kinect view";
            if(phase!=Phase.Roost)GUI.Label(new Rect(20,Screen.height-118,Screen.width-40,76),caption,storyStyle);
            GUI.color=previous;
        }
        bool HitsBlade(Bird bird,float radius)
        {
            for(int i=0;i<BladeCurve.Length-1;i++)
            {
                Vector3 currentA=sword.TransformPoint(BladeCurve[i]),currentB=sword.TransformPoint(BladeCurve[i+1]);
                for(int sample=0;sample<=4;sample++)
                {
                    float t=sample/4f;
                    Vector3 a=swordHistory?Vector3.Lerp(previousCurve[i],currentA,t):currentA;
                    Vector3 b=swordHistory?Vector3.Lerp(previousCurve[i+1],currentB,t):currentB;
                    if(SegmentDistance(bird.previous,bird.root.position,a,b)<=radius)return true;
                }
            }
            return false;
        }
        Mesh CurvedBlade(float widthScale,float halfDepth)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<BladeCurve.Length;i++)
            {
                Vector3 tangent=BladeCurve[Mathf.Min(i+1,BladeCurve.Length-1)]-BladeCurve[Mathf.Max(0,i-1)];
                Vector3 normal=new Vector3(tangent.y,-tangent.x,0).normalized*BladeWidths[i]*widthScale;
                vertices.Add(BladeCurve[i]-normal-Vector3.forward*halfDepth);vertices.Add(BladeCurve[i]+normal-Vector3.forward*halfDepth);
                vertices.Add(BladeCurve[i]-normal+Vector3.forward*halfDepth);vertices.Add(BladeCurve[i]+normal+Vector3.forward*halfDepth);
            }
            void Quad(int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
            for(int i=0;i<BladeCurve.Length-1;i++){int a=i*4,b=(i+1)*4;Quad(a,a+1,b+1,b);Quad(a+2,b+2,b+3,a+3);Quad(a,b,b+2,a+2);Quad(a+1,a+3,b+3,b+1);}
            Quad(0,2,3,1);int last=(BladeCurve.Length-1)*4;Quad(last,last+1,last+3,last+2);
            return MeshFrom(vertices.ToArray(),triangles.ToArray());
        }
        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 u=b-a,v=d-c,w=a-c; float uu=Vector3.Dot(u,u),vv=Vector3.Dot(v,v),uv=Vector3.Dot(u,v),uw=Vector3.Dot(u,w),vw=Vector3.Dot(v,w);
            float s=0,t=0,den=uu*vv-uv*uv;
            if (uu<0.000001f && vv<0.000001f) return Vector3.Distance(a,c);
            if (uu<0.000001f) t=Mathf.Clamp01(vw/vv);
            else if(vv<0.000001f) s=Mathf.Clamp01(-uw/uu);
            else { s=den>0.000001f?Mathf.Clamp01((uv*vw-vv*uw)/den):0; t=(uv*s+vw)/vv; if(t<0){t=0;s=Mathf.Clamp01(-uw/uu);}else if(t>1){t=1;s=Mathf.Clamp01((uv-uw)/uu);} }
            return Vector3.Distance(a+u*s,c+v*t);
        }
        Mesh Extrude(Vector2[] outline, float halfDepth)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>(); int n=outline.Length;
            for(int i=0;i<n;i++){ vertices.Add(new Vector3(outline[i].x,outline[i].y,-halfDepth)); vertices.Add(new Vector3(outline[i].x,outline[i].y,halfDepth)); }
            for(int i=1;i<n-1;i++){triangles.Add(0);triangles.Add(i*2);triangles.Add((i+1)*2);triangles.Add(1);triangles.Add((i+1)*2+1);triangles.Add(i*2+1);}
            for(int i=0;i<n;i++){int j=(i+1)%n;triangles.Add(i*2);triangles.Add(i*2+1);triangles.Add(j*2);triangles.Add(j*2);triangles.Add(i*2+1);triangles.Add(j*2+1);}
            return MeshFrom(vertices.ToArray(),triangles.ToArray());
        }
        Mesh MeshFrom(Vector3[] source, int[] indices)
        {
            var vertices=new Vector3[indices.Length]; var triangles=new int[indices.Length];
            for(int i=0;i<indices.Length;i++){vertices[i]=source[indices[i]];triangles[i]=i;}
            var mesh=new Mesh{name="Faceted bird / weapon mesh"}; mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
        }
        void OnDestroy()
        { if(content!=null)Destroy(content.gameObject);foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);if(ink!=null)Destroy(ink);if(lineMaterial!=null)Destroy(lineMaterial);if(roomMaterial!=null)Destroy(roomMaterial); }
    }
}
