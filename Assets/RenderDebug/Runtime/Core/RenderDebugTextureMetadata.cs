using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SAS.RenderDebugging
{
    /// <summary>Describes the shape and GPU format of one published frame texture.</summary>
    public readonly struct RenderDebugTextureMetadata
    {
        public RenderDebugTextureMetadata(
            int width,
            int height,
            GraphicsFormat graphicsFormat,
            TextureDimension dimension,
            int volumeDepth,
            int msaaSamples,
            int mipCount)
        {
            Width = width;
            Height = height;
            GraphicsFormat = graphicsFormat;
            Dimension = dimension;
            VolumeDepth = volumeDepth;
            MsaaSamples = msaaSamples;
            MipCount = mipCount;
        }

        public int Width { get; }
        public int Height { get; }
        public GraphicsFormat GraphicsFormat { get; }
        public TextureDimension Dimension { get; }
        public int VolumeDepth { get; }
        public int MsaaSamples { get; }
        public int MipCount { get; }

        /// <summary>Creates metadata from an existing Unity texture without taking ownership.</summary>
        public static RenderDebugTextureMetadata FromTexture(Texture texture)
        {
            if (texture == null)
                return default;

            RenderTexture renderTexture = texture as RenderTexture;
            return new RenderDebugTextureMetadata(
                texture.width,
                texture.height,
                texture.graphicsFormat,
                texture.dimension,
                renderTexture != null ? renderTexture.volumeDepth : 1,
                renderTexture != null ? renderTexture.antiAliasing : 1,
                texture.mipmapCount);
        }

        /// <summary>Creates metadata from a render texture descriptor.</summary>
        public static RenderDebugTextureMetadata FromDescriptor(in RenderTextureDescriptor descriptor)
        {
            return new RenderDebugTextureMetadata(
                descriptor.width,
                descriptor.height,
                descriptor.graphicsFormat,
                descriptor.dimension,
                descriptor.volumeDepth,
                descriptor.msaaSamples,
                descriptor.mipCount);
        }
    }
}
