using System.Collections.Generic;
using ToolkitEngine.Rendering;
using UnityEngine.UI;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public class PortraitManager : Subsystem<PortraitManager>
    {
		#region Fields

		private Dictionary<DialogueSpeakerType, HashSet<ISpriteDisplay>> m_map = new();

		private const string DEFAULT_KEY = "Default";
		private const string PORTRAIT_META_KEY = "portrait:";

		#endregion

		#region Methods

		public static void Register(Portrait portrait)
		{
			foreach (var speakerType in portrait.speakerTypes)
			{
				if (!CastInstance.m_map.TryGetValue(speakerType, out var set))
				{
					set = new HashSet<ISpriteDisplay>();
					CastInstance.m_map.Add(speakerType, set);
				}

				set.Add(portrait.display);
			}
		}

		public static void Unregister(Portrait portrait)
		{
			foreach (var speakerType in portrait.speakerTypes)
			{
				if (!CastInstance.m_map.TryGetValue(speakerType, out var set))
					continue;

				set.Remove(portrait.display);

				if (set.Count == 0)
				{
					CastInstance.m_map.Remove(speakerType);
				}
			}
		}

		public static void HideAllPortraits()
		{
			foreach (var set in CastInstance.m_map.Values)
			{
				foreach (var image in set)
				{
					image.enabled = false;
				}
			}
		}

		public static void SetPortrait(string speakerName, string portraitKey)
		{
			if (DialogueManager.TryGetDialogueSpeakerTypeByCharacterName(speakerName, out var speakerType)
				&& CastInstance.m_map.TryGetValue(speakerType, out var set))
			{
				SetPortrait(speakerType, portraitKey, set);
			}
		}

		public static void SetPortrait(DialogueSpeakerType speakerType, LocalizedLine line, IPortraitPresenter presenter = null)
		{
			if (speakerType != null && CastInstance.m_map.TryGetValue(speakerType, out var image))
			{
				SetPortrait(speakerType, line, image, presenter);
			}
		}

		private static void SetPortrait(DialogueSpeakerType speakerType, LocalizedLine line, HashSet<ISpriteDisplay> set, IPortraitPresenter presenter)
		{
			foreach (var image in set)
			{
				SetPortrait(speakerType, line, image, presenter);
			}
		}

		private static void SetPortrait(DialogueSpeakerType speakerType, LocalizedLine line, ISpriteDisplay display, IPortraitPresenter presenter)
		{
			if (line != null)
			{
				if ((presenter?.TryGetCustomPortraitKey(speakerType, out string portraitKey) ?? false)
					&& SetPortrait(speakerType, portraitKey, display))
				{
					return;
				}

				for (int i = 0; i < line.Metadata.Length; ++i)
				{
					if (line.Metadata[i].StartsWith(PORTRAIT_META_KEY))
					{
						if (SetPortrait(speakerType, line.Metadata[i].Substring(PORTRAIT_META_KEY.Length), display))
							return;
					}
				}

				if (SetPortrait(speakerType, DEFAULT_KEY, display))
					return;
			}

			SetPortrait(speakerType, portraitKey: null, display);
		}

		private static bool SetPortrait(DialogueSpeakerType speakerType, string portraitKey, HashSet<ISpriteDisplay> set)
		{
			HideAllPortraits();

			bool allEnabled = true;
			foreach (var image in set)
			{
				allEnabled &= SetPortrait(speakerType, portraitKey, image, false);
			}

			return allEnabled;
		}

		private static bool SetPortrait(DialogueSpeakerType speakerType, string portraitKey, ISpriteDisplay display, bool hideAllPortraits = true)
		{
			if (hideAllPortraits)
			{
				HideAllPortraits();
			}

			if (!string.IsNullOrWhiteSpace(portraitKey) && (speakerType?.portraitSet?.TryGetPortrait(portraitKey, out var sprite) ?? false))
			{
				display.sprite = sprite;
				display.enabled = true;
			}
			else
			{
				display.enabled = false;
			}
			return display.enabled;
		}

		#endregion

		#region Yarn Methods

		[YarnCommand("defaultPortrait")]
		public static void CmdSetDefaultPortrait(string speakerName)
		{
			CmdSetPortrait(speakerName, DEFAULT_KEY);
		}

		[YarnCommand("portrait")]
		public static void CmdSetPortrait(string speakerName, string portraitKey)
		{
			SetPortrait(speakerName, portraitKey);
		}

		[YarnCommand("hideAllPortraits")]
		public static void CmdHidePortraits()
		{
			HideAllPortraits();
		}

		#endregion
	}
}