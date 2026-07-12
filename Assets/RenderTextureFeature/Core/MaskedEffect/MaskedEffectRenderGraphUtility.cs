using System;
using UnityEngine;
using UnityEngine.Rendering;

public static class MaskedEffectRenderGraphUtility
{
    public static ProfilingSampler GetOrCreateProfilingSampler(
        string profilingName,
        ref string cachedProfilingName,
        ProfilingSampler profilingSampler)
    {
        profilingName ??= string.Empty;
        if (profilingSampler != null &&
            string.Equals(cachedProfilingName, profilingName, StringComparison.Ordinal))
        {
            return profilingSampler;
        }

        cachedProfilingName = profilingName;
        return new ProfilingSampler(profilingName);
    }

    public static Vector4 CreateTexelSize(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        return new Vector4(1.0f / width, 1.0f / height, width, height);
    }
}
