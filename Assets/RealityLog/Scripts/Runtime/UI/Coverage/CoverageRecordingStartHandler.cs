#nullable enable

using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Helper MonoBehaviour that can be targeted by any UnityEvent to reset the
    /// coverage sphere whenever a recording session begins.
    /// </summary>
    public sealed class CoverageRecordingStartHandler : MonoBehaviour
    {
        [SerializeField] private CoverageSphereController coverageController = default!;

        public void OnRecordingStarted()
        {
            if (coverageController == null)
            {
                Debug.LogWarning($"{nameof(CoverageRecordingStartHandler)} missing CoverageSphereController reference.", this);
                return;
            }

            coverageController.ResetCoverage();
        }
    }
}

