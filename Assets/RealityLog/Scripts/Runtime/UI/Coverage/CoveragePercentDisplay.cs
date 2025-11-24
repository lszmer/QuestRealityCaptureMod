#nullable enable

using TMPro;
using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Updates a TextMeshPro label with the current environment coverage
    /// reported by the CoverageSphereController.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class CoveragePercentDisplay : MonoBehaviour
    {
        [SerializeField] private CoverageSphereController coverageController = default!;
        [SerializeField] private TMP_Text percentageLabel = default!;
        [SerializeField] private string format = "{0:0}%";
        [SerializeField, Min(0.05f)] private float updateIntervalSeconds = 0.1f;

        private float nextUpdateTime;

        private void Awake()
        {
            if (percentageLabel == null)
            {
                percentageLabel = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void OnEnable()
        {
            nextUpdateTime = 0f;
            UpdateLabel();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (percentageLabel == null || coverageController == null)
            {
                return;
            }

            var percent = coverageController.CoveragePercent * 100f;
            percentageLabel.text = string.Format(format, percent);
            nextUpdateTime = Time.unscaledTime + updateIntervalSeconds;
        }
    }
}

