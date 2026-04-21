using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue/Nudge")]
	[UnitTitle("Clear Nudges")]
	public class ClearNudges : Unit
	{
		#region Fields

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter;

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit;

		[DoNotSerialize]
		public ValueInput nudgeType;

		#endregion

		#region Methods

		protected override void Definition()
		{
			enter = ControlInput(nameof(enter), Enter);
			exit = ControlOutput(nameof(exit));
			Succession(enter, exit);

			nudgeType = ValueInput<NudgeType>(nameof(nudgeType), null);
			Requirement(nudgeType, enter);
		}

		private ControlOutput Enter(Flow flow)
		{
			NudgeManager.Clear(flow.GetValue<NudgeType>(nudgeType));
			return exit;
		}

		#endregion
	}
}