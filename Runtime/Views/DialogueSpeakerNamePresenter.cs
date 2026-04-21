using UnityEngine;
using Yarn.Unity;

#if USE_TMP
using TMPro;
#else
using TextMeshProUGUI = Yarn.Unity.TMPShim;
#endif

#nullable enable

namespace ToolkitEngine.Dialogue
{
	public class DialogueSpeakerNamePresenter : DialoguePresenterBase
	{
		#region Fields

		[SerializeField]
		private TMP_Text? m_characterNameText;

		#endregion

		#region Methods

		public override YarnTask OnDialogueStartedAsync()
		{
			return YarnTask.CompletedTask;
		}

		public override YarnTask RunLineAsync(LocalizedLine dialogueLine, LineCancellationToken token)
		{
			if (m_characterNameText != null
				&& DialogueManager.TryGetDialogueSpeakerTypeByCharacterName(dialogueLine.CharacterName, out var speakerType))
			{
				m_characterNameText.text = speakerType.displayName;
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