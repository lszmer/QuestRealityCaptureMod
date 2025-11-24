#nullable enable

using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Keeps the coverage sphere pinned to the user's view so it behaves like a
    /// lightweight HUD element in passthrough mode.
    /// </summary>
    public sealed class HeadLockedWidgetAnchor : MonoBehaviour
    {
        [SerializeField] private Transform headTransform = default!;
        [SerializeField] private Vector3 viewSpaceOffset = new(0.22f, -0.18f, 0.55f);
        [SerializeField] private Vector3 viewSpaceEuler = new(-10f, 0f, 0f);
        [SerializeField, Range(0.01f, 20f)] private float followSpeed = 12f;
        [SerializeField, Range(0.01f, 20f)] private float rotateSpeed = 14f;
        [SerializeField] private bool inheritScale = false;

        private void Awake()
        {
            var mainCamera = UnityEngine.Camera.main;

            if (headTransform == null && mainCamera != null)
            {
                headTransform = mainCamera.transform;
            }
        }

        private void LateUpdate()
        {
            if (headTransform == null)
            {
                return;
            }

            var targetPosition = headTransform.TransformPoint(viewSpaceOffset);
            var targetRotation = headTransform.rotation * Quaternion.Euler(viewSpaceEuler);

            var dt = Time.unscaledDeltaTime;
            var posLerp = 1f - Mathf.Exp(-followSpeed * dt);
            var rotLerp = 1f - Mathf.Exp(-rotateSpeed * dt);

            transform.position = Vector3.Lerp(transform.position, targetPosition, posLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotLerp);

            if (inheritScale)
            {
                transform.localScale = headTransform.localScale;
            }
        }
    }
}

