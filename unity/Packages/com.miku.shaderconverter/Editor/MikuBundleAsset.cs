// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>Diagnostic main asset for a sealed Miku bundle import.</summary>
    public sealed class MikuBundleAsset : ScriptableObject
    {
        public string documentKind = "";
        public string schemaVersion = "";
        public string bundleHash = "";
        public string materialName = "";
        public string workflow = "";
        public string outputRoot = "";
        public string status = "";
        public string receiptPath = "";
        public List<string> dependencies = new List<string>();
        public List<string> diagnostics = new List<string>();
    }
}
