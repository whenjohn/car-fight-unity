using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarFight.Editor
{
    public static class ProjectBootstrap
    {
        private const string SceneDirectory = "Assets/CarFight/Scenes";
        private const string BootstrapScene = SceneDirectory + "/Bootstrap.unity";
        private static readonly string[] StockTemplateAssets =
        {
            "Assets/Readme.asset",
            "Assets/TutorialInfo",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Resources"
        };

        [MenuItem("Car Fight/Apply Project Bootstrap")]
        public static void Apply()
        {
            PlayerSettings.companyName = "whenjohn";
            PlayerSettings.productName = "Car Fight";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.MaximizedWindow;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;

            Directory.CreateDirectory(SceneDirectory);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("CarFight");
            EditorSceneManager.SaveScene(scene, BootstrapScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScene, true) };

            foreach (string assetPath in StockTemplateAssets)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"Car Fight bootstrap applied. Scene: {BootstrapScene}");
        }
    }
}
