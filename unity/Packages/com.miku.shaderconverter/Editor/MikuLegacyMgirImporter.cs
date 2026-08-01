// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEditor.AssetImporters;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Diagnostic-only guard for retired MIKU files. It intentionally performs
    /// no parsing, migration, shader generation, or material generation.
    /// </summary>
    [ScriptedImporter(1, "mgir")]
    public sealed class MikuLegacyMgirImporter : ScriptedImporter
    {
        const string Message =
            "MIKU_LEGACY_FORMAT: .miku is retired. Install the Miku 1.1 " +
            "Blender extension and re-export a complete .migrbundle folder.";

        public override void OnImportAsset(AssetImportContext context)
        {
            var diagnostic = new TextAsset(Message) { name = "Miku Legacy Format" };
            context.AddObjectToAsset("diagnostic", diagnostic);
            context.SetMainObject(diagnostic);
            context.LogImportError(Message, diagnostic);
            Debug.LogError(Message, diagnostic);
        }
    }
}
