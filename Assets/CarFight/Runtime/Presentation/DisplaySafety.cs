using System;
using UnityEngine;

namespace CarFight.Presentation
{
    public static class DisplaySafetyPolicy
    {
        public const string WindowedFallbackPreference = "carfight.display.windowed-fallback";

        public static bool IsAffectedIntelMac(RuntimePlatform platform, string processorType)
        {
            bool isMac = platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.OSXEditor;
            return isMac
                && !string.IsNullOrEmpty(processorType)
                && processorType.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static FullScreenMode ResolveStartupMode(
            RuntimePlatform platform,
            string processorType,
            FullScreenMode requestedMode,
            bool preferWindowed)
        {
            if (preferWindowed)
                return FullScreenMode.Windowed;

            if (requestedMode == FullScreenMode.FullScreenWindow
                && IsAffectedIntelMac(platform, processorType))
            {
                return FullScreenMode.MaximizedWindow;
            }

            return requestedMode;
        }
    }

    /// <summary>
    /// Applies the Intel Mac presentation guardrail and records lightweight display events.
    /// Heavy WindowServer and framebuffer monitoring belongs in external test tooling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplaySafety : MonoBehaviour
    {
        private const int FallbackWidth = 1440;
        private const int FallbackHeight = 900;
        private const float DevelopmentSampleSeconds = 10f;

        private static DisplaySafety instance;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float sampleElapsed;
        private float longestFrame;
        private int sampleFrames;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode || instance != null)
                return;

            GameObject service = new GameObject(nameof(DisplaySafety));
            service.AddComponent<DisplaySafety>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Display.onDisplaysUpdated += OnDisplaysUpdated;

            bool preferWindowed = PlayerPrefs.GetInt(
                DisplaySafetyPolicy.WindowedFallbackPreference,
                0) == 1;
            FullScreenMode requestedMode = Screen.fullScreenMode;
            FullScreenMode safeMode = DisplaySafetyPolicy.ResolveStartupMode(
                Application.platform,
                SystemInfo.processorType,
                requestedMode,
                preferWindowed);

            if (safeMode == FullScreenMode.Windowed && requestedMode != safeMode)
                Screen.SetResolution(FallbackWidth, FallbackHeight, safeMode);
            else if (safeMode != requestedMode)
                Screen.fullScreenMode = safeMode;

            LogDisplayState(safeMode == requestedMode ? "startup" : $"startup-fallback-from-{requestedMode}");
        }

        public static void RequestWindowedFallback(bool remember = true)
        {
            if (remember)
                SaveWindowedPreference(true);

            Screen.SetResolution(FallbackWidth, FallbackHeight, FullScreenMode.Windowed);
            LogDisplayState("windowed-fallback-requested");
        }

        public static void RequestMaximizedWindow(bool remember = true)
        {
            if (remember)
                SaveWindowedPreference(false);

            Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
            LogDisplayState("maximized-window-requested");
        }

        private static void SaveWindowedPreference(bool preferWindowed)
        {
            PlayerPrefs.SetInt(
                DisplaySafetyPolicy.WindowedFallbackPreference,
                preferWindowed ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            LogDisplayState(hasFocus ? "focus-gained" : "focus-lost");
        }

        private void OnApplicationPause(bool isPaused)
        {
            LogDisplayState(isPaused ? "paused" : "resumed");
        }

        private void OnDisplaysUpdated()
        {
            LogDisplayState("displays-updated");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            float frameSeconds = Time.unscaledDeltaTime;
            sampleElapsed += frameSeconds;
            longestFrame = Mathf.Max(longestFrame, frameSeconds);
            sampleFrames++;

            if (sampleElapsed < DevelopmentSampleSeconds)
                return;

            float averageFps = sampleElapsed > 0f ? sampleFrames / sampleElapsed : 0f;
            Debug.Log(
                $"[DisplaySafety] frame-sample averageFps={averageFps:F1} "
                + $"longestFrameMs={longestFrame * 1000f:F1} window={Screen.width}x{Screen.height} "
                + $"mode={Screen.fullScreenMode}");
            sampleElapsed = 0f;
            longestFrame = 0f;
            sampleFrames = 0;
        }
#endif

        private void OnDestroy()
        {
            if (instance != this)
                return;

            Display.onDisplaysUpdated -= OnDisplaysUpdated;
            instance = null;
        }

        private static void LogDisplayState(string reason)
        {
            Debug.Log(
                $"[DisplaySafety] {reason} platform={Application.platform} "
                + $"processor={SystemInfo.processorType} displays={Display.displays.Length} "
                + $"window={Screen.width}x{Screen.height} mode={Screen.fullScreenMode} "
                + $"focused={Application.isFocused}");
        }
    }
}
