// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Miku.ShaderConverter.Runtime.NPR
{
    /// <summary>
    /// Sends an animated head-bone basis to Miku face-SDF materials.
    /// Static FBX previews keep working without this optional component.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MikuFaceSdfHeadBinder : MonoBehaviour
    {
        static readonly int HeadForwardId = Shader.PropertyToID("_MikuHeadForwardWS");
        static readonly int HeadRightId = Shader.PropertyToID("_MikuHeadRightWS");
        static readonly int HeadUpId = Shader.PropertyToID("_MikuHeadUpWS");
        static readonly int HeadAxesValidId = Shader.PropertyToID("_MikuHeadAxesValid");

        public Transform headBone;
        public Vector3 localForward = Vector3.forward;
        public Vector3 localUp = Vector3.up;
        public bool invertRight;
        public Renderer[] targetRenderers;

        MaterialPropertyBlock propertyBlock;

        void OnEnable() { Apply(); }
        void LateUpdate() { Apply(); }
        void OnValidate() { Apply(); }
        void OnDisable() { SetValidity(0f); }

        public void Apply()
        {
            if (headBone == null || localForward.sqrMagnitude < 1e-6f || localUp.sqrMagnitude < 1e-6f)
            {
                SetValidity(0f);
                return;
            }

            var forward = headBone.TransformDirection(localForward).normalized;
            var upSeed = headBone.TransformDirection(localUp).normalized;
            var right = Vector3.Cross(upSeed, forward).normalized;
            if (invertRight)
                right = -right;
            var up = Vector3.Cross(forward, right).normalized;
            if (right.sqrMagnitude < 1e-6f || up.sqrMagnitude < 1e-6f)
            {
                SetValidity(0f);
                return;
            }

            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            foreach (var target in ResolveRenderers())
            {
                if (target == null)
                    continue;
                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(HeadForwardId, new Vector4(forward.x, forward.y, forward.z, 0f));
                propertyBlock.SetVector(HeadRightId, new Vector4(right.x, right.y, right.z, 0f));
                propertyBlock.SetVector(HeadUpId, new Vector4(up.x, up.y, up.z, 0f));
                propertyBlock.SetFloat(HeadAxesValidId, 1f);
                target.SetPropertyBlock(propertyBlock);
            }
        }

        Renderer[] ResolveRenderers()
        {
            return targetRenderers != null && targetRenderers.Length > 0
                ? targetRenderers
                : GetComponentsInChildren<Renderer>(true);
        }

        void SetValidity(float value)
        {
            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            foreach (var target in ResolveRenderers())
            {
                if (target == null)
                    continue;
                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(HeadAxesValidId, value);
                target.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
