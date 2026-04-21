using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue/Cinematic")]
	[UnitTitle("Is Skippable")]
	public class IsSkippable : Unit
    {
		#region Ports

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter;

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit;

		[DoNotSerialize, PortLabelHidden]
		public ValueOutput skippable;

		#endregion

		#region Methods

		protected override void Definition()
		{
			enter = ControlInput(nameof(enter), Trigger);
			exit = ControlOutput(nameof(exit));
			Succession(enter, exit);

			skippable = ValueOutput(nameof(skippable), (flow) =>
			{
				return CinematicManager.skippable;
			});
		}

		private ControlOutput Trigger(Flow flow)
		{
			return exit;
		}

		#endregion
	}
}