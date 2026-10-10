using System;
using Xunit;

namespace UnityEngine.Rendering
{
    /// <summary>Stands in for the engine type, which the plugin finds by this name.</summary>
    public static class SplashScreen
    {
        public enum StopBehavior
        {
            StopImmediate = 1,
            FadeOut = 2,
        }

        public static StopBehavior? Stopped { get; set; }

        public static void Stop(StopBehavior behavior) => Stopped = behavior;
    }
}

namespace MortarBepInExBridge.Tests
{
    public class SplashStopTests
    {
        [Fact]
        public void TheEnginesSplashIsStoppedAtOnce()
        {
            UnityEngine.Rendering.SplashScreen.Stopped = null;
            Action? stop = IntroSkip.SplashStop(name => Type.GetType(name));
            Assert.NotNull(stop);
            stop();
            Assert.Equal(UnityEngine.Rendering.SplashScreen.StopBehavior.StopImmediate, UnityEngine.Rendering.SplashScreen.Stopped);
        }

        [Fact]
        public void AUnityWithNoSplashCallIsLeftAlone() =>
            Assert.Null(IntroSkip.SplashStop(_ => null));
    }
}
