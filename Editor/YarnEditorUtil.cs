using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ToolkitEngine.Dialogue;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Editor;

#if USE_UNITY_LOCALIZATION
using UnityEditor.Localization;
using UnityEngine.Localization;
#endif

namespace ToolkitEditor.Dialogue
{
	public static class YarnEditorUtil
    {
		#region Fields

		private const string ORDER_TAG = "order:";

		#endregion

		public static IEnumerable<YarnProject> GetYarnProjects() => AssetUtil.GetAssetsOfType<YarnProject>();
        public static IEnumerable<DialogueSpeakerType> GetDialogueSpeakerTypes() => AssetUtil.GetAssetsOfType<DialogueSpeakerType>();

        public static Yarn.Dialogue GetDialogue(YarnProject project)
        {
            var dialogue = new Yarn.Dialogue(new EmptyVariableStorage());
            dialogue.SetProgram(project.Program);

            return dialogue;
		}

        public static YarnProject FindYarnProject(TextAsset script)
        {
            foreach (var project in GetYarnProjects())
            {
                var importer = AssetUtil.LoadImporter<YarnProjectImporter>(project);
                if (importer == null)
                    continue;

                if (importer.ImportData.yarnFiles.Contains(script))
                    return project;
            }

            return null;
        }

		public static TextAsset FindYarnScript(YarnProject project, string nodeName)
		{
			if (!project.NodeExists(nodeName))
				return null;

			var importer = AssetUtil.LoadImporter<YarnProjectImporter>(project);
			if (importer == null)
				return null;

			foreach (var yarnFile in importer.ImportData.yarnFiles)
			{
				foreach (var line in Regex.Split(yarnFile.text, @"\r?\n"))
				{
					var match = Regex.Match(line, @"(?!//).*title:\s*(?<title>\w*)");
					if (match.Success && string.Equals(match.Groups["title"].Value, nodeName, StringComparison.OrdinalIgnoreCase))
					{
						return yarnFile;
					}
				}
			}
			return null;
		}

		public static IEnumerable<YarnProjectTableEntry> GetOrderedEntries(IEnumerable<YarnProject> projects)
		{
			// Collect entries from ALL projects
			List<YarnProjectTableEntry> yarnEntries = new();
			foreach (var project in projects)
			{
				var importer = AssetUtil.LoadImporter<YarnProjectImporter>(project);
				var entries = YarnEditorUtil.GenerateStringsTable(importer);

				// Project doesn't have lines, skip
				if (!entries.Any())
					continue;

				yarnEntries.AddRange(entries.Select(x => new YarnProjectTableEntry()
				{
					project = project,
					entry = x
				}));
			}

			// Sort all entries
			return yarnEntries.Select(x => new
			{
				yarnEntry = x,
				hasOrder = YarnEditorUtil.TryGetOrder(x.project, x.entry, out int act, out int scene, out int beat),
				act,
				scene,
				beat,
			})
			.OrderByDescending(x => x.hasOrder)
			.ThenBy(x => x.act)
			.ThenBy(x => x.scene)
			.ThenBy(x => x.beat)
			.Select(x => x.yarnEntry);
		}

		public static bool TryGetOrder(YarnProject project, StringTableEntry entry, out int act, out int scene, out int beat)
		{
			act = scene = beat = 0;
			var nodeTags = GetDialogue(project)?.GetHeaderValue(entry.Node, "tags")?.Split(" ");
			if (nodeTags == null)
				return false;

			var orderTag = nodeTags.FirstOrDefault(x => x.StartsWith(ORDER_TAG, StringComparison.OrdinalIgnoreCase));
			if (orderTag == null)
				return false;

			string[] s = orderTag.Substring(ORDER_TAG.Length).Split('.');
			if (s.Length != 3)
				return false;

			return int.TryParse(s[0], out act)
				&& int.TryParse(s[1], out scene)
				&& int.TryParse(s[2], out beat);
		}

		public static void AddLineTagsToFilesInYarnProject(YarnProject project)
		{
			if (project == null)
				return;

			AddLineTagsToFilesInYarnProject(AssetUtil.LoadImporter<YarnProjectImporter>(project));
		}

