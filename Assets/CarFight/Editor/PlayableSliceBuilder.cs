using System.IO;
using CarFight.Driving;
using CarFight.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarFight.Editor
{
    public static class PlayableSliceBuilder
    {
        private const string ScenePath = "Assets/CarFight/Scenes/Bootstrap.unity";
        private const string MaterialDirectory = "Assets/CarFight/Materials";
        private const float ArenaHalfExtent = 84f;
        private const int ArenaLayer = 2;

        [MenuItem("Car Fight/Rebuild Local Driving Slice")]
        public static void Apply()
        {
            Directory.CreateDirectory(MaterialDirectory);
            AssetDatabase.Refresh();

            Material grid = Material("GridGround", Shader.Find("CarFight/GridGround"), Color.white);
            Material wall = Material("ArenaWall", Shader.Find("Universal Render Pipeline/Lit"), Hex("596674"));
            Material jeep = Material("JeepGreen", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.18f, 0.48f, 0.22f));
            Material opponent = Material("JeepOrange", Shader.Find("Universal Render Pipeline/Lit"), Hex("ffb45e"));
            Material dark = Material("JeepDark", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.055f, 0.075f, 0.095f));
            Material glass = Material("JeepGlass", Shader.Find("Universal Render Pipeline/Lit"), Hex("75b8c8"));
            Material cyan = Material("PlayerCyan", Shader.Find("Universal Render Pipeline/Unlit"), Hex("63d8ff"));
            Material speed = Material("MaxSpeedIvory", Shader.Find("Universal Render Pipeline/Unlit"), Hex("fff1b8"));

            PhysicsMaterial physicsMaterial = PhysicsMaterial();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("CarFight");

            ConfigureEnvironment();
            CreateArena(root.transform, grid, wall, physicsMaterial);
            GameObject jeepRoot = CreateJeep(
                "LocalJeep",
                root.transform,
                new Vector3(0f, VehiclePhysicsProfile.CollisionRadius, 0f),
                Quaternion.identity,
                jeep,
                dark,
                glass,
                cyan,
                physicsMaterial,
                locallyControlled: true);
            CreateJeep(
                "CollisionJeep",
                root.transform,
                new Vector3(0f, VehiclePhysicsProfile.CollisionRadius, -10f),
                Quaternion.Euler(0f, 180f, 0f),
                opponent,
                dark,
                glass,
                opponent,
                physicsMaterial,
                locallyControlled: false);
            Camera camera = CreateCamera(root.transform, jeepRoot.transform);
            CursorIntentView cursor = CreateCursor(root.transform, cyan, speed);
            jeepRoot.GetComponent<LocalJeepController>().Configure(camera, cursor, 1 << ArenaLayer);
            CreateLight(root.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = jeepRoot;
            Debug.Log("Car Fight local driving slice rebuilt.");
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
                throw new InvalidDataException("Playable scene has no camera.");

            const int width = 1280;
            const int height = 720;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();

            Directory.CreateDirectory("TestResults");
            File.WriteAllBytes("TestResults/playable-preview.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Debug.Log("Car Fight playable preview captured.");
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("b6cad3");
            RenderSettings.ambientIntensity = 0.08f;
            RenderSettings.fog = false;
        }

        private static void CreateArena(
            Transform parent,
            Material gridMaterial,
            Material wallMaterial,
            PhysicsMaterial physicsMaterial)
        {
            GameObject collision = GameObject.CreatePrimitive(PrimitiveType.Cube);
            collision.name = "GroundCollision";
            collision.transform.SetParent(parent);
            collision.transform.position = new Vector3(0f, -0.5f, 0f);
            collision.transform.localScale = new Vector3(ArenaHalfExtent * 2f, 1f, ArenaHalfExtent * 2f);
            collision.layer = ArenaLayer;
            collision.GetComponent<Collider>().material = physicsMaterial;
            Object.DestroyImmediate(collision.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(collision.GetComponent<MeshFilter>());

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "ShaderGridGround";
            surface.transform.SetParent(parent);
            surface.transform.position = new Vector3(0f, -0.01f, 0f);
            surface.transform.localScale = new Vector3(ArenaHalfExtent / 5f, 1f, ArenaHalfExtent / 5f);
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            MeshRenderer surfaceRenderer = surface.GetComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = gridMaterial;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = true;

            const float wallHeight = 4f;
            const float wallThickness = 1f;
            CreateBox("WallNorth", parent, new Vector3(0f, wallHeight * 0.5f, -ArenaHalfExtent),
                new Vector3(ArenaHalfExtent * 2f + wallThickness * 2f, wallHeight, wallThickness), wallMaterial, physicsMaterial);
            CreateBox("WallSouth", parent, new Vector3(0f, wallHeight * 0.5f, ArenaHalfExtent),
                new Vector3(ArenaHalfExtent * 2f + wallThickness * 2f, wallHeight, wallThickness), wallMaterial, physicsMaterial);
            CreateBox("WallWest", parent, new Vector3(-ArenaHalfExtent, wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, ArenaHalfExtent * 2f), wallMaterial, physicsMaterial);
            CreateBox("WallEast", parent, new Vector3(ArenaHalfExtent, wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, ArenaHalfExtent * 2f), wallMaterial, physicsMaterial);
        }

        private static GameObject CreateJeep(
            string name,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Material bodyMaterial,
            Material darkMaterial,
            Material glassMaterial,
            Material markerMaterial,
            PhysicsMaterial physicsMaterial,
            bool locallyControlled)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, rotation);

            Rigidbody body = root.AddComponent<Rigidbody>();
            VehiclePhysicsProfile.Configure(body);
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = VehiclePhysicsProfile.CollisionRadius;
            collider.material = physicsMaterial;
            if (locallyControlled)
                root.AddComponent<LocalJeepController>();

            GameObject visual = new GameObject("PrimitiveJeep");
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.down * VehiclePhysicsProfile.CollisionRadius;
            CreateVisualBox("LowerBody", visual.transform, new Vector3(0f, 0.62f, 0f), new Vector3(1.72f, 0.52f, 2.85f), bodyMaterial);
            CreateVisualBox("Hood", visual.transform, new Vector3(0f, 0.86f, -0.84f), new Vector3(1.58f, 0.34f, 1.02f), bodyMaterial);
            CreateVisualBox("Cabin", visual.transform, new Vector3(0f, 1.18f, 0.32f), new Vector3(1.46f, 0.66f, 1.18f), bodyMaterial);
            CreateVisualBox("Windshield", visual.transform, new Vector3(0f, 1.26f, -0.30f), new Vector3(1.28f, 0.38f, 0.08f), glassMaterial);
            CreateVisualBox("FrontBumper", visual.transform, new Vector3(0f, 0.52f, -1.52f), new Vector3(1.82f, 0.18f, 0.16f), darkMaterial);
            CreateVisualBox("RearBumper", visual.transform, new Vector3(0f, 0.52f, 1.52f), new Vector3(1.82f, 0.18f, 0.16f), darkMaterial);

            foreach (float x in new[] { -0.96f, 0.96f })
            foreach (float z in new[] { -0.92f, 0.92f })
                CreateWheel(visual.transform, new Vector3(x, 0.36f, z), darkMaterial);

            GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pip.name = "PeerMarker";
            pip.transform.SetParent(visual.transform);
            pip.transform.localPosition = new Vector3(0f, 1.68f, 0f);
            pip.transform.localScale = new Vector3(0.32f, 0.06f, 0.32f);
            pip.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
            Object.DestroyImmediate(pip.GetComponent<Collider>());
            return root;
        }

        private static Camera CreateCamera(Transform parent, Transform target)
        {
            GameObject cameraObject = new GameObject("IsometricCamera");
            cameraObject.transform.SetParent(parent);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = IsometricFollowCamera.UnityOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("10171d");
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 250f;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            IsometricFollowCamera follow = cameraObject.AddComponent<IsometricFollowCamera>();
            follow.Configure(target);
            return camera;
        }

        private static CursorIntentView CreateCursor(Transform parent, Material cursor, Material speed)
        {
            GameObject root = new GameObject("CursorIntent");
            root.transform.SetParent(parent);

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "CursorMarker";
            marker.transform.SetParent(root.transform);
            marker.transform.localScale = new Vector3(0.56f, 0.02f, 0.56f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = cursor;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            GameObject maximum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            maximum.name = "MaxSpeedMarker";
            maximum.transform.SetParent(root.transform);
            maximum.transform.localScale = new Vector3(0.30f, 0.0225f, 0.30f);
            maximum.GetComponent<MeshRenderer>().sharedMaterial = speed;
            Object.DestroyImmediate(maximum.GetComponent<Collider>());

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "CursorLine";
            line.transform.SetParent(root.transform);
            line.GetComponent<MeshRenderer>().sharedMaterial = cursor;
            Object.DestroyImmediate(line.GetComponent<Collider>());

            CursorIntentView view = root.AddComponent<CursorIntentView>();
            view.Configure(marker.transform, maximum.transform, line.transform);
            return view;
        }

        private static void CreateLight(Transform parent)
        {
            GameObject lightObject = new GameObject("ShadowSun");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(50f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("fff1d4");
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.92f;
            light.shadowBias = 0.08f;
            light.shadowNormalBias = 0.55f;
        }

        private static void CreateBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            PhysicsMaterial physicsMaterial)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.position = position;
            box.transform.localScale = size;
            box.layer = ArenaLayer;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.GetComponent<Collider>().material = physicsMaterial;
        }

        private static void CreateVisualBox(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 size,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        private static void CreateWheel(Transform parent, Vector3 localPosition, Material material)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = localPosition.z < 0f ? "FrontWheel" : "RearWheel";
            wheel.transform.SetParent(parent);
            wheel.transform.localPosition = localPosition;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.62f, 0.18f, 0.62f);
            wheel.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
        }

        private static Material Material(string name, Shader shader, Color color)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static PhysicsMaterial PhysicsMaterial()
        {
            string path = $"{MaterialDirectory}/VehiclePhysics.physicMaterial";
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("VehiclePhysics");
                AssetDatabase.CreateAsset(material, path);
            }

            VehiclePhysicsProfile.Configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString($"#{value}", out Color color);
            return color;
        }
    }
}
