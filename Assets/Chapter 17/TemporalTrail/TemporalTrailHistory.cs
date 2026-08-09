using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// URP-owned, per-camera temporal storage. BufferedRTHandleSystem automatically
/// rotates CurrentTexture and PreviousTexture once at the start of each frame.
/// </summary>
internal sealed class TemporalTrailHistory : CameraHistoryItem
{
    private int textureId;
    private Hash128 descriptorHash;
    private bool allocated;
    private bool valid;
    private int lastFrame = -1;
    private float lastTime;
    private float lastCaptureTime;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;

    public RTHandle CurrentTexture => allocated ? GetCurrentFrameRT(textureId) : null;
    public RTHandle PreviousTexture => allocated ? GetPreviousFrameRT(textureId) : null;

    public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
    {
        base.OnCreate(owner, typeId);
        textureId = MakeId(0);
    }

    public override void Reset()
    {
        if (allocated)
            ReleaseHistoryFrameRT(textureId);

        allocated = false;
        descriptorHash = default;
        valid = false;
        lastFrame = -1;
        lastTime = 0f;
        lastCaptureTime = 0f;
        lastCameraPosition = default;
        lastCameraRotation = Quaternion.identity;
    }

    public void Update(ref RenderTextureDescriptor descriptor)
    {
        Hash128 newHash = Hash128.Compute(ref descriptor);
        if (allocated && descriptorHash != newHash)
            Reset();

        if (allocated)
            return;

        AllocHistoryFrameRT(
            textureId,
            2,
            ref descriptor,
            FilterMode.Bilinear,
            "_TemporalTrailHistory");
        descriptorHash = newHash;
        allocated = true;
    }

    public TemporalTrailHistoryFrame Prepare(
        Camera camera,
        TemporalTrailFeature.Settings settings)
    {
        int frame = Time.frameCount;
        float now = Time.realtimeSinceStartup;
        bool skippedFrame = lastFrame >= 0 && frame > lastFrame + 1;
        bool cameraCut = valid &&
            (Vector3.Distance(lastCameraPosition, camera.transform.position) > settings.CameraCutDistance ||
             Quaternion.Angle(lastCameraRotation, camera.transform.rotation) > settings.CameraCutAngle);
        bool canReadHistory = valid && !skippedFrame && !cameraCut;
        float captureInterval = Mathf.Max(0f, settings.CaptureInterval);
        bool captureCurrentFrame = !canReadHistory ||
            captureInterval <= 0f ||
            now - lastCaptureTime >= captureInterval;

        float deltaTime = lastFrame >= 0 ? Mathf.Clamp(now - lastTime, 0f, 0.25f) : 0f;
        float halfLife = Mathf.Max(0.01f, settings.HalfLife);
        float retention = canReadHistory ? Mathf.Pow(0.5f, deltaTime / halfLife) : 0f;

        return new TemporalTrailHistoryFrame(
            this,
            PreviousTexture,
            CurrentTexture,
            canReadHistory,
            captureCurrentFrame,
            retention,
            frame,
            now,
            camera.transform.position,
            camera.transform.rotation);
    }

    internal void Commit(
        int frame,
        float time,
        bool capturedCurrentFrame,
        Vector3 cameraPosition,
        Quaternion cameraRotation)
    {
        valid = true;
        lastFrame = frame;
        lastTime = time;
        if (capturedCurrentFrame)
            lastCaptureTime = time;
        lastCameraPosition = cameraPosition;
        lastCameraRotation = cameraRotation;
    }
}

internal readonly struct TemporalTrailHistoryFrame
{
    private readonly TemporalTrailHistory owner;
    private readonly int frame;
    private readonly float time;
    private readonly bool capturedCurrentFrame;
    private readonly Vector3 cameraPosition;
    private readonly Quaternion cameraRotation;

    public readonly RTHandle Read;
    public readonly RTHandle Write;
    public readonly bool IsValid;
    public readonly bool CaptureCurrentFrame;
    public readonly float Retention;

    public TemporalTrailHistoryFrame(
        TemporalTrailHistory historyOwner,
        RTHandle read,
        RTHandle write,
        bool isValid,
        bool captureCurrentFrame,
        float retention,
        int sourceFrame,
        float sourceTime,
        Vector3 sourceCameraPosition,
        Quaternion sourceCameraRotation)
    {
        owner = historyOwner;
        Read = read;
        Write = write;
        IsValid = isValid;
        CaptureCurrentFrame = captureCurrentFrame;
        Retention = retention;
        frame = sourceFrame;
        time = sourceTime;
        capturedCurrentFrame = captureCurrentFrame;
        cameraPosition = sourceCameraPosition;
        cameraRotation = sourceCameraRotation;
    }

    public void Commit()
    {
        owner?.Commit(frame, time, capturedCurrentFrame, cameraPosition, cameraRotation);
    }
}
