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
                "Game Toon Geometry + Screen Rim Renderer Features: {0}/{1} active Renderer Data assets installed.",
                1,
                2);
            Assert.That(text, Does.Contain("1/2"));
            Assert.That(text, Does.Contain("Renderer Data"));
            Assert.That(text, Does.Contain("游戏卡通"));
            Assert.That(
                MikuEditorLocalization.Tr(
                    "Open Game Toon Renderer Feature Installer"),
                Does.Contain("游戏卡通"));
            Assert.That(
                MikuEditorLocalization.Tr(
                    "Miku Game Toon Renderer Features"),
                Does.Contain("渲染功能"));
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
        public void MaterialDiagnosticHelpBoxesHaveChineseTranslations()
        {
            MikuEditorLocalization.SetLanguage("zh_HANS");
            foreach (var diagnostic in new[]
            {
                MikuWuwaFaceMaterialDiagnostics.SdfRequired,
                MikuWuwaFaceMaterialDiagnostics.SdfStrengthZero,
                MikuWuwaFaceMaterialDiagnostics.BasisInvalid,
                MikuWuwaFaceMaterialDiagnostics.ImportSettingsInvalid,
                MikuWuwaFaceMaterialDiagnostics.ChannelsIdentical,
                MikuWuwaFaceMaterialDiagnostics.TintContrastZero,
                MikuWuwaFaceMaterialDiagnostics.DebugViewActive,
                MikuWuwaFaceMaterialDiagnostics.TransitionTooWide,
                MikuWuwaFaceMaterialDiagnostics.SssMayFlattenShadow,
            })
            {
                var english = MikuWuwaFaceMaterialDiagnostics.Message(
                    diagnostic);
                Assert.That(
                    MikuEditorLocalization.Tr(english),
                    Is.Not.EqualTo(english),
                    diagnostic);
            }

            const string endfieldMatCap =
                "Endfield iris materials require an authored MatCap " +
                "for the tutorial cornea highlight.";
            Assert.That(
                MikuEditorLocalization.Tr(endfieldMatCap),
                Is.Not.EqualTo(endfieldMatCap));
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
