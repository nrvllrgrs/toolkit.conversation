using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue/Cinematic")]
	[UnitTitle("Skip Cinematic")]
	public class SkipCinematic : Unit
	{
		#region Ports

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter;

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit;

		#endregion

		#region Methods

		protected override void Definition()
		{
			enter = ControlInput(nameof(enter), Trigger);
			exit = ControlOutput(nameof(exit));
			Succession(enter, exit);
		}

		private ControlOutput Trigger(Flow flow)
		{
			CinematicManager.Skip();
			return exit;
		}

		#endregion
	}
}