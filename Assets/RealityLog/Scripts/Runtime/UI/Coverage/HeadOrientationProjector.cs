#nullable enable

using UnityEngine;

namespace RealityLog.UI.Coverage
{
    /// <summary>
    /// Converts head orientation into UV coordinates that line up with a
    /// standard equirectangular sphere mesh.
    /// </summary>
    public static class HeadOrientationProjector
    {
        private const float INV_TWO_PI = 1f / (Mathf.PI * 2f);
        private const float INV_PI = 1f / Mathf.PI;

        public static Vector2 ForwardToLatLong(Quaternion orientation)
        {
            var forward = orientation * Vector3.forward;
            return DirectionToLatLong(forward);
        }

        public static Vector2 DirectionToLatLong(Vector3 direction)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return new Vector2(0.5f, 0.5f);
            }

            direction.Normalize();

            var longitude = Mathf.Atan2(direction.x, direction.z);
            var latitude = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));

            var u = (longitude + Mathf.PI) * INV_TWO_PI;
            var v = (latitude + Mathf.PI * 0.5f) * INV_PI;

            return new Vector2(Mathf.Repeat(u, 1f), Mathf.Clamp01(v));
        }
    }
}

