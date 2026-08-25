using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Rendering/Virtual Depth/Tight Mesh God Ray Renderer")]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(VirtualDepthTightMeshSpriteRenderer))]
public sealed class VirtualDepthTightMeshGodRayRenderer : VirtualDepthGodRayRenderer
{
}
