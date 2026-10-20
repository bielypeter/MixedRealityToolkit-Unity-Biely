// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using MixedReality.Toolkit.Input;
using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Helpers for projecting the MRTK eye-gaze ray onto flat UI.
    /// </summary>
    public static class GazeRayUtility
    {
        /// <summary>
        /// Intersects the gaze ray with the plane of <paramref name="target"/> and returns the hit in its local space.
        /// </summary>
        /// <param name="gazeInteractor">Cached interactor; found in the scene when null.</param>
        public static bool TryGetGazePoint(ref GazeInteractor gazeInteractor, RectTransform target, out Vector2 localPoint)
        {
            localPoint = default;

            if (gazeInteractor == null)
            {
                gazeInteractor = Object.FindObjectOfType<GazeInteractor>();
            }
            if (gazeInteractor == null || !gazeInteractor.isActiveAndEnabled)
            {
                return false;
            }

            Transform origin = gazeInteractor.rayOriginTransform != null ? gazeInteractor.rayOriginTransform : gazeInteractor.transform;
            Ray ray = new Ray(origin.position, origin.forward);
            Plane plane = new Plane(target.forward, target.position);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            localPoint = target.InverseTransformPoint(ray.GetPoint(enter));
            return true;
        }

        /// <summary>Frame-rate independent smoothing factor for a time constant in seconds.</summary>
        public static float SmoothingFactor(float timeConstant) =>
            timeConstant > 0f ? 1f - Mathf.Exp(-Time.deltaTime / timeConstant) : 1f;
    }
}
#pragma warning restore CS1591
