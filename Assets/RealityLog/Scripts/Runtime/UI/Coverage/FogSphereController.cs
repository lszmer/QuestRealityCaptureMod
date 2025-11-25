#nullable enable

using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Controls the large fog sphere that surrounds the player and clears fog
    /// as the user looks around.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FogSphereController : MonoBehaviour
    {
        private static readonly int FogMaskId = Shader.PropertyToID("_MaskTex");

        [Header("References")]
        [SerializeField] private Transform headTransform = default!;
        [SerializeField] private Transform sphereTransform = default!;
        [SerializeField] private Renderer? sphereRenderer = null;
        [SerializeField] private Material fogMaterial = default!;
        [SerializeField] private bool instantiateMaterial = true;
        [SerializeField] private ComputeShader fogMaskCompute = default!;

        [Header("Fog Parameters")]
        [SerializeField, Min(0.5f)] private float sphereRadius = 2.0f;
        [SerializeField] private Vector2Int maskResolution = new(256, 128);
        [SerializeField, Range(1f, 45f)] private float brushAngleDegrees = 15f;
        [SerializeField, Range(0.5f, 30f)] private float brushFeatherDegrees = 5f;
        [SerializeField] private Vector2 viewOffsetDegrees = Vector2.zero;
        [SerializeField, Min(0.01f)] private float maskUpdateInterval = 0.03f;

        private RenderTexture? maskTexture;
        private Material? runtimeMaterial;
        private int kernelId;
        private float nextUpdateTime;

        private void Awake()
        {
            var mainCamera = UnityEngine.Camera.main;

            if (headTransform == null && mainCamera != null)
            {
                headTransform = mainCamera.transform;
            }

            if (sphereTransform == null)
            {
                sphereTransform = transform;
            }

            if (sphereRenderer == null)
            {
                sphereRenderer = sphereTransform.GetComponent<Renderer>();
            }

            if (sphereRenderer == null)
            {
                Debug.LogError("FogSphereController requires a MeshRenderer reference.", this);
                enabled = false;
                return;
            }

            SetupMaterialInstance();

            if (fogMaskCompute == null)
            {
                Debug.LogError("FogSphereController requires a compute shader reference.", this);
                enabled = false;
                return;
            }

            maskResolution = new Vector2Int(
                Mathf.Max(16, maskResolution.x),
                Mathf.Max(8, maskResolution.y));

            maskTexture = new RenderTexture(maskResolution.x, maskResolution.y, 0, RenderTextureFormat.R8)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "FogSphereMask"
            };
            maskTexture.Create();

            kernelId = fogMaskCompute.FindKernel("CSMain");
            fogMaskCompute.SetInts("_TextureSize", maskResolution.x, maskResolution.y);
            fogMaskCompute.SetTexture(kernelId, "Result", maskTexture);

            ApplyMaskToMaterial();
            ResetFog();
            UpdateSphereScale();
        }

        private void LateUpdate()
        {
            if (headTransform == null || maskTexture == null)
            {
                return;
            }

            FollowHeadTransform();

            if (Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            StampCurrentViewDirection();
            nextUpdateTime = Time.unscaledTime + maskUpdateInterval;
        }

        private void FollowHeadTransform()
        {
            sphereTransform.position = headTransform.position;
            // Keep the fog sphere aligned with world axes so it does not rotate with the head.
            sphereTransform.rotation = Quaternion.identity;
        }

        private void UpdateSphereScale()
        {
            if (sphereTransform != null)
            {
                var diameter = sphereRadius * 2f;
                sphereTransform.localScale = Vector3.one * diameter;
            }
        }

        private void StampCurrentViewDirection()
        {
            if (fogMaskCompute == null || maskTexture == null || headTransform == null)
            {
                return;
            }

            var forward = headTransform.forward.normalized;
            forward = ApplyViewOffset(forward);
            forward.x = -forward.x;

            fogMaskCompute.SetFloats("_ForwardDir", forward.x, forward.y, forward.z);

            var outerRad = Mathf.Deg2Rad * Mathf.Max(brushAngleDegrees, 0.1f);
            var featherRad = Mathf.Min(Mathf.Deg2Rad * brushFeatherDegrees, outerRad * 0.95f);
            var innerRad = Mathf.Max(outerRad - featherRad, 0.005f);

            fogMaskCompute.SetFloat("_CosOuterAngle", Mathf.Cos(outerRad));
            fogMaskCompute.SetFloat("_CosInnerAngle", Mathf.Cos(innerRad));

            var groupsX = Mathf.CeilToInt(maskResolution.x / 8f);
            var groupsY = Mathf.CeilToInt(maskResolution.y / 8f);
            fogMaskCompute.Dispatch(kernelId, groupsX, groupsY, 1);
        }

        private Vector3 ApplyViewOffset(Vector3 direction)
        {
            if (viewOffsetDegrees == Vector2.zero)
            {
                return direction;
            }

            var yaw = Quaternion.AngleAxis(viewOffsetDegrees.x, Vector3.up);
            var pitch = Quaternion.AngleAxis(viewOffsetDegrees.y, Vector3.right);
            return yaw * pitch * direction;
        }

        private void SetupMaterialInstance()
        {
            if (sphereRenderer == null)
            {
                return;
            }

            if (fogMaterial == null)
            {
                fogMaterial = instantiateMaterial
                    ? sphereRenderer.material
                    : sphereRenderer.sharedMaterial;
            }
            else if (instantiateMaterial)
            {
                runtimeMaterial = new Material(fogMaterial);
                fogMaterial = runtimeMaterial;
            }

            if (instantiateMaterial)
            {
                sphereRenderer.material = fogMaterial;
            }
            else
            {
                sphereRenderer.sharedMaterial = fogMaterial;
            }
        }

        private void ApplyMaskToMaterial()
        {
            if (fogMaterial != null && maskTexture != null)
            {
                fogMaterial.SetTexture(FogMaskId, maskTexture);
            }
        }

        public void ResetFog()
        {
            if (maskTexture == null)
            {
                return;
            }

            var active = RenderTexture.active;
            RenderTexture.active = maskTexture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;

            nextUpdateTime = 0f;
        }

        private void OnDestroy()
        {
            if (maskTexture != null)
            {
                maskTexture.Release();
                maskTexture = null;
            }

            if (instantiateMaterial && runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sphereRadius = Mathf.Max(0.5f, sphereRadius);
            brushAngleDegrees = Mathf.Clamp(brushAngleDegrees, 1f, 90f);
            brushFeatherDegrees = Mathf.Clamp(brushFeatherDegrees, 0.1f, brushAngleDegrees);
            maskResolution = new Vector2Int(
                Mathf.Max(16, maskResolution.x),
                Mathf.Max(8, maskResolution.y));

            if (sphereTransform != null)
            {
                UpdateSphereScale();
            }
        }
#endif
    }
}