		public static void AddLineTagsToFilesInYarnProject(YarnProjectImporter importer)
		{
			if (importer == null)
				return;

			var methodInfo = typeof(YarnProjectEditor).Assembly.GetType("Yarn.Unity.Editor.YarnProjectUtility")
					.GetMethod("AddLineTagsToFilesInYarnProject", BindingFlags.Static | BindingFlags.NonPublic);
			if (methodInfo != null)
			{
				methodInfo.Invoke(null, new[] { importer });
			}
		}

		public static IEnumerable<StringTableEntry> GenerateStringsTable(YarnProjectImporter importer)
		{
			if (importer == null)
				return null;

			var methodInfo = typeof(YarnProjectEditor).Assembly.GetType("Yarn.Unity.Editor.YarnProjectImporter")
					.GetMethod("GenerateStringsTable", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { }, null);
			if (methodInfo != null)
			{
				return methodInfo.Invoke(importer, null) as IEnumerable<StringTableEntry>;
			}

			return new StringTableEntry[] { };
		}

		public static string GetLocalizedDisplayName(DialogueSpeakerType speakerType)
		{
			if (speakerType == null)
				return speakerType.name;

#if USE_UNITY_LOCALIZATION

			var fieldInfo = typeof(DialogueSpeakerType).GetField("m_displayName", BindingFlags.Instance | BindingFlags.NonPublic);
			if (fieldInfo != null)
			{
				var localizedString = fieldInfo.GetValue(speakerType) as LocalizedString;
				if (localizedString != null)
				{
					return LocalizedStringEditorExt.GetLocalizedStringImmediate(localizedString);
				}
			}
#endif

			return speakerType.displayName;
		}

		#region Audio Methods

		public static async YarnTask<AudioClip> GetPreviewClip(YarnProject project, StringTableEntry entry)
		{
			string localeCode = string.Empty;
			switch (project.localizationType)
			{
				case LocalizationType.YarnInternal:
					return await GetPreviewClip(project, entry, project.baseLocalization);

#if USE_UNITY_LOCALIZATION
				case LocalizationType.Unity:
					localeCode = LocalizationEditorSettings.ActiveLocalizationSettings.GetSelectedLocale()?.Identifier.Code;
					break;
#endif
			}

			return null;
		}

		public static async YarnTask<AudioClip> GetPreviewClip(YarnProject project, StringTableEntry entry, string localeCode)
		{
			switch (project.localizationType)
			{
				case LocalizationType.YarnInternal:
					return await GetPreviewClip(project, entry, project.GetLocalization(localeCode));

#if USE_UNITY_LOCALIZATION
				case LocalizationType.Unity:
					var activeLocalization = LocalizationEditorSettings.ActiveLocalizationSettings;


					//var record = s_assetTableCollections.Select(x => x.GetTable(localeCode) as AssetTable)
					//	.Select(x => x.GetEntry(value.entry.ID))
					//	.Where(x => x != null)
					//	.FirstOrDefault();

					//if (record != null)
					//{
					//	clip = activeLocalization.GetAssetDatabase()?.GetLocalizedAsset<AudioClip>(record.Table.TableCollectionName, record.KeyId);
					//}
					break;
#endif
			}

			return null;
		}

		private static async YarnTask<AudioClip> GetPreviewClip(YarnProject project, StringTableEntry entry, Localization localization)
		{
			if (localization == null)
				return null;

			AudioClip clip = null;
			switch (project.localizationType)
			{
				case LocalizationType.YarnInternal:
					clip = await localization.GetLocalizedObjectAsync<AudioClip>(entry.ID);
					break;

#if USE_UNITY_LOCALIZATION
				case LocalizationType.Unity:
					var activeLocalization = LocalizationEditorSettings.ActiveLocalizationSettings;


					//var record = s_assetTableCollections.Select(x => x.GetTable(localeCode) as AssetTable)
					//	.Select(x => x.GetEntry(value.entry.ID))
					//	.Where(x => x != null)
					//	.FirstOrDefault();

					//if (record != null)
					//{
					//	clip = activeLocalization.GetAssetDatabase()?.GetLocalizedAsset<AudioClip>(record.Table.TableCollectionName, record.KeyId);
					//}
					break;
#endif
			}
			return clip;
		}

		#endregion
	}

	public struct YarnProjectTableEntry
	{
		public YarnProject project;
		public StringTableEntry entry;
	}
}