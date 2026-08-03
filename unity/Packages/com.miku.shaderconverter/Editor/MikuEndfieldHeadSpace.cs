// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Editor-side mirror of the Endfield renderer-object-space head basis.
    /// Setup and validation code use this instead of a bone or scene binder.
    /// </summary>
    public static class MikuEndfieldHeadSpace
    {
        /// <summary>Calculates an orthonormal Endfield head basis.</summary>
        public static MikuEndfieldHeadBasis ComputeBasis(Matrix4x4 objectToWorld)
        {
            var rawRight = NormalizeOrFallback(
                objectToWorld.MultiplyVector(Vector3.right),
                Vector3.right);
            var forward = NormalizeOrFallback(
                objectToWorld.MultiplyVector(new Vector3(0f, -1f, 0f)),
                Vector3.forward);
            var upHint = NormalizeOrFallback(
                objectToWorld.MultiplyVector(Vector3.forward),
                Vector3.up);
            var right = NormalizeOrFallback(
                Vector3.Cross(upHint, forward),
                rawRight);
            if (Vector3.Dot(right, rawRight) < 0f)
                right = -right;
            var up = NormalizeOrFallback(
                Vector3.Cross(forward, right),
                upHint);
            forward = NormalizeOrFallback(
                Vector3.Cross(right, up),
                forward);
            return new MikuEndfieldHeadBasis(
                right,
                forward,
                -forward,
                up);
        }

        static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 1e-12f ? value.normalized : fallback;
    }

    /// <summary>Immutable orthonormal head directions in world space.</summary>
    public readonly struct MikuEndfieldHeadBasis
    {
        public MikuEndfieldHeadBasis(
            Vector3 right,
            Vector3 forward,
            Vector3 back,
            Vector3 up)
        {
            Right = right;
            Forward = forward;
            Back = back;
            Up = up;
        }

        public Vector3 Right { get; }
        public Vector3 Forward { get; }
        public Vector3 Back { get; }
        public Vector3 Up { get; }
    }
}
