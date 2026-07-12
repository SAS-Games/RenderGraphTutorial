using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class FrameTextureRegistry : ContextItem
{
    private readonly Dictionary<int, Entry> _textures = new();

    private readonly struct Entry
    {
        public Entry(TextureHandle texture, Vector4 texelSize)
        {
            Texture = texture;
            TexelSize = texelSize;
        }

        public TextureHandle Texture { get; }
        public Vector4 TexelSize { get; }
    }

    public override void Reset()
    {
        _textures.Clear();
    }

    public static FrameTextureRegistry GetOrCreate(ContextContainer frameData)
    {
        return frameData.GetOrCreate<RenderTexturePass.CustomTextureData>();
    }

    public static bool TryGet(
        ContextContainer frameData,
        out FrameTextureRegistry textureRegistry)
    {
        if (frameData.Contains<RenderTexturePass.CustomTextureData>())
        {
            textureRegistry = frameData.Get<RenderTexturePass.CustomTextureData>();
            return true;
        }

        textureRegistry = null;
        return false;
    }

    public virtual void SetTexture(
        int texturePropertyId,
        TextureHandle texture,
        Vector4 texelSize)
    {
        _textures[texturePropertyId] = new Entry(texture, texelSize);
    }

    public bool TryGetTexture(
        int texturePropertyId,
        out TextureHandle texture,
        out Vector4 texelSize)
    {
        if (_textures.TryGetValue(texturePropertyId, out Entry entry))
        {
            texture = entry.Texture;
            texelSize = entry.TexelSize;
            return true;
        }

        texture = TextureHandle.nullHandle;
        texelSize = Vector4.zero;
        return false;
    }
}

public class FrameTextureResolver
{
    private readonly string _ownerName;
    private string _textureName;
    private int _texturePropertyId;
    private bool _loggedMissingMaskData;
    private bool _loggedMissingMaskTexture;

    public FrameTextureResolver(string ownerName)
    {
        _ownerName = ownerName;
    }

    public string TextureName => _textureName;

    public int TexturePropertyId => _texturePropertyId;

    public void SetTextureName(string textureName)
    {
        textureName ??= string.Empty;

        if (_textureName == textureName)
        {
            return;
        }

        _textureName = textureName;
        _texturePropertyId = string.IsNullOrWhiteSpace(_textureName)
            ? 0
            : Shader.PropertyToID(_textureName);
        _loggedMissingMaskData = false;
        _loggedMissingMaskTexture = false;
    }

    public bool TryResolve(
        ContextContainer frameData,
        out TextureHandle maskTexture,
        out Vector4 maskTexelSize)
    {
        maskTexture = TextureHandle.nullHandle;
        maskTexelSize = Vector4.zero;

        if (!FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry textureData))
        {
            LogMissingMaskDataOnce();
            return false;
        }

        if (textureData.TryGetTexture(_texturePropertyId, out maskTexture, out maskTexelSize))
        {
            return true;
        }

        LogMissingMaskTextureOnce();
        return false;
    }

    private void LogMissingMaskDataOnce()
    {
        if (_loggedMissingMaskData)
        {
            return;
        }

        Debug.LogWarning(
            $"{_ownerName} did not find the frame texture registry. " +
            $"Ensure a prior renderer feature publishes a texture named '{_textureName}'.");

        _loggedMissingMaskData = true;
    }

    private void LogMissingMaskTextureOnce()
    {
        if (_loggedMissingMaskTexture)
        {
            return;
        }

        Debug.LogWarning(
            $"{_ownerName} did not find registered texture '{_textureName}'. " +
            $"Check the producer feature order and make sure both features use the same texture name.");

        _loggedMissingMaskTexture = true;
    }
}

public sealed class MaskedTextureResolver : FrameTextureResolver
{
    public MaskedTextureResolver(string ownerName)
        : base(ownerName)
    {
    }
}
