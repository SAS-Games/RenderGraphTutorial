using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public static class RenderingHelpers
{
    private static readonly ShaderTagId[] _shaderTagValues = new ShaderTagId[1];
    private static readonly RenderStateBlock[] _renderStateBlocks = new RenderStateBlock[1];

    // --- Copied from internal method RenderingUtils.CreateRendererListWithRenderStateBlock() ---
    // Create a RendererList using a RenderStateBlock override is quite common so we have this optimized utility function for it
    public static RendererListHandle CreateRendererListWithRenderStateBlock(
        RenderGraph renderGraph,
        ref CullingResults cullResults,
        DrawingSettings drawingSettings,
        FilteringSettings filteringSettings,
        RenderStateBlock renderStateBlock)
    {
        _shaderTagValues[0] = ShaderTagId.none;
        _renderStateBlocks[0] = renderStateBlock;
        var tagValues = new NativeArray<ShaderTagId>(_shaderTagValues, Allocator.Temp);
        var stateBlocks = new NativeArray<RenderStateBlock>(_renderStateBlocks, Allocator.Temp);
        var param = new RendererListParams(cullResults, drawingSettings, filteringSettings)
        {
            tagValues = tagValues, stateBlocks = stateBlocks, isPassTagName = false,
        };
        return renderGraph.CreateRendererList(param);
    }
}