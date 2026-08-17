using CarFight.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CarFight.Tests.Presentation
{
    public sealed class DisplaySafetyPolicyTests
    {
        [Test]
        public void SavedWindowedFallbackAlwaysWins()
        {
            FullScreenMode result = DisplaySafetyPolicy.ResolveStartupMode(
                RuntimePlatform.WindowsPlayer,
                "Any processor",
                FullScreenMode.ExclusiveFullScreen,
                preferWindowed: true);

            Assert.That(result, Is.EqualTo(FullScreenMode.Windowed));
        }

        [Test]
        public void IntelMacBorderlessFullscreenFallsBackToMaximizedWindow()
        {
            FullScreenMode result = DisplaySafetyPolicy.ResolveStartupMode(
                RuntimePlatform.OSXPlayer,
                "Intel(R) Core(TM) i7",
                FullScreenMode.FullScreenWindow,
                preferWindowed: false);

            Assert.That(result, Is.EqualTo(FullScreenMode.MaximizedWindow));
        }

        [Test]
        public void IntelMacWindowedAndMaximizedModesRemainUnchanged()
        {
            Assert.That(
                DisplaySafetyPolicy.ResolveStartupMode(
                    RuntimePlatform.OSXPlayer,
                    "Intel Core i5",
                    FullScreenMode.Windowed,
                    preferWindowed: false),
                Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(
                DisplaySafetyPolicy.ResolveStartupMode(
                    RuntimePlatform.OSXPlayer,
                    "Intel Core i5",
                    FullScreenMode.MaximizedWindow,
                    preferWindowed: false),
                Is.EqualTo(FullScreenMode.MaximizedWindow));
        }

        [Test]
        public void OtherPlatformsRetainTheirRequestedMode()
        {
            FullScreenMode result = DisplaySafetyPolicy.ResolveStartupMode(
                RuntimePlatform.WindowsPlayer,
                "Intel Core i7",
                FullScreenMode.FullScreenWindow,
                preferWindowed: false);

            Assert.That(result, Is.EqualTo(FullScreenMode.FullScreenWindow));
        }
    }
}
