using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Attributes;

#if USE_TMP
using TMPro;
#else
using TextMeshProUGUI = Yarn.Unity.TMPShim;
#endif

#nullable enable

namespace ToolkitEngine.Dialogue
{
	public class DialogueSpeakerColorPresenter : DialoguePresenterBase
	{
		#region Fields

		/// <summary>
		/// The <see cref="TMP_Text"/> object that displays the text of
		/// dialogue lines.
		/// </summary>
		[MustNotBeNull]
		[SerializeField]
		private TMP_Text? m_lineText;

		[SerializeField]
		private TMP_Text? m_characterNameText;

		[SerializeField]
		private Color m_defaultColor = Color.white;

		#endregion

		#region Methods

		public override YarnTask OnDialogueStartedAsync()
		{
			return YarnTask.CompletedTask;
		}

		public override YarnTask RunLineAsync(LocalizedLine dialogueLine, LineCancellationToken token)
		{
			if (m_lineText != null)
			{
				var color = DialogueManager.TryGetDialogueSpeakerTypeByCharacterName(dialogueLine.CharacterName, out var speakerType)
					? speakerType.color
					: m_defaultColor;

				m_lineText.color = color;

				if (m_characterNameText != null)
				{
					m_characterNameText.color = color;
				}
			}
			return YarnTask.CompletedTask;
		}

		public override YarnTask OnDialogueCompleteAsync()
		{
			return YarnTask.CompletedTask;
		}

		#endregion
	}
}