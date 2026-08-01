using System;
using UnityEngine;

namespace Miku.ShaderConverter.Runtime
{
    /// <summary>Generic runtime Property ID API; generated bindings are opt-in.</summary>
    public static class MikuMaterialBinding
    {
        public static int PropertyToId(string referenceName)
        {
            if (string.IsNullOrWhiteSpace(referenceName)) throw new ArgumentException("referenceName is required", nameof(referenceName));
            return Shader.PropertyToID(referenceName);
        }

        public static void SetFloat(Material material, string referenceName, float value)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            material.SetFloat(PropertyToId(referenceName), value);
        }

        public static void SetColor(Material material, string referenceName, Color value)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            material.SetColor(PropertyToId(referenceName), value);
        }
    }
}
