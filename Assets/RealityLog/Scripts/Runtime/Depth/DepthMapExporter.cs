# nullable enable

using System;
using System.IO;
using RealityLog.Core;
using UnityEngine;
using UnityEngine.Android;

namespace RealityLog.Depth
{
    class DepthMapExporter : MonoBehaviour
    {
        private static readonly string[] descriptorHeader = new[]
            {
                "timestamp_ms", "ovr_timestamp",
                "create_pose_location_x", "create_pose_location_y", "create_pose_location_z",
                "create_pose_rotation_x", "create_pose_rotation_y", "create_pose_rotation_z", "create_pose_rotation_w",
                "fov_left_angle_tangent", "fov_right_angle_tangent", "fov_top_angle_tangent", "fov_down_angle_tangent",
                "near_z", "far_z",
                "width", "height"
            };

        [HideInInspector]
        [SerializeField] private ComputeShader copyDepthMapShader = default!;
        [SerializeField] private string directoryName = "";
        [SerializeField] private string leftDepthMapDirectoryName = "left_depth";
        [SerializeField] private string rightDepthMapDirectoryName = "right_depth";
        [SerializeField] private string leftDepthDescFileName = "left_depth_descriptors.csv";
        [SerializeField] private string rightDepthDescFileName = "right_depth_descriptors.csv";

        [SerializeField] private FrameRateSettings? frameRateSettings;

        [Header("Frame Rate Limiter")]
        [SerializeField] private bool limitDepthExportFrameRate = true;
        [SerializeField, Min(0.1f)] private float targetDepthExportFps = 15f;

        private DepthDataExtractor? depthDataExtractor;

        private DepthRenderTextureExporter? renderTextureExporter;
        private CsvWriter? leftDepthCsvWriter;
        private CsvWriter? rightDepthCsvWriter;

        private double baseOvrTimeSec;
        private long baseUnixTimeMs;

        private bool isExporting = false;
        private bool hasScenePermission = false;
        private float nextDepthExportTime;

        public string DirectoryName
        {
            get => directoryName;
            set => directoryName = value;
        }

        public void StartExport()
        {
            isExporting = true;

            leftDepthCsvWriter?.Dispose();
            rightDepthCsvWriter?.Dispose();

            leftDepthCsvWriter = new(Path.Join(Application.persistentDataPath, DirectoryName, leftDepthDescFileName), descriptorHeader);
            rightDepthCsvWriter = new(Path.Join(Application.persistentDataPath, DirectoryName, rightDepthDescFileName), descriptorHeader);

            Directory.CreateDirectory(Path.Join(Application.persistentDataPath, DirectoryName, leftDepthMapDirectoryName));
            Directory.CreateDirectory(Path.Join(Application.persistentDataPath, DirectoryName, rightDepthMapDirectoryName));

            if (hasScenePermission)
            {
                depthDataExtractor?.SetDepthEnabled(true);
            }
        }

        public void StopExport()
        {
            isExporting = false;

            leftDepthCsvWriter?.Dispose();
            leftDepthCsvWriter = null;
            rightDepthCsvWriter?.Dispose();
            rightDepthCsvWriter = null;

            depthDataExtractor?.SetDepthEnabled(false);
        }

        private void Awake()
        {
            ApplyFrameRateSettings();
        }

        private void Start()
        {
            baseOvrTimeSec = OVRPlugin.GetTimeInSeconds();
            baseUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            depthDataExtractor = new();
            renderTextureExporter = new(copyDepthMapShader);

            nextDepthExportTime = Time.unscaledTime;

            Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission);

            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDestroy()
        {
            renderTextureExporter?.Dispose();
            renderTextureExporter = null;

            Application.onBeforeRender -= OnBeforeRender;
        }

        private void OnBeforeRender()
        {
            if (!isExporting ||
                renderTextureExporter == null || depthDataExtractor == null
                || leftDepthCsvWriter == null || rightDepthCsvWriter == null)
            {
                return;
            }

            if (limitDepthExportFrameRate && !IsDepthExportDue())
            {
                return;
            }

            if (!hasScenePermission)
            {
                hasScenePermission = Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission);

                if (hasScenePermission)
                {
                    depthDataExtractor.SetDepthEnabled(isExporting);
                }
                else
                {
                    return;
                }
            }

