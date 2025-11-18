# nullable enable

using System;
using System.IO;
using RealityLog.Core;
using UnityEngine;

namespace RealityLog.Camera
{
    public class ImageReaderSurfaceProvider : SurfaceProviderBase
    {
        private const string IMAGE_READER_SURFACE_PROVIDER_CLASS_NAME = "com.t34400.questcamera.io.ImageReaderSurfaceProvider";
        
        private const string SET_SHOULD_SAVE_FRAME_METHOD_NAME = "setShouldSaveFrame";
        private const string CLOSE_METHOD_NAME = "close";

        [SerializeField] private string dataDirectoryName = string.Empty;
        [SerializeField] private string imageSubdirName = "left_camera";
        [SerializeField] private string cameraMetaDataFileName = "left_camera_characteristics.json";
        [SerializeField] private string formatInfoFileName = "left_camera_image_format.json";
        [SerializeField] private int bufferPoolSize = 5;

        [SerializeField] private FrameRateSettings? frameRateSettings;

        [Header("Frame Rate Limiter")]
        [SerializeField] private bool limitCaptureFrameRate = true;
        [SerializeField, Min(0.1f)] private float targetCaptureFps = 15f;

        private AndroidJavaObject? currentInstance;
        private bool captureGateOpen;
        private bool pendingCaptureWindow;
        private float nextCaptureTime;

        public string DataDirectoryName
        {
            get => dataDirectoryName;
            set => dataDirectoryName = value;
        }

        private void Awake()
        {
            ApplyFrameRateSettings();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyFrameRateSettings();
        }
#endif

        public override AndroidJavaObject? GetJavaInstance(CameraMetadata metadata)
        {
            Close();

            var dataDirPath = Path.Join(Application.persistentDataPath, dataDirectoryName);

            var metaDataFilePath = Path.Join(dataDirPath, cameraMetaDataFileName);
            var metaDataJson = JsonUtility.ToJson(metadata);
            try
            {
                File.WriteAllText(metaDataFilePath, metaDataJson);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var imageFileDirPath = Path.Join(dataDirPath, imageSubdirName);
            var formatInfoFilePath = Path.Join(dataDirPath, formatInfoFileName);

            var size = metadata.sensor.pixelArraySize;

            currentInstance = new AndroidJavaObject(
                IMAGE_READER_SURFACE_PROVIDER_CLASS_NAME,
                size.width,
                size.height,
                imageFileDirPath,
                formatInfoFilePath,
                bufferPoolSize
            );

            InitializeCaptureThrottle();

            return currentInstance;
        }

        private void Update()
        {
            if (currentInstance == null || !limitCaptureFrameRate)
            {
                return;
            }

            if (Time.unscaledTime < nextCaptureTime)
            {
                return;
            }

            OpenCaptureGateOnce();

            var interval = CaptureIntervalSeconds();
            nextCaptureTime = Time.unscaledTime + interval;
        }

        private void LateUpdate()
        {
            if (!pendingCaptureWindow || currentInstance == null)
            {
                return;
            }

            ApplyCaptureGate(false);
            pendingCaptureWindow = false;
        }

        private void OnDestroy()
        {
            Close();
        }

        private void Close()
        {
            currentInstance?.Call(CLOSE_METHOD_NAME);
            currentInstance?.Dispose();
            currentInstance = null;
            captureGateOpen = false;
            pendingCaptureWindow = false;
        }

        private void InitializeCaptureThrottle()
        {
            captureGateOpen = false;
            pendingCaptureWindow = false;
            nextCaptureTime = Time.unscaledTime;

            if (limitCaptureFrameRate)
            {
                ApplyCaptureGate(false);
            }
            else
            {
                ApplyCaptureGate(true);
            }
        }

        private float CaptureIntervalSeconds()
        {
            const float MIN_FPS = 0.1f;
            return 1f / Mathf.Max(targetCaptureFps, MIN_FPS);
        }

        private void OpenCaptureGateOnce()
        {
            ApplyCaptureGate(true);
            pendingCaptureWindow = true;
        }

        private void ApplyCaptureGate(bool shouldSaveFrame)
        {
            if (currentInstance == null || captureGateOpen == shouldSaveFrame)
            {
                return;
            }

            currentInstance.Call(SET_SHOULD_SAVE_FRAME_METHOD_NAME, shouldSaveFrame);
            captureGateOpen = shouldSaveFrame;
        }

        private void ApplyFrameRateSettings()
        {
            if (frameRateSettings == null)
            {
                frameRateSettings = GetComponentInParent<FrameRateSettings>();
            }

            if (frameRateSettings == null)
            {
                return;
            }

            limitCaptureFrameRate = frameRateSettings.LimitCameraCaptureFrameRate;
            targetCaptureFps = frameRateSettings.CameraCaptureFps;
        }
    }
}