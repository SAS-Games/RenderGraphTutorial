using UnityEngine;

namespace SAS.RenderDebugging
{
    /// <summary>References previewable pixels published for a stage in one frame.</summary>
    public readonly struct RenderDebugTextureData
    {
        public RenderDebugTextureData(
            string sourceId,
            string stageId,
            Texture texture,
            RenderDebugTextureMetadata metadata,
            int frameIndex,
            int cameraInstanceId,
            string cameraName,
            bool isCaptured)
        {
            SourceId = sourceId;
            StageId = stageId;
            Texture = texture;
            Metadata = metadata;
            FrameIndex = frameIndex;
            CameraInstanceId = cameraInstanceId;
            CameraName = cameraName ?? string.Empty;
            IsCaptured = isCaptured;
        }

        public string SourceId { get; }
        public string StageId { get; }
        public Texture Texture { get; }
        public RenderDebugTextureMetadata Metadata { get; }
        public int FrameIndex { get; }
        public int CameraInstanceId { get; }
        public string CameraName { get; }
        public bool IsCaptured { get; }
        public bool IsValid => Texture != null;
    }
}
