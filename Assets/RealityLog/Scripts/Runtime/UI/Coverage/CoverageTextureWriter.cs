#nullable enable

using System;
using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Manages a single-channel texture that accumulates visited gaze
    /// directions using a configurable spherical brush.
    /// </summary>
    public sealed class CoverageTextureWriter : IDisposable
    {
        private readonly int width;
        private readonly int height;
        private readonly int brushLonPixels;
        private readonly int brushLatPixels;
        private readonly Color32[] pixels;
        private int visitedPixels;

        private bool dirty;

        public CoverageTextureWriter(int width, int height, float brushAngleDegrees)
        {
            this.width = Mathf.Max(8, width);
            this.height = Mathf.Max(4, height);

            var brush = Mathf.Max(0.5f, brushAngleDegrees);
            brushLonPixels = Mathf.Max(1, Mathf.RoundToInt((brush / 360f) * this.width));
            brushLatPixels = Mathf.Max(1, Mathf.RoundToInt((brush / 180f) * this.height));

            pixels = new Color32[this.width * this.height];

            Texture = new Texture2D(this.width, this.height, TextureFormat.R8, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "HeadCoverageMask"
            };

            Clear();
        }

        public Texture2D Texture { get; }

        public void Clear()
        {
            var clearColor = new Color32(0, 0, 0, 255);
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clearColor;
            }

            visitedPixels = 0;
            dirty = true;
        }

        public bool Stamp(Vector2 normalizedUv)
        {
            var x = Mathf.FloorToInt(normalizedUv.x * (width - 1));
            var y = Mathf.FloorToInt(normalizedUv.y * (height - 1));
            return Stamp(x, y);
        }

        public bool Stamp(int centerX, int centerY)
        {
            var changed = false;

            for (var dy = -brushLatPixels; dy <= brushLatPixels; dy++)
            {
                var y = Mathf.Clamp(centerY + dy, 0, height - 1);

                for (var dx = -brushLonPixels; dx <= brushLonPixels; dx++)
                {
                    var x = Mathf.Clamp(centerX + dx, 0, width - 1);
                    var index = y * width + x;

                    if (pixels[index].r == byte.MaxValue)
                    {
                        continue;
                    }

                    pixels[index].r = byte.MaxValue;
                    visitedPixels++;
                    changed = true;
                }
            }

            if (changed)
            {
                dirty = true;
            }

            return changed;
        }

        public bool ApplyIfNeeded()
        {
            if (!dirty)
            {
                return false;
            }

            Texture.SetPixels32(pixels);
            Texture.Apply(false, false);
            dirty = false;

            return true;
        }

        public float CoverageRatio => pixels.Length == 0 ? 0f : Mathf.Clamp01((float)visitedPixels / pixels.Length);

        public void Dispose()
        {
            if (Texture != null)
            {
                UnityEngine.Object.Destroy(Texture);
            }
        }
    }
}

