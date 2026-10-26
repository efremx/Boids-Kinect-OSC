using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids
{
    // A faceted mannequin built from the same joints as the weapon anchor, without a rig import.
    public class BoidAvatar : MonoBehaviour
    {
        public bool showAvatar = true;
        public bool frameCamera = true;
        public float cameraHalfHeight = 2.05f;
        [Range(0,1)] public float Integration;
        readonly List<Transform> parts = new List<Transform>();
        readonly List<Vector2Int> bones = new List<Vector2Int>();
        readonly List<Vector2> widths = new List<Vector2>();
        Mesh limbMesh;
        Mesh headMesh;
        Material material;
        BoidBodyPose pose;
        Transform head, handMarker;
        Camera framedCamera;
        GameObject root;
        readonly Vector3[] display = new Vector3[32];
        readonly Vector3[] directions = new Vector3[32];
        void Start()
        {
            pose = GetComponent<BoidBodyPose>();
            root = new GameObject("White low-poly avatar"); root.transform.SetParent(transform, false);
            limbMesh = CreateLimb();
            material = new Material(Shader.Find("GesturalBoids/FacetedWhite"));
            Add(KinectInterop.JointType.Pelvis, KinectInterop.JointType.SpineChest, 0.18f, 0.25f);
            Add(KinectInterop.JointType.SpineChest, KinectInterop.JointType.Neck, 0.13f, 0.065f);
            Add(KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.ShoulderRight, 0.11f, 0.11f);
            Add(KinectInterop.JointType.HipLeft, KinectInterop.JointType.HipRight, 0.14f, 0.14f);
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var shoulder = left ? KinectInterop.JointType.ShoulderLeft : KinectInterop.JointType.ShoulderRight;
                var elbow = left ? KinectInterop.JointType.ElbowLeft : KinectInterop.JointType.ElbowRight;
                var hand = left ? KinectInterop.JointType.HandLeft : KinectInterop.JointType.HandRight;
                var hip = left ? KinectInterop.JointType.HipLeft : KinectInterop.JointType.HipRight;
                var knee = left ? KinectInterop.JointType.KneeLeft : KinectInterop.JointType.KneeRight;
                var ankle = left ? KinectInterop.JointType.AnkleLeft : KinectInterop.JointType.AnkleRight;
                var foot = left ? KinectInterop.JointType.FootLeft : KinectInterop.JointType.FootRight;
                Add(shoulder, elbow, 0.085f, 0.065f); Add(elbow, hand, 0.065f, 0.045f);
                Add(hip, knee, 0.105f, 0.075f); Add(knee, ankle, 0.075f, 0.055f);
            }
            head = Part("Simple head"); headMesh = CreateHead(); head.GetComponent<MeshFilter>().sharedMesh = headMesh;
            head.localScale = new Vector3(0.13f, 0.17f, 0.13f);
            handMarker = Part("Weapon hand anchor"); handMarker.localScale = new Vector3(0.065f, 0.11f, 0.065f);
        }
        void Add(KinectInterop.JointType a, KinectInterop.JointType b, float bottom, float top)
        {
            bones.Add(new Vector2Int((int)a, (int)b)); widths.Add(new Vector2(bottom, top)); parts.Add(Part(a + " to " + b));
        }
        Transform Part(string name)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(root.transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = limbMesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }
        void LateUpdate()
        {
            if (pose == null || root == null) return;
            material.SetFloat("_Integration", Integration);
            root.SetActive(showAvatar);
            if (frameCamera && Camera.main != null && framedCamera != Camera.main)
            {
                framedCamera = Camera.main;
                framedCamera.orthographic = true; framedCamera.orthographicSize = cameraHalfHeight;
                framedCamera.transform.position = new Vector3(0, 1.25f, -7);
                framedCamera.transform.rotation = Quaternion.identity;
            }
            // Solve each branch from its parent. A missing ankle can never remain pinned in world space
            // while its hip moves, and every segment has a plausible maximum length.
            display[(int)KinectInterop.JointType.SpineChest] = pose.Joint(KinectInterop.JointType.SpineChest);
            Solve(0,2,0.32f,0.56f,Vector3.down); Solve(3,2,0.10f,0.24f,Vector3.up); Solve(4,3,0.10f,0.22f,Vector3.up);
            Solve(6,2,0.16f,0.30f,Vector3.left); Solve(7,6,0.20f,0.42f,new Vector3(-0.4f,-1,0)); Solve(9,7,0.18f,0.43f,new Vector3(-0.1f,-1,0));
            Solve(11,2,0.16f,0.30f,Vector3.right); Solve(12,11,0.20f,0.42f,new Vector3(0.4f,-1,0)); Solve(14,12,0.18f,0.43f,new Vector3(0.1f,-1,0));
            Solve(15,0,0.09f,0.18f,Vector3.left); Solve(16,15,0.30f,0.53f,Vector3.down); Solve(17,16,0.28f,0.53f,Vector3.down);
            Solve(19,0,0.09f,0.18f,Vector3.right); Solve(20,19,0.30f,0.53f,Vector3.down); Solve(21,20,0.28f,0.53f,Vector3.down);
            for (int i = 0; i < parts.Count; i++)
            {
                var a = display[bones[i].x]; var b = display[bones[i].y];
                var delta = b - a;
                parts[i].position = a;
                if (delta.sqrMagnitude > 0.00001f) parts[i].rotation = Quaternion.FromToRotation(Vector3.up, delta);
                parts[i].localScale = new Vector3(widths[i].x, Mathf.Max(0.001f, delta.magnitude), widths[i].y);
            }
            head.position = display[(int)KinectInterop.JointType.Head];
            var controller = GetComponent<GesturalBoidController>();
            handMarker.position = controller.Hand - Vector3.up * 0.055f;
        }

        void Solve(int joint, int parent, float minimum, float maximum, Vector3 fallback)
        {
            var delta = pose.Joints[joint] - display[parent];
            bool live = pose.JointLive((KinectInterop.JointType)joint) || !pose.EverTracked || pose.previewOnly;
            if (live && delta.sqrMagnitude > 0.0001f)
            {
                directions[joint] = delta.normalized;
                display[joint] = display[parent] + delta.normalized * Mathf.Clamp(delta.magnitude, minimum, maximum);
            }
            else
            {
                var direction = directions[joint].sqrMagnitude > 0.1f ? directions[joint] : fallback.normalized;
                display[joint] = display[parent] + direction * ((minimum + maximum) * 0.5f);
            }
            if((joint==16||joint==17||joint==20||joint==21)&&GetComponent<GesturalBoidController>().Calibration.Ready)
                display[joint].y=Mathf.Max(0.06f,display[joint].y);
        }
        static Mesh CreateHead()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector3 Point(int ring, int side)
            {
                float latitude = Mathf.PI * ring / 5, angle = Mathf.PI * side / 4;
                return new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle));
            }
            for(int ring=0;ring<5;ring++)for(int side=0;side<8;side++)
            {
                if(ring>0)Triangle(vertices,triangles,Point(ring,side),Point(ring+1,side),Point(ring,side+1));
                if(ring<4)Triangle(vertices,triangles,Point(ring,side+1),Point(ring+1,side),Point(ring+1,side+1));
            }
            var mesh=new Mesh{name="Simple faceted oval head"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh CreateLimb()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            // Six sides with a narrow upper ring; duplicated vertices preserve flat normals.
            for (int side = 0; side < 6; side++)
            {
                float a = side * Mathf.PI / 3, b = (side + 1) * Mathf.PI / 3;
                var loA = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var loB = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                var hiA = new Vector3(Mathf.Cos(a) * 0.72f, 1, Mathf.Sin(a) * 0.72f); var hiB = new Vector3(Mathf.Cos(b) * 0.72f, 1, Mathf.Sin(b) * 0.72f);
                Triangle(vertices, triangles, loA, hiA, hiB); Triangle(vertices, triangles, loA, hiB, loB);
                Triangle(vertices, triangles, Vector3.zero, loA, loB); Triangle(vertices, triangles, Vector3.up, hiB, hiA);
            }
            var mesh = new Mesh { name = "Flat shaded six-sided avatar mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static void Triangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
        void OnDestroy() { if (root != null) Destroy(root); if (material != null) Destroy(material); if (limbMesh != null) Destroy(limbMesh); if(headMesh!=null)Destroy(headMesh); }
    }
}
