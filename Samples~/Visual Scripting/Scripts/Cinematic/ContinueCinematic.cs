using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue/Cinematic")]
	[UnitTitle("Continue Cinematic")]
	public class ContinueCinematic : Unit
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
			CinematicManager.Continue();
			return exit;
		}

		#endregion
	}
}