using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using com.rfilkov.kinect;

namespace GesturalBoids.Editor
{
    public static class BoidsSceneBuilder
    {
        [MenuItem("Boids/Create Prototype Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 1.6f, -5.5f);
            cameraObject.transform.LookAt(new Vector3(0, 1.4f, 0));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.018f, 0.04f);
            camera.fieldOfView = 55;
            new GameObject("Gestural Boids", typeof(BoidPhoneInput), typeof(GesturalBoidController));
            // Optional rig stays disabled until a sensor is connected, avoiding startup errors in preview mode.
            var rig = new GameObject("Kinect Rig - enable when sensor connected");
            rig.SetActive(false);
            rig.AddComponent<KinectManager>();
            var sensor = new GameObject("Azure Kinect Sensor");
            sensor.transform.SetParent(rig.transform);
            sensor.AddComponent<Kinect4AzureInterface>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/GesturalBoids/GesturalBoids.unity");
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("BOIDS_SCENE_CREATED " + path);
        }
    }
}
