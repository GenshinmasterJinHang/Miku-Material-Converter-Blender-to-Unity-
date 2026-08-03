// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Miku.ShaderConverter.Editor;

namespace Miku.ShaderConverter.Tests.Editor
{
    public sealed class MikuEditorLocalizationTests
    {
        const string PreferenceKey =
            "com.miku.shaderconverter.editorLanguage";

        string originalValue;
        bool hadOriginalValue;

        [SetUp]
        public void SaveLanguagePreference()
        {
            hadOriginalValue = EditorPrefs.HasKey(PreferenceKey);
            originalValue = EditorPrefs.GetString(PreferenceKey, null);
            EditorPrefs.DeleteKey(PreferenceKey);
        }

        [TearDown]
        public void RestoreLanguagePreference()
        {
            if (hadOriginalValue)
                EditorPrefs.SetString(PreferenceKey, originalValue);
            else
                EditorPrefs.DeleteKey(PreferenceKey);
        }

        [Test]
        public void DefaultsToEnglishAndFallsBackFromInvalidValue()
        {
            Assert.That(MikuEditorLocalization.Language, Is.EqualTo("en_US"));
            EditorPrefs.SetString(PreferenceKey, "fr_FR");
            Assert.That(MikuEditorLocalization.Language, Is.EqualTo("en_US"));
            Assert.That(MikuEditorLocalization.Tr("Miku Settings"),
                Is.EqualTo("Miku Settings"));
        }

        [Test]
        public void SimplifiedChineseIsIndependentAndPersistsPerUser()
        {
            MikuEditorLocalization.SetLanguage("zh_HANS");
            Assert.That(MikuEditorLocalization.Language, Is.EqualTo("zh_HANS"));
            Assert.That(MikuEditorLocalization.Tr("Miku Settings"),
                Is.EqualTo("Miku 设置"));
            Assert.That(MikuEditorLocalization.Tr("Language"),
                Is.EqualTo("语言"));
            Assert.That(EditorPrefs.GetString(PreferenceKey),
                Is.EqualTo("zh_HANS"));

            MikuEditorLocalization.SetLanguage("en_US");
            Assert.That(MikuEditorLocalization.Tr("Miku Settings"),
                Is.EqualTo("Miku Settings"));
        }

        [Test]
        public void InvalidLanguageInputResetsToEnglish()
        {
            MikuEditorLocalization.SetLanguage("zh_HANS");
            MikuEditorLocalization.SetLanguage("invalid");
            Assert.That(MikuEditorLocalization.Language, Is.EqualTo("en_US"));
        }

        [Test]
        public void FormatPreservesTranslatedPlaceholders()
        {
            MikuEditorLocalization.SetLanguage("zh_HANS");
            var text = MikuEditorLocalization.Format(
                "Screen Rim Renderer Feature: {0}/{1} active Renderer Data assets installed. Use Miku > Game Toon > Rendering > Screen Rim Installer for explicit Preview/Apply.",
                1,
                2);
            Assert.That(text, Does.Contain("1/2"));
            Assert.That(text, Does.Contain("Renderer Data"));
        }

        [Test]
        public void TranslationDirectoryHasStableVisibleMessageIds()
        {
            MikuEditorLocalization.SetLanguage("zh_HANS");
            foreach (var messageId in MikuEditorLocalization.KnownMessageIds)
            {
                Assert.That(messageId, Is.Not.Null.And.Not.Empty);
                Assert.That(MikuEditorLocalization.Tr(messageId),
                    Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void SettingsMenuAndUserPreferencesProviderAreRegistered()
        {
            var menuFound = false;
            foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (var attribute in method.GetCustomAttributes(
                             typeof(MenuItem), false))
                {
                    if (((MenuItem)attribute).menuItem == "Miku/Settings")
                        menuFound = true;
                }
            }
            Assert.That(menuFound, Is.True);
            var providerFound = typeof(MikuEditorLocalization)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Any(method => method.GetCustomAttribute<SettingsProviderAttribute>() != null);
            Assert.That(providerFound, Is.True);
        }
    }
}
