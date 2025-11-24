#nullable enable

using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Bridges the head-pose history, UV projection, and GPU texture so the
    /// coverage sphere material can stay in sync with user movements.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoverageSphereController : MonoBehaviour
    {
        private static readonly int CoverageTexId = Shader.PropertyToID("_CoverageTex");
        private static readonly int CoverageIntensityId = Shader.PropertyToID("_CoverageIntensity");

        [SerializeField] private Transform headPoseSource = default!;
        [SerializeField] private Renderer sphereRenderer = default!;
        [SerializeField] private Vector2Int coverageResolution = new(192, 96);
        [SerializeField, Range(1f, 40f)] private float brushAngleDegrees = 6f;
        [SerializeField, Min(0.02f)] private float uploadIntervalSeconds = 0.1f;
        [SerializeField, Min(0f)] private float historyWindowSeconds = 30f;
        [SerializeField, Range(0f, 1f)] private float coverageIntensity = 1f;
        [SerializeField] private bool instantiateMaterial = true;

        private HeadPoseHistoryBuffer? historyBuffer;
        private CoverageTextureWriter? textureWriter;
        private Material? runtimeMaterial;
        private float nextUploadTime;

        private void Awake()
        {
            var mainCamera = UnityEngine.Camera.main;

            if (headPoseSource == null && mainCamera != null)
            {
                headPoseSource = mainCamera.transform;
            }

            if (sphereRenderer == null)
            {
                sphereRenderer = GetComponentInChildren<Renderer>(true);
            }

            historyBuffer = new HeadPoseHistoryBuffer(historyWindowSeconds);
            textureWriter = new CoverageTextureWriter(
                Mathf.Max(8, coverageResolution.x),
                Mathf.Max(4, coverageResolution.y),
                brushAngleDegrees);

            if (sphereRenderer != null)
            {
                runtimeMaterial = instantiateMaterial ? sphereRenderer.material : sphereRenderer.sharedMaterial;
                runtimeMaterial?.SetFloat(CoverageIntensityId, coverageIntensity);
                runtimeMaterial?.SetTexture(CoverageTexId, textureWriter.Texture);
            }
        }

        private void OnEnable()
        {
            ForceUploadTexture();
        }

        private void Update()
        {
            if (headPoseSource == null || textureWriter == null || historyBuffer == null)
            {
                return;
            }

            var now = Time.unscaledTime;
            var orientation = headPoseSource.rotation;

            historyBuffer.AddSample(orientation, now);

            var uv = HeadOrientationProjector.ForwardToLatLong(orientation);
            textureWriter.Stamp(uv);

            if (now >= nextUploadTime)
            {
                UploadTextureIfNeeded();
            }
        }

        private void UploadTextureIfNeeded()
        {
            if (textureWriter == null)
            {
                return;
            }

            if (textureWriter.ApplyIfNeeded())
            {
                runtimeMaterial?.SetTexture(CoverageTexId, textureWriter.Texture);
            }

            nextUploadTime = Time.unscaledTime + uploadIntervalSeconds;
        }

        private void ForceUploadTexture()
        {
            nextUploadTime = Time.unscaledTime;
            UploadTextureIfNeeded();
        }

        public void ResetCoverage()
        {
            textureWriter?.Clear();
            ForceUploadTexture();
        }

        public float CoveragePercent => textureWriter == null ? 0f : textureWriter.CoverageRatio;

        private void OnDestroy()
        {
            if (instantiateMaterial && runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            textureWriter?.Dispose();
        }
    }
}

