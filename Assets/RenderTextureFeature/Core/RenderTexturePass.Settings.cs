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
        [NonSerialized]
        private LightModeTags _cachedLightMode = (LightModeTags)(-1);

        [NonSerialized]
        private string[] _cachedCustomShaderTags = Array.Empty<string>();

        [NonSerialized]
        private List<ShaderTagId> _cachedLightModeShaderTags;

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

        public enum SizeMode
        {
            Camera,
            Custom,
        }

        public enum DebugMode
        {
            Fullscreen,
            Overlay,
        }

        public enum TextureExposureMode
        {
            [InspectorName("Frame Registry + Global Texture + Texel Size")]
            FrameRegistryAndShaderGlobals = 0,

            [InspectorName("Frame Registry Only")]
            FrameRegistryOnly = 1,

            [InspectorName("Frame Registry + Global Texture")]
            FrameRegistryAndGlobalTexture = 2,
        }

        [Tooltip("Name used to register this output in FrameTextureRegistry and, when Texture Exposure includes Shader Globals, publish it as a global shader texture. Every consumer must use the exact same name.")]
        public string TextureName = "_MyTexture";

        [Tooltip("Controls how this output is exposed. Frame Registry Only is preferred for C# Render Graph consumers. Frame Registry + Global Texture follows Unity's tracked global-texture path without publishing texel size. Frame Registry + Global Texture + Texel Size also publishes <TextureName>_TexelSize and therefore requires global-state modification.")]
        public TextureExposureMode TextureExposure = TextureExposureMode.FrameRegistryAndShaderGlobals;

        [Tooltip("Optional override material used to render matching objects into this output texture. Use a flat mask, id, normal, or custom effect material when another pass will read this texture; leave empty to render objects with their own materials.")]
        public Material Material;

        [Tooltip("Override material pass to render. -1 renders all passes; use 0 or another explicit pass index when the override shader has a dedicated mask/effect pass.")]
        public int MaterialPassIndex = -1; // -1 means render all passes

        [Tooltip("When this output pass runs in the URP frame. Choose an event before the effect that reads Texture Name; AfterRenderingOpaques is a good default for opaque masks.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("URP inputs requested for this pass. Enable Depth, Normal, or Color only when the material or depth behavior needs them; extra inputs can add cost.")]
        public ScriptableRenderPassInput RenderPassInput = ScriptableRenderPassInput.None;

        [Tooltip("Minimum material render queue included in this output. Use 0 for most opaque/mask captures, or raise it to isolate a specific queue range.")]
        [Range(0, 5000)]
        public int RenderQueueLowerBound;

        [Tooltip("Maximum material render queue included in this output. 2499 captures opaque objects; 5000 includes transparent objects too.")]
        [Range(0, 5000)]
        public int RenderQueueUpperBound = 2499;

        [Tooltip("Graphics format used by the output render texture. R8 is recommended for masks because it stores one channel and uses less memory and bandwidth. Use ARGB32 or another multi-channel format only for color, normal, or packed-data outputs.")]
        public RenderTextureFormat ColorFormat = RenderTextureFormat.R8;

        [Tooltip("How the output texture size is chosen. Camera uses the active camera size scaled by Camera Size Multiplier; Custom uses Texture Size.")]
        public SizeMode TextureSizeMode = SizeMode.Camera;

        [Tooltip("Scales the active camera resolution when Texture Size Mode is Camera. 1 matches camera size, 0.5 is half resolution, and 2 is double resolution; lower values are cheaper but less precise. The active camera depth texture can only be attached when the final output size still matches the camera size.")]
        [Range(0.1f, 2.0f)]
        public float CameraSizeMultiplier = 1.0f;

        [Tooltip("Explicit output texture width and height when Texture Size Mode is Custom. Ignored when Texture Size Mode is Camera.")]
        public Vector2Int TextureSize = new(1024, 1024);

        [Tooltip("How the output texture is sampled by later shaders. Point keeps crisp masks; Bilinear softens edges and is useful for blur or soft masks.")]
        public FilterMode FilterMode = FilterMode.Point;

        [Tooltip("Sampling behavior outside the 0-1 UV range. Clamp is safest for screen-space textures because it avoids repeated edge artifacts.")]
        public TextureWrapMode WrapMode = TextureWrapMode.Clamp;

        [Tooltip("Renderer sorting used while drawing objects into the output texture. CommonOpaque is best for opaque masks; CommonTransparent is useful when capturing transparent render queues.")]
        public SortingCriteria SortingCriteria = SortingCriteria.CommonOpaque;

        [Tooltip("Unity GameObject layers included in this output. Use this to choose which scene objects are rendered into the texture.")]
        public LayerMask LayerMask = ~0;

        [Tooltip("URP Rendering Layers included in this output. Use this when effect membership should be controlled independently from Unity GameObject layers.")]
        public RenderingLayerMask RenderLayerMask = RenderingLayerMask.defaultRenderingLayerMask;


        [Tooltip("Built-in shader LightMode tags this output will draw. Standard covers common URP forward and unlit passes; add custom Shader Tags below for custom shaders.")]
        public LightModeTags LightMode = LightModeTags.Standard;

        [Tooltip("Optional global shader keyword changes applied before and after this output renders. Use carefully because global keywords affect shader state outside this pass too.")]
        public GlobalKeyword[] GlobalShaderKeywords;

        [Tooltip("Extra shader LightMode tag names to draw, used for custom shaders that do not use the standard URP LightMode tags.")]
        public List<string> ShaderTags;

        [Tooltip("Enables a depth state override and attaches the active camera depth texture only when this output is exactly camera-sized. Use this when the output should respect scene occlusion; scaled or custom-sized outputs skip camera depth to avoid Render Graph attachment-size mismatches.")]
        public bool Depth;

        [Tooltip("Writes to camera depth when Depth is enabled. Usually disabled for masks and effects so this output does not affect later rendering.")]
        public bool WriteDepth;

        [Tooltip("Depth comparison used when Depth is enabled. LessEqual gives normal visible-surface masking; Always makes the output render through walls.")]
        public CompareFunction DepthCompare = CompareFunction.LessEqual;

        [Header("Debug View")]
        [Tooltip("Draws this output texture back to the camera for setup and debugging. Disable it for normal production use unless you intentionally want the debug overlay.")]
        public bool DebugView;

        [Tooltip("How the debug texture is displayed. Fullscreen replaces the camera view; Overlay tints the texture over the scene using Debug Color.")]
        public DebugMode DebugDisplayMode = DebugMode.Fullscreen;

        [Tooltip("Tint and alpha used when Debug Display Mode is Overlay.")]
        public Color DebugColor = new(0.0f, 1.0f, 0.0f, 0.75f);

        [Tooltip("When the debug view is drawn. AfterRenderingTransparents is useful because it appears on top of the final scene.")]
        public RenderPassEvent DebugRenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Serializable]
        public struct GlobalKeyword
        {
            public enum Mode
            {
                None,
                Enable,
                Disable,
            }

            [Tooltip("Global shader keyword name to change, for example _MY_KEYWORD. Leave this entry disabled if it is not needed.")]
            public string Name;

            [Tooltip("Skips this keyword entry without removing it from the list.")]
            public bool Disabled;

            [Tooltip("Keyword action applied before this output renders. Use Enable or Disable when the mask/effect shader needs a specific global keyword state.")]
            public Mode BeforeRenderMode;

            [Tooltip("Keyword action applied after this output renders. This applies the selected state; it does not remember and restore the state that existed before the pass.")]
            public Mode AfterRenderMode;
        }

        public static bool HasActiveGlobalKeywordChanges(GlobalKeyword[] globalKeywords)
        {
            if (globalKeywords == null)
            {
                return false;
            }

            foreach (GlobalKeyword keyword in globalKeywords)
            {
                if (keyword.Disabled || string.IsNullOrWhiteSpace(keyword.Name))
                {
                    continue;
                }

                if (keyword.BeforeRenderMode != GlobalKeyword.Mode.None ||
                    keyword.AfterRenderMode != GlobalKeyword.Mode.None)
                {
                    return true;
                }
            }

            return false;
        }

        public RenderQueueRange RenderQueueRange => new(RenderQueueLowerBound, RenderQueueUpperBound);

        public List<ShaderTagId> LightModeShaderTags
        {
            get
            {
                if (!IsShaderTagCacheValid())
                {
                    RebuildShaderTagCache();
                }

                return _cachedLightModeShaderTags;
            }
        }

        private bool IsShaderTagCacheValid()
        {
            if (_cachedLightModeShaderTags == null || _cachedLightMode != LightMode)
            {
                return false;
            }

            int customTagCount = ShaderTags?.Count ?? 0;
            if (_cachedCustomShaderTags.Length != customTagCount)
            {
                return false;
            }

            for (int i = 0; i < customTagCount; i++)
            {
                if (!string.Equals(
                        _cachedCustomShaderTags[i],
                        ShaderTags[i],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void RebuildShaderTagCache()
        {
            _cachedLightMode = LightMode;
            _cachedLightModeShaderTags ??= new List<ShaderTagId>();
            _cachedLightModeShaderTags.Clear();

            AddBuiltInTag(LightModeTags.SRPDefaultUnlit, "SRPDefaultUnlit");
            AddBuiltInTag(LightModeTags.UniversalForward, "UniversalForward");
            AddBuiltInTag(LightModeTags.UniversalForwardOnly, "UniversalForwardOnly");
            AddBuiltInTag(LightModeTags.LightweightForward, "LightweightForward");
            AddBuiltInTag(LightModeTags.DepthNormals, "DepthNormals");
            AddBuiltInTag(LightModeTags.DepthNormalsOnly, "DepthNormalsOnly");
            AddBuiltInTag(LightModeTags.DepthOnly, "DepthOnly");

            int customTagCount = ShaderTags?.Count ?? 0;
            _cachedCustomShaderTags = customTagCount == 0
                ? Array.Empty<string>()
                : new string[customTagCount];

            for (int i = 0; i < customTagCount; i++)
            {
                string tag = ShaderTags[i];
                _cachedCustomShaderTags[i] = tag;
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    _cachedLightModeShaderTags.Add(new ShaderTagId(tag));
                }
            }
        }

        private void AddBuiltInTag(LightModeTags tag, string shaderTagName)
        {
            if ((LightMode & tag) != 0)
            {
                _cachedLightModeShaderTags.Add(new ShaderTagId(shaderTagName));
            }
        }
    }
}
