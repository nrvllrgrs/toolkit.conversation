using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue/Nudge")]
	[UnitTitle("Pause Nudges")]
	public class PauseNudges : Unit
	{
		#region Fields

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter;

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit;

		[DoNotSerialize, NullMeansSelf]
		public ValueInput source;

		[DoNotSerialize]
		public ValueInput value;

		#endregion

		#region Methods

		protected override void Definition()
		{
			enter = ControlInput(nameof(enter), Enter);
			exit = ControlOutput(nameof(exit));
			Succession(enter, exit);

			source = ValueInput<object>(nameof(source), null);
			value = ValueInput(nameof(value), true);

			Requirement(source, enter);
		}

		private ControlOutput Enter(Flow flow)
		{
			if (flow.GetValue<bool>(value))
			{
				NudgeManager.Pause(flow.GetValue<object>(source));
			}
			else
			{
				NudgeManager.Unpause(flow.GetValue<object>(source));
			}
			return exit;
		}

		#endregion
	}
}