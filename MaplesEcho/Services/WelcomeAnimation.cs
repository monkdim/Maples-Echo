using System;
using System.Numerics;

namespace MaplesEcho.Services;

internal static class WelcomeAnimation
{
    internal const string Resource = "MaplesEcho.Assets.maple-viera-motion.png";

    // Match the approved preview: long neutral holds, 240ms blink, 340ms twitch.
    internal static int FrameAt(double elapsedSeconds, bool reduceMotion)
    {
        if (reduceMotion || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            return 0;
        var t = elapsedSeconds % 12;
        if (t < 2.8) return 0;
        if (t < 2.87) return 1;
        if (t < 2.97) return 2;
        if (t < 3.04) return 1;
        if (t < 6.5) return 0;
        if (t < 6.58) return 3;
        if (t < 6.7) return 4;
        if (t < 6.78) return 5;
        if (t < 6.84) return 3;
        return 0;
    }

    internal static (Vector2 Min, Vector2 Max) UvFor(int frame)
    {
        frame = Math.Clamp(frame, 0, 5);
        var min = new Vector2(frame % 3 / 3f, frame / 3 / 2f);
        return (min, min + new Vector2(1f / 3f, 1f / 2f));
    }
}
