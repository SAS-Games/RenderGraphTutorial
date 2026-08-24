using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime entry point for world-space shockwaves rendered by DepthShockwaveFeature.
/// </summary>
public static class DepthShockwave
{
    public const int MaximumShaderShockwaves = 8;

    private static readonly List<ShockwaveEvent> activeEvents = new(MaximumShaderShockwaves);

    private readonly struct ShockwaveEvent
    {
        public ShockwaveEvent(Vector3 center, float maxRadius, float duration, double startTime)
        {
            Center = center;
            MaxRadius = maxRadius;
            Duration = duration;
            StartTime = startTime;
        }

        public Vector3 Center { get; }
        public float MaxRadius { get; }
        public float Duration { get; }
        public double StartTime { get; }
    }

    public static void Emit(Vector3 center, float maxRadius = 12f, float duration = 1.35f)
    {
        if (!IsFinite(center) || !float.IsFinite(maxRadius) || !float.IsFinite(duration))
            return;

        maxRadius = Mathf.Max(0.01f, maxRadius);
        duration = Mathf.Max(0.01f, duration);
        RemoveExpired(Time.unscaledTimeAsDouble);

        if (activeEvents.Count >= MaximumShaderShockwaves)
            activeEvents.RemoveAt(0);

        activeEvents.Add(new ShockwaveEvent(
            center,
            maxRadius,
            duration,
            Time.unscaledTimeAsDouble));
    }

    internal static bool HasActiveEvents
    {
        get
        {
            RemoveExpired(Time.unscaledTimeAsDouble);
            return activeEvents.Count > 0;
        }
    }

    internal static int CopyActiveSamples(
        Vector4[] centersAndRadii,
        Vector4[] parameters,
        int maximumCount)
    {
        double now = Time.unscaledTimeAsDouble;
        RemoveExpired(now);

        int count = Mathf.Min(
            Mathf.Min(maximumCount, MaximumShaderShockwaves),
            activeEvents.Count);

        for (int outputIndex = 0; outputIndex < count; outputIndex++)
        {
            ShockwaveEvent shockwave = activeEvents[activeEvents.Count - 1 - outputIndex];
            float age = (float)((now - shockwave.StartTime) / shockwave.Duration);
            float progress = Mathf.Clamp01(age);
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.06f));
            float fadeOut = 1f - Mathf.SmoothStep(0.68f, 1f, progress);
            float radius = shockwave.MaxRadius * progress;

            centersAndRadii[outputIndex] = new Vector4(
                shockwave.Center.x,
                shockwave.Center.y,
                shockwave.Center.z,
                radius);
            parameters[outputIndex] = new Vector4(fadeIn * fadeOut, progress, 0f, 0f);
        }

        return count;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        activeEvents.Clear();
    }

    private static void RemoveExpired(double now)
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            ShockwaveEvent shockwave = activeEvents[i];
            if (now - shockwave.StartTime >= shockwave.Duration)
                activeEvents.RemoveAt(i);
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }
}