            if (depthDataExtractor.TryGetUpdatedDepthTexture(out var renderTexture, out var frameDescriptors))
            {
                if (limitDepthExportFrameRate)
                {
                    ScheduleNextDepthExport();
                }
                const int FRAME_DESC_COUNT = 2;

                if (renderTexture == null || !renderTexture.IsCreated())
                {
                    Debug.LogError("RenderTexture is not created or null.");
                    return;
                }

                if (frameDescriptors.Length != FRAME_DESC_COUNT)
                    {
                        Debug.LogError("Expected exactly two depth frame descriptors (left and right).");
                        return;
                    }

                var width = renderTexture.width;
                var height = renderTexture.height;

                var unixTime = ConvertTimestampNsToUnixTimeMs(frameDescriptors[0].timestampNs);

                var leftDepthFilePath = Path.Join(Application.persistentDataPath, DirectoryName, $"{leftDepthMapDirectoryName}/{unixTime}.raw");
                var rightDepthFilePath = Path.Join(Application.persistentDataPath, DirectoryName, $"{rightDepthMapDirectoryName}/{unixTime}.raw");

                renderTextureExporter.Export(renderTexture, leftDepthFilePath, rightDepthFilePath);

                for (var i = 0; i < FRAME_DESC_COUNT; ++i)
                {
                    var frameDesc = frameDescriptors[i];

                    var timestampMs = ConvertTimestampNsToUnixTimeMs(frameDesc.timestampNs);
                    var ovrTimestamp = frameDesc.timestampNs / 1.0e9;

                    var row = new double[]
                    {
                        timestampMs,
                        ovrTimestamp,
                        frameDesc.createPoseLocation.x, frameDesc.createPoseLocation.y, frameDesc.createPoseLocation.z,
                        frameDesc.createPoseRotation.x, frameDesc.createPoseRotation.y, frameDesc.createPoseRotation.z, frameDesc.createPoseRotation.w,
                        frameDesc.fovLeftAngleTangent, frameDesc.fovRightAngleTangent,
                        frameDesc.fovTopAngleTangent, frameDesc.fovDownAngleTangent,
                        frameDesc.nearZ, frameDesc.farZ,
                        width, height
                    };

                    if (i == 0)
                    {
                        leftDepthCsvWriter?.EnqueueRow(row);
                    }
                    else
                    {
                        rightDepthCsvWriter?.EnqueueRow(row);
                    }
                }
            }
        }

        private long ConvertTimestampNsToUnixTimeMs(long timestampNs)
        {
            var deltaMs = (long) (timestampNs / 1.0e6 - baseOvrTimeSec * 1000.0);
            return baseUnixTimeMs + deltaMs;
        }

        private bool IsDepthExportDue()
        {
            var now = Time.unscaledTime;
            if (now < nextDepthExportTime)
            {
                return false;
            }

            return true;
        }

        private void ScheduleNextDepthExport()
        {
            const float MIN_FPS = 0.1f;
            var interval = 1f / Mathf.Max(targetDepthExportFps, MIN_FPS);
            nextDepthExportTime = Time.unscaledTime + interval;
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

            limitDepthExportFrameRate = frameRateSettings.LimitDepthExportFrameRate;
            targetDepthExportFps = frameRateSettings.DepthExportFps;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyFrameRateSettings();

            const string COPY_DEPTH_MAP_SHADER_PATH = "Assets/RealityLog/ComputeShaders/CopyDepthMap.compute";

            if (copyDepthMapShader == null)
            {
                var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(COPY_DEPTH_MAP_SHADER_PATH);
                if (shader == null)
                {
                    Debug.LogError($"Failed to load ComputeShader at path: {COPY_DEPTH_MAP_SHADER_PATH}");
                }
                else
                {
                    copyDepthMapShader = shader;
                    Debug.Log($"Successfully loaded ComputeShader: {COPY_DEPTH_MAP_SHADER_PATH}");
                }
            }
        }
# endif
    }
}