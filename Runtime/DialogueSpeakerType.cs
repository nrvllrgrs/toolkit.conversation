using NaughtyAttributes;
using UnityEngine;

#if USE_UNITY_LOCALIZATION
using UnityEngine.Localization;
#endif

namespace ToolkitEngine.Dialogue
{
	[CreateAssetMenu(menuName = "Toolkit/Dialogue/Dialogue Speaker", order = 401)]
	public class DialogueSpeakerType : ScriptableObject
    {
		#region Fields

#if USE_UNITY_LOCALIZATION
		[SerializeField, Tooltip("Name of speaking character.")]
		private LocalizedString m_displayName;
#else
		[SerializeField]
		private string m_displayName;
#endif

		[SerializeField]
		private Color m_color = Color.white;

		[Space]

		[SerializeField]
		private AnimationSet m_animationSet;

		[SerializeField]
		private PortraitSet m_portraitSet;

		[SerializeField]
		private GibberishType m_gibberishType;

#if UNITY_EDITOR

		[Header("Editor Only")]

		[SerializeField, Label("TTS Voice")]
		private TTSVoice m_ttsVoice;
#endif

		#endregion

		#region Properties

		public string displayName
		{
			get
			{
#if USE_UNITY_LOCALIZATION
				try
				{
					return m_displayName.GetLocalizedString();
				}
				catch { }
#else
				if (!string.IsNullOrWhiteSpace(m_displayName))
				{
					return m_displayName;
				}
#endif
				return name;
			}
		}

		public Color color => m_color;
		public AnimationSet animationSet => m_animationSet;
		public PortraitSet portraitSet => m_portraitSet;
		public GibberishType gibberishType => m_gibberishType;

#if UNITY_EDITOR
		public TTSVoice ttsVoice => m_ttsVoice;
#endif
		#endregion
	}
}
