#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Keeps a small rolling window of head pose samples so we can reason about
    /// where the user has looked recently without allocating each frame.
    /// </summary>
    public sealed class HeadPoseHistoryBuffer
    {
        public readonly struct Sample
        {
            public Sample(Quaternion orientation, float timestamp)
            {
                Orientation = orientation;
                Timestamp = timestamp;
            }

            public Quaternion Orientation { get; }
            public float Timestamp { get; }
        }

        private readonly List<Sample> samples;
        private readonly float windowSeconds;

        public HeadPoseHistoryBuffer(float windowSeconds, int initialCapacity = 256)
        {
            this.windowSeconds = Mathf.Max(0f, windowSeconds);
            samples = new List<Sample>(Mathf.Max(1, initialCapacity));
        }

        public IReadOnlyList<Sample> Samples => samples;

        public void AddSample(Quaternion orientation, float timestamp)
        {
            samples.Add(new Sample(orientation, timestamp));

            if (windowSeconds <= 0f)
            {
                return;
            }

            var minTime = timestamp - windowSeconds;
            var firstIndexToKeep = 0;

            while (firstIndexToKeep < samples.Count && samples[firstIndexToKeep].Timestamp < minTime)
            {
                firstIndexToKeep++;
            }

            if (firstIndexToKeep > 0)
            {
                samples.RemoveRange(0, firstIndexToKeep);
            }
        }

        public void Clear() => samples.Clear();
    }
}

