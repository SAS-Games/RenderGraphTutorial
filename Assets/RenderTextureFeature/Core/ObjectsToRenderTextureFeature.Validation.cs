using System;
using System.Collections.Generic;
using UnityEngine;

public partial class ObjectsToRenderTextureFeature
{
    private readonly HashSet<string> _loggedValidationMessages = new();

    private void OnValidate()
    {
        _loggedValidationMessages.Clear();
        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        if (RenderTextureOutputSettings == null)
        {
            return;
        }

        var textureNameOwners = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < RenderTextureOutputSettings.Count; i++)
        {
            RenderTexturePass.Settings settings = RenderTextureOutputSettings[i];
            if (settings == null)
            {
                LogValidationWarning(i, "Settings entry is null and will be skipped.");
                continue;
            }

            ValidateTextureName(settings, i, textureNameOwners);
            ValidateTextureExposure(settings, i);
            ValidateRenderQueue(settings, i);
            ValidateTextureSize(settings, i);
            ValidateShaderPassFilters(settings, i);
            ValidateGlobalKeywords(settings, i);
        }
    }

    private void ValidateTextureName(RenderTexturePass.Settings settings, int index, Dictionary<string, int> textureNameOwners)
    {
        if (string.IsNullOrWhiteSpace(settings.TextureName))
        {
            LogValidationWarning(index, "Texture Name is empty. This output will be skipped.");
            return;
        }

        if (textureNameOwners.TryGetValue(settings.TextureName, out int firstIndex))
        {
            LogValidationWarning(index, $"Texture Name '{settings.TextureName}' is already used by output {firstIndex}. Texture names must be unique because registry and global-property entries use this name as their key.");
            return;
        }

        textureNameOwners.Add(settings.TextureName, index);
    }

    private void ValidateTextureExposure(RenderTexturePass.Settings settings, int index)
    {
        if (!Enum.IsDefined(typeof(RenderTexturePass.Settings.TextureExposureMode), settings.TextureExposure))
        {
            LogValidationWarning(index, $"Texture Exposure contains unknown serialized value {(int)settings.TextureExposure}. The pass will use the backward-compatible global texture behavior.");
        }
    }

    private void ValidateRenderQueue(RenderTexturePass.Settings settings, int index)
    {
        if (settings.RenderQueueLowerBound > settings.RenderQueueUpperBound)
        {
            LogValidationWarning(
                index,
                $"Render Queue Lower Bound ({settings.RenderQueueLowerBound}) is greater than Upper Bound " +
                $"({settings.RenderQueueUpperBound}), so the renderer filter cannot match the intended range.");
        }
    }

    private void ValidateTextureSize(RenderTexturePass.Settings settings, int index)
    {
        if (settings.TextureSizeMode == RenderTexturePass.Settings.SizeMode.Custom &&
            (settings.TextureSize.x <= 0 || settings.TextureSize.y <= 0))
        {
            LogValidationWarning(
                index,
                $"Custom Texture Size is {settings.TextureSize.x}x{settings.TextureSize.y}. " +
                "The pass will clamp each invalid dimension to 1 pixel.");
        }
    }

    private void ValidateShaderPassFilters(RenderTexturePass.Settings settings, int index)
    {
        if (settings.LightMode != RenderTexturePass.Settings.LightModeTags.None || HasCustomShaderTag(settings.ShaderTags))
            return;

        LogValidationWarning(index, "Light Mode is None and Shader Tags contains no valid custom LightMode tag. No shader pass can be selected.");
    }

    private void ValidateGlobalKeywords(RenderTexturePass.Settings settings, int index)
    {
        RenderTexturePass.Settings.GlobalKeyword[] keywords = settings.GlobalShaderKeywords;
        if (keywords == null || keywords.Length == 0)
            return;

        var activeKeywordNames = new HashSet<string>(StringComparer.Ordinal);

        for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
        {
            RenderTexturePass.Settings.GlobalKeyword keyword = keywords[keywordIndex];
            if (keyword.Disabled)
                continue;

            bool hasAction = keyword.BeforeRenderMode != RenderTexturePass.Settings.GlobalKeyword.Mode.None ||
                             keyword.AfterRenderMode != RenderTexturePass.Settings.GlobalKeyword.Mode.None;

            if (!hasAction)
                continue;

            if (string.IsNullOrWhiteSpace(keyword.Name))
            {
                LogValidationWarning(index, $"Global Shader Keywords element {keywordIndex} has an action but no Name, so it will be ignored.");
                continue;
            }

            if (!activeKeywordNames.Add(keyword.Name))
            {
                LogValidationWarning(index, $"Global keyword '{keyword.Name}' appears more than once. Conflicting actions execute in list order.");
            }

            if (keyword.BeforeRenderMode != RenderTexturePass.Settings.GlobalKeyword.Mode.None && keyword.AfterRenderMode == RenderTexturePass.Settings.GlobalKeyword.Mode.None)
            {
                LogValidationWarning(index, $"Global keyword '{keyword.Name}' changes before rendering but has no After Render action. Its changed state can affect later passes and cameras.");
            }
        }

        if (RenderTexturePass.Settings.HasActiveGlobalKeywordChanges(keywords))
        {
            LogValidationWarning(index,
                $"Texture Exposure '{settings.TextureExposure}' does not require command-buffer global state by itself, " +
                "but active global keyword actions still require global-state modification and prevent this pass from being culled.");
        }
    }

    private static bool HasCustomShaderTag(List<string> shaderTags)
    {
        if (shaderTags == null)
            return false;

        foreach (string shaderTag in shaderTags)
        {
            if (!string.IsNullOrWhiteSpace(shaderTag))
            {
                return true;
            }
        }

        return false;
    }

    private void LogValidationWarning(int outputIndex, string message)
    {
        string fullMessage = $"{nameof(ObjectsToRenderTextureFeature)} output {outputIndex}: {message}";
        if (_loggedValidationMessages.Add(fullMessage))
        {
            Debug.LogWarning(fullMessage, this);
        }
    }
}
