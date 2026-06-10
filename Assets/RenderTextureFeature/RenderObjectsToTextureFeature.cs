using UnityEngine.Rendering.Universal;

public class RenderObjectsToTextureFeature : ScriptableRendererFeature
{
    public string ProfilingName = "Render To Texture";
    public RenderTexturePass.Settings Settings = new();

    private RenderTexturePass  _renderPass;

    public override void Create()
    {
        _renderPass = new RenderTexturePass ();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _renderPass.Setup(ProfilingName, Settings);
        renderer.EnqueuePass(_renderPass);
    }
}