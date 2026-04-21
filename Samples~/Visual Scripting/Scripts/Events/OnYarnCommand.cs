using System;
using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitTitle("On Command")]
	public class OnYarnCommand : BaseFilteredDialogueEventUnit
	{
		public override Type MessageListenerType => typeof(OnYarnCommandMessageListener);

		protected override void StartListeningToManager()
		{
			DialogueManager.Command += InvokeTrigger;
		}

		protected override void StopListeningToManager()
		{
			DialogueManager.Command -= InvokeTrigger;
		}

		protected override string GetFilterValue(DialogueEventArgs args) => args.command;
	}
}