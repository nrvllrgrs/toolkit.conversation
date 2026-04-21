using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public interface IPortraitPresenter
	{
		bool TryGetCustomPortraitKey(DialogueSpeakerType speakerType, out string portraitKey);
	}

	public class PortraitPresenter : DialoguePresenterBase, IPortraitPresenter
	{
		#region Methods

		public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
		{
			if (DialogueManager.TryGetDialogueSpeakerTypeByCharacterName(line.CharacterName, out var speakerType))
			{
				PortraitManager.SetPortrait(speakerType, line, this);
			}
			return YarnTask.CompletedTask;
		}

		public override YarnTask OnDialogueStartedAsync()
		{
			return YarnTask.CompletedTask;
		}

		public override YarnTask OnDialogueCompleteAsync()
		{
			PortraitManager.HideAllPortraits();
			return YarnTask.CompletedTask;
		}

		public virtual bool TryGetCustomPortraitKey(DialogueSpeakerType speakerType, out string portraitKey)
		{
			portraitKey = default;
			return false;
		}

		#endregion
	}
}