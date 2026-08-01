// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuAtomicAssetWriter
    {
        const int ReplaceAttempts = 7;
        const int InitialReplaceDelayMilliseconds = 25;

        public static bool WriteIfChanged(string absolutePath, string text)
        {
            text = Normalize(text);
            if (File.Exists(absolutePath) &&
                Normalize(File.ReadAllText(absolutePath, Encoding.UTF8)) == text)
                return false;

            var directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "Generated asset path has no parent directory: " + absolutePath);
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(
                directory,
                "." + Path.GetFileName(absolutePath) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            try
            {
                if (File.Exists(absolutePath))
                    ReplaceWithRetry(temporary, absolutePath);
                else
                    File.Move(temporary, absolutePath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            return true;
        }

        static void ReplaceWithRetry(string temporary, string destination)
        {
            var delay = InitialReplaceDelayMilliseconds;
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Replace(temporary, destination, null);
                    return;
                }
                catch (IOException) when (attempt < ReplaceAttempts - 1)
                {
                    Thread.Sleep(delay);
                    delay *= 2;
                }
            }
        }

        static string Normalize(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
