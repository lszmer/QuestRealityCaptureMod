# nullable enable

using System;
using System.IO;
using RealityLog.Core;
using UnityEngine;

namespace RealityLog.OVR
{
    public enum PoseStateMode
    {
        Immediate,
        Raw
    }

    class PoseLogger : MonoBehaviour
    {
        private static readonly string[] HEADER = new string[]
            {
                "unix_time", "ovr_timestamp",
                "pos_x", "pos_y", "pos_z", 
                "rot_x", "rot_y", "rot_z", "rot_w", 
            };

        [SerializeField] private OVRPlugin.Node node = OVRPlugin.Node.Head;
        [SerializeField] private PoseStateMode mode = PoseStateMode.Immediate;
        [SerializeField] private string fileName = "poses.csv";
        [SerializeField] private string directoryName = "";
        [SerializeField] private bool startLoggingOnStart = false;
        [Header("Optional")]
        [SerializeField] private Transform trackingSpace = default!;

        [SerializeField] private FrameRateSettings? frameRateSettings;

        [Header("Frame Rate Limiter")]
        [SerializeField] private bool limitPoseLoggingFrameRate = true;
        [SerializeField, Min(0.1f)] private float targetPoseLoggingFps = 15f;

        private CsvWriter? writer = null;

        private double baseOvrTimeSec;
        private long baseUnixTimeMs;

        private double latestTimestamp;
        private float nextPoseLogTime;

        public string DirectoryName
        {
            get => directoryName;
            set => directoryName = value;
        }

        public void StartLogging()
        {
            try
            {
                StopLogging();
                var filePath = Path.Combine(Application.persistentDataPath, DirectoryName, fileName);
                writer = new CsvWriter(filePath, HEADER);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] Failed to create CsvWriter: {ex.Message}");
                writer = null;
            }
        }

        public void StopLogging()
        {
            try
            {
                writer?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] Failed to dispose CsvWriter: {ex.Message}");
            }

            writer = null;
        }

        private void Awake()
        {
            ApplyFrameRateSettings();
        }

        private void Start()
        {
            baseOvrTimeSec = OVRPlugin.GetTimeInSeconds();
            baseUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            nextPoseLogTime = Time.unscaledTime;

            Debug.Log($"[Time Log] Base OVR Time (sec): {baseOvrTimeSec}, Base Unix Time (ms): {baseUnixTimeMs}");

            if (startLoggingOnStart)
            {
                StartLogging();
            }
        }

        private void FixedUpdate()
        {
            if (writer == null)
                return;

            EnqueueRowIfNeeded(writer);
        }

        private void EnqueueRowIfNeeded(CsvWriter writer)
        {
            var poseState = mode switch 
                {
                    PoseStateMode.Immediate => OVRPlugin.GetNodePoseStateImmediate(node),
                    PoseStateMode.Raw => OVRPlugin.GetNodePoseStateRaw(node, OVRPlugin.Step.Render),
                    _ => OVRPlugin.PoseStatef.identity,
                };

            var timestamp = poseState.Time;

            if (timestamp <= latestTimestamp)
            {
                return;
            }

            if (limitPoseLoggingFrameRate && !IsPoseLogDue())
            {
                return;
            }

            var pose = poseState.Pose.ToOVRPose();

            var position = pose.position;
            var orientation = pose.orientation;

            if (trackingSpace != null)
            {
                position = trackingSpace.TransformPoint(position);
                orientation = trackingSpace.rotation * orientation;
            }

            writer.EnqueueRow(
                ConvertOvrSecToUnixTimeMs(timestamp), timestamp,
                position.x, position.y, position.z,
                orientation.x, orientation.y, orientation.z, orientation.w
            );

            latestTimestamp = timestamp;

            if (limitPoseLoggingFrameRate)
            {
                ScheduleNextPoseLog();
            }
        }

        private long ConvertOvrSecToUnixTimeMs(double ovrTime)
        {
            var deltaSec = ovrTime - baseOvrTimeSec;
            var deltaMs = (long) (deltaSec * 1000.0);
            return baseUnixTimeMs + deltaMs;
        }

        private void OnDestroy()
        {
            writer?.Dispose();
            writer = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyFrameRateSettings();
        }
#endif

        private bool IsPoseLogDue()
        {
            return Time.unscaledTime >= nextPoseLogTime;
        }

        private void ScheduleNextPoseLog()
        {
            const float MIN_FPS = 0.1f;
            var interval = 1f / Mathf.Max(targetPoseLoggingFps, MIN_FPS);
            nextPoseLogTime = Time.unscaledTime + interval;
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

            limitPoseLoggingFrameRate = frameRateSettings.LimitPoseLoggingFrameRate;
            targetPoseLoggingFps = frameRateSettings.PoseLoggingFps;
        }
    }
}