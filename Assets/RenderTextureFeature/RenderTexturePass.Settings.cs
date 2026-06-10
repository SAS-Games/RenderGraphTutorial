using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class RenderTexturePass
{
    [Serializable]
    public class Settings
    {
        [Flags]
        public enum LightModeTags
        {
            None = 0,

            // ReSharper disable once InconsistentNaming
            SRPDefaultUnlit = 1 << 0,
            UniversalForward = 1 << 1,
            UniversalForwardOnly = 1 << 2,
            LightweightForward = 1 << 3,
            DepthNormals = 1 << 4,
            DepthOnly = 1 << 5,
            DepthNormalsOnly = 1 << 6,
            Standard = SRPDefaultUnlit | UniversalForward | UniversalForwardOnly | LightweightForward,
        }

        public Material Material;
        public int MaterialPassIndex = -1; // -1 means render all passes

        // TODO: Add support for doing a blit after rendering the objects to a texture
        // public Material BlitMaterial;
        // public int BlitMaterialPassIndex = -1; // -1 means render all passes

        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public ScriptableRenderPassInput RenderPassInput = ScriptableRenderPassInput.None;

        [Range(0, 5000)]
        public int RenderQueueLowerBound;

        [Range(0, 5000)]
        public int RenderQueueUpperBound = 2499;

        public RenderTextureFormat ColorFormat = RenderTextureFormat.ARGB32;
        public SortingCriteria SortingCriteria = SortingCriteria.CommonOpaque;
        public LayerMask LayerMask = ~0;
        public RenderingLayerMask RenderLayerMask = RenderingLayerMask.defaultRenderingLayerMask;
        public string TextureName = "_MyTexture";

        public LightModeTags LightMode = LightModeTags.Standard;

        public GlobalKeyword[] GlobalShaderKeywords;
        public List<string> ShaderTags;

        public bool Depth;
        public bool WriteDepth;
        public CompareFunction DepthCompare = CompareFunction.LessEqual;

        [Serializable]
        public struct GlobalKeyword
        {
            public enum Mode
            {
                None,
                Enable,
                Disable,
            }

            public string Name;
            public bool Disabled;

            public Mode BeforeRenderMode;
            public Mode AfterRenderMode;
        }

        public RenderQueueRange RenderQueueRange => new(RenderQueueLowerBound, RenderQueueUpperBound);

        public List<ShaderTagId> LightModeShaderTags
        {
            get
            {
                var tags = new List<ShaderTagId>();
                if (LightMode.HasFlag(LightModeTags.SRPDefaultUnlit))
                {
                    tags.Add(new ShaderTagId("SRPDefaultUnlit"));
                }
                if (LightMode.HasFlag(LightModeTags.UniversalForward))
                {
                    tags.Add(new ShaderTagId("UniversalForward"));
                }
                if (LightMode.HasFlag(LightModeTags.UniversalForwardOnly))
                {
                    tags.Add(new ShaderTagId("UniversalForwardOnly"));
                }
                if (LightMode.HasFlag(LightModeTags.LightweightForward))
                {
                    tags.Add(new ShaderTagId("LightweightForward"));
                }
                if (LightMode.HasFlag(LightModeTags.DepthNormals))
                {
                    tags.Add(new ShaderTagId("DepthNormals"));
                }
                if (LightMode.HasFlag(LightModeTags.DepthNormalsOnly))
                {
                    tags.Add(new ShaderTagId("DepthNormalsOnly"));
                }
                if (LightMode.HasFlag(LightModeTags.DepthOnly))
                {
                    tags.Add(new ShaderTagId("DepthOnly"));
                }
                if (ShaderTags != null)
                {
                    foreach (string tag in ShaderTags)
                    {
                        tags.Add(new ShaderTagId(tag));
                    }
                }
                return tags;
            }
        }
    }
}