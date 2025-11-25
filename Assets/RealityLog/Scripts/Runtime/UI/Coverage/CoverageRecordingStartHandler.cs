#nullable enable

using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Helper MonoBehaviour that can be targeted by any UnityEvent to reset the
    /// coverage visualization whenever a recording session begins.
    /// </summary>
    public sealed class CoverageRecordingStartHandler : MonoBehaviour
    {
        [SerializeField] private CoverageSphereController? coverageController;
        [SerializeField] private FogSphereController? fogController;

        private void Awake()
        {
            if (coverageController == null)
            {
                coverageController = GetComponentInChildren<CoverageSphereController>(true)
                    ?? GetComponentInParent<CoverageSphereController>();
            }

            if (fogController == null)
            {
                fogController = GetComponentInChildren<FogSphereController>(true)
                    ?? GetComponentInParent<FogSphereController>();
            }
        }

        public void OnRecordingStarted()
        {
            var hasTarget = false;

            if (coverageController != null)
            {
                coverageController.ResetCoverage();
                hasTarget = true;
            }

            if (fogController != null)
            {
                fogController.ResetFog();
                hasTarget = true;
            }

            if (!hasTarget)
            {
                Debug.LogWarning($"{nameof(CoverageRecordingStartHandler)} has no coverage targets assigned.", this);
            }
        }
    }
}

