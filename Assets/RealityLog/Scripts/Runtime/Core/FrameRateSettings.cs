# nullable enable

using UnityEngine;

namespace RealityLog.Core
{
    /// <summary>
    /// Centralized container for capture/export/logging frame-rate limits.
    /// Place this component on the root RealityLog GameObject so child components
    /// can inherit the configured values automatically.
    /// </summary>
    public class FrameRateSettings : MonoBehaviour
    {
        private const float MIN_FPS = 0.1f;

        [Header("Camera Capture (YUV)")]
        [SerializeField] private bool limitCameraCaptureFrameRate = true;
        [SerializeField, Min(MIN_FPS)] private float cameraCaptureFps = 15f;

        [Header("Depth Export")]
        [SerializeField] private bool limitDepthExportFrameRate = true;
        [SerializeField, Min(MIN_FPS)] private float depthExportFps = 15f;

        [Header("Pose Logging")]
        [SerializeField] private bool limitPoseLoggingFrameRate = true;
        [SerializeField, Min(MIN_FPS)] private float poseLoggingFps = 15f;

        public bool LimitCameraCaptureFrameRate => limitCameraCaptureFrameRate;
        public float CameraCaptureFps => Mathf.Max(cameraCaptureFps, MIN_FPS);

        public bool LimitDepthExportFrameRate => limitDepthExportFrameRate;
        public float DepthExportFps => Mathf.Max(depthExportFps, MIN_FPS);

        public bool LimitPoseLoggingFrameRate => limitPoseLoggingFrameRate;
        public float PoseLoggingFps => Mathf.Max(poseLoggingFps, MIN_FPS);
    }
}

