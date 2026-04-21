using System;
using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitTitle("On Dialogue Completed")]
	public class OnDialogueCompleted : BaseDialogueEventUnit
	{
		public override Type MessageListenerType => typeof(OnDialogueCompletedMessageListener);

		protected override void StartListeningToManager()
		{
			DialogueManager.DialogueCompleted += InvokeTrigger;
		}

		protected override void StopListeningToManager()
		{
			DialogueManager.DialogueCompleted -= InvokeTrigger;
		}
	}
}