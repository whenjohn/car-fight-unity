using System;
using FishNet.Managing;
using FishNet.Managing.Object;
using UnityEngine;

namespace CarFight.Networking.Runtime
{
    public static class NetworkBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallForCommandLineRole()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!NetworkLaunchOptions.HasNetworkRole(arguments))
                return;

            if (!NetworkLaunchOptions.TryParse(arguments, out NetworkLaunchOptions options, out string error))
            {
                Debug.LogError($"CFNET event=BOOTSTRAP_FAILED reason={Token(error)}");
                Application.Quit(2);
                return;
            }

            GameObject root = new GameObject("CarFightNetworkRuntime");
            root.SetActive(false);
            NetworkManager manager = root.AddComponent<NetworkManager>();
            manager.SpawnablePrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            BaselineNetworkScenario scenario = root.AddComponent<BaselineNetworkScenario>();
            root.SetActive(true);

            if (!manager.Initialized)
            {
                Debug.LogError("CFNET event=BOOTSTRAP_FAILED reason=fishnet_initialization");
                Application.Quit(2);
                return;
            }

            manager.ServerManager.SetStartOnHeadless(false);
            scenario.Begin(manager, options);
        }

        private static string Token(string value)
        {
            return value.Replace(' ', '_');
        }
    }
}
