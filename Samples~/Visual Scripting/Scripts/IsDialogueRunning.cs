using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue")]
	public class IsDialogueRunning : Unit
    {
		#region Fields

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter { get; private set; }

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit { get; private set; }

		[UnitHeaderInspectable("Any Dialogue")]
		public bool anyDialogue = true;

		[DoNotSerialize]
		public ValueInput dialogueCategory;

		[DoNotSerialize]
		public ValueOutput running;

		#endregion

		#region Methods

		protected override void Definition()
		{
			enter = ControlInput(nameof(enter), Trigger);
			exit = ControlOutput(nameof(exit));
			Succession(enter, exit);

			running = ValueOutput(nameof(running), (flow) =>
			{
				return anyDialogue
					? DialogueManager.isAnyDialogueRunning
					: DialogueManager.IsDialogueCategoryRunning(flow.GetValue<DialogueCategory>(dialogueCategory));
			});

			if (!anyDialogue)
			{
				dialogueCategory = ValueInput<DialogueCategory>(nameof(dialogueCategory), null);
				Requirement(dialogueCategory, enter);
			}
		}

		private ControlOutput Trigger(Flow flow)
		{
			return exit;
		}

		#endregion
	}
}