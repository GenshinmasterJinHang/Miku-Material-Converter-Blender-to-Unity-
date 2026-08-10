// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;

namespace Miku.ShaderConverter.Runtime.Endfield
{
    /// <summary>
    /// Publishes the shared day/night and top-light state used by Miku's
    /// Endfield character shaders. When no controller is enabled, the shaders
    /// retain their legacy lighting path.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class MikuEndfieldLightingController : MonoBehaviour
    {
        static readonly int AvailableId =
            Shader.PropertyToID("_MikuEndfieldLightingAvailable");
        static readonly int DayStrengthId =
            Shader.PropertyToID("_MikuEndfieldDayStrength");
        static readonly int TopLightColorId =
            Shader.PropertyToID("_MikuEndfieldTopLightColor");
        static readonly int TopLightDirectionId =
            Shader.PropertyToID("_MikuEndfieldTopLightDirection");
        static readonly int TopLightParamsId =
            Shader.PropertyToID("_MikuEndfieldTopLightParams");
        static readonly int CameraForwardBlendId =
            Shader.PropertyToID("_MikuEndfieldCameraForwardBlend");
        static readonly int BackLightStrengthId =
            Shader.PropertyToID("_MikuEndfieldBackLightStrength");

        static readonly List<MikuEndfieldLightingController> Instances = new();
        static MikuEndfieldLightingController owner;
        static bool duplicateReported;

        [SerializeField, Range(0f, 1f)]
        float dayStrength = 1f;

        [SerializeField, ColorUsage(true, true)]
        Color topLightColor = new(1f, 0.98f, 0.95f, 1f);

        [SerializeField]
        Vector3 topLightDirection = Vector3.up;

        [SerializeField, Range(0f, 1f)]
        float topLightNormalScale = 0.5f;

        [SerializeField, Range(0f, 1f)]
        float topLightNormalOffset = 0.5f;

        [SerializeField, Min(0f)]
        float dayOneTopStrength = 0.18f;

        [SerializeField, Min(0f)]
        float dayZeroTopStrength = 0.85f;

        [SerializeField, Range(0f, 1f)]
        float cameraForwardBlend = 1f;

        [SerializeField, Range(0f, 2f)]
        float backLightStrength = 1f;

        /// <summary>Gets or sets the day blend, where zero is night and one is day.</summary>
        public float DayStrength
        {
            get => dayStrength;
            set => dayStrength = Mathf.Clamp01(value);
        }

        /// <summary>Gets or sets the linear HDR color of the shared top light.</summary>
        public Color TopLightColor
        {
            get => topLightColor;
            set => topLightColor = value;
        }

        /// <summary>Gets or sets the world-space direction toward the top light.</summary>
        public Vector3 TopLightDirection
        {
            get => topLightDirection;
            set => topLightDirection = value;
        }

        /// <summary>Gets or sets the normal-dot-top scale.</summary>
        public float TopLightNormalScale
        {
            get => topLightNormalScale;
            set => topLightNormalScale = Mathf.Clamp01(value);
        }

        /// <summary>Gets or sets the normal-dot-top offset.</summary>
        public float TopLightNormalOffset
        {
            get => topLightNormalOffset;
            set => topLightNormalOffset = Mathf.Clamp01(value);
        }

        /// <summary>Gets or sets the day-one top-light multiplier.</summary>
        public float DayOneTopStrength
        {
            get => dayOneTopStrength;
            set => dayOneTopStrength = Mathf.Max(value, 0f);
        }

        /// <summary>Gets or sets the day-zero top-light multiplier.</summary>
        public float DayZeroTopStrength
        {
            get => dayZeroTopStrength;
            set => dayZeroTopStrength = Mathf.Max(value, 0f);
        }

        /// <summary>Gets or sets the camera-forward direct-specular blend.</summary>
        public float CameraForwardBlend
        {
            get => cameraForwardBlend;
            set => cameraForwardBlend = Mathf.Clamp01(value);
        }

        /// <summary>Gets or sets the shared camera-relative back-light multiplier.</summary>
        public float BackLightStrength
        {
            get => backLightStrength;
            set => backLightStrength = Mathf.Clamp(value, 0f, 2f);
        }

        void OnEnable()
        {
            Register();
            SelectOwnerAndApply();
        }

        void LateUpdate()
        {
            if (owner == this)
                ApplyGlobals();
        }

        void OnValidate()
        {
            Sanitize();
            if (isActiveAndEnabled)
            {
                Register();
                SelectOwnerAndApply();
            }
        }

        void OnDisable()
        {
            Instances.Remove(this);
            SelectOwnerAndApply();
        }

        /// <summary>Immediately republishes this controller when it owns the scene state.</summary>
        public void Apply()
        {
            Sanitize();
            Register();
            SelectOwnerAndApply();
        }

        void Register()
        {
            Instances.RemoveAll(instance => instance == null);
            if (!Instances.Contains(this))
                Instances.Add(this);
        }

        static void SelectOwnerAndApply()
        {
            Instances.RemoveAll(instance => instance == null || !instance.isActiveAndEnabled);
            owner = null;
            foreach (var instance in Instances)
            {
                if (owner == null || instance.GetInstanceID() < owner.GetInstanceID())
                    owner = instance;
            }

            if (owner != null)
                owner.ApplyGlobals();
            else
                ResetGlobals();

            if (Instances.Count > 1 && !duplicateReported)
            {
                Debug.LogWarning(
                    "MIKU_ENDFIELD_LIGHTING_CONTROLLER_DUPLICATE",
                    owner);
                duplicateReported = true;
            }
            else if (Instances.Count <= 1)
            {
                duplicateReported = false;
            }
        }

        void Sanitize()
        {
            dayStrength = Mathf.Clamp01(FiniteOr(dayStrength, 1f));
            topLightNormalScale = Mathf.Clamp01(FiniteOr(topLightNormalScale, 0.5f));
            topLightNormalOffset = Mathf.Clamp01(FiniteOr(topLightNormalOffset, 0.5f));
            dayOneTopStrength = Mathf.Max(FiniteOr(dayOneTopStrength, 0.18f), 0f);
            dayZeroTopStrength = Mathf.Max(FiniteOr(dayZeroTopStrength, 0.85f), 0f);
            cameraForwardBlend = Mathf.Clamp01(FiniteOr(cameraForwardBlend, 1f));
            backLightStrength = Mathf.Clamp(FiniteOr(backLightStrength, 1f), 0f, 2f);
            topLightDirection = IsFinite(topLightDirection) &&
                topLightDirection.sqrMagnitude > 1e-8f
                ? topLightDirection.normalized
                : Vector3.up;
            if (!IsFinite(topLightColor))
                topLightColor = new Color(1f, 0.98f, 0.95f, 1f);
        }

        void ApplyGlobals()
        {
            Sanitize();
            Shader.SetGlobalFloat(AvailableId, 1f);
            Shader.SetGlobalFloat(DayStrengthId, dayStrength);
            Shader.SetGlobalColor(TopLightColorId, topLightColor);
            Shader.SetGlobalVector(TopLightDirectionId, topLightDirection);
            Shader.SetGlobalVector(
                TopLightParamsId,
                new Vector4(
                    topLightNormalScale,
                    topLightNormalOffset,
                    dayOneTopStrength,
                    dayZeroTopStrength));
            Shader.SetGlobalFloat(CameraForwardBlendId, cameraForwardBlend);
            Shader.SetGlobalFloat(BackLightStrengthId, backLightStrength);
        }

        static void ResetGlobals()
        {
            Shader.SetGlobalFloat(AvailableId, 0f);
            Shader.SetGlobalFloat(DayStrengthId, 1f);
            Shader.SetGlobalColor(
                TopLightColorId,
                new Color(1f, 0.98f, 0.95f, 1f));
            Shader.SetGlobalVector(TopLightDirectionId, Vector3.up);
            Shader.SetGlobalVector(
                TopLightParamsId,
                new Vector4(0.5f, 0.5f, 0.18f, 0.85f));
            Shader.SetGlobalFloat(CameraForwardBlendId, 1f);
            Shader.SetGlobalFloat(BackLightStrengthId, 1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instances.Clear();
            owner = null;
            duplicateReported = false;
            ResetGlobals();
        }

        static float FiniteOr(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        static bool IsFinite(Vector3 value) =>
            !(float.IsNaN(value.x) || float.IsInfinity(value.x) ||
              float.IsNaN(value.y) || float.IsInfinity(value.y) ||
              float.IsNaN(value.z) || float.IsInfinity(value.z));

        static bool IsFinite(Color value) =>
            !(float.IsNaN(value.r) || float.IsInfinity(value.r) ||
              float.IsNaN(value.g) || float.IsInfinity(value.g) ||
              float.IsNaN(value.b) || float.IsInfinity(value.b) ||
              float.IsNaN(value.a) || float.IsInfinity(value.a));
    }
}
