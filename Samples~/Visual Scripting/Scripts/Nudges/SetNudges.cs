using Unity.VisualScripting;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue/Nudge")]
	[UnitTitle("Set Nudges")]
	public class SetNudges : Unit
	{
		#region Fields

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter;

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit;

		[DoNotSerialize]
		public ValueInput nudgeType;

		[DoNotSerialize]
		public ValueInput yarnNode;

		[DoNotSerialize]
		public ValueInput playImmediately;

		#endregion

		#region Methods

		protected override void Definition()
		{
			enter = ControlInput(nameof(enter), Enter);
			exit = ControlOutput(nameof(exit));
			Succession(enter, exit);

			nudgeType = ValueInput<NudgeType>(nameof(nudgeType), null);
			yarnNode = ValueInput<YarnNode>(nameof(yarnNode), null);
			playImmediately = ValueInput(nameof(playImmediately), false);

			Requirement(nudgeType, enter);
			Requirement(yarnNode, enter);
		}

		private ControlOutput Enter(Flow flow)
		{
			var node = flow.GetValue<YarnNode>(yarnNode);

			NudgeManager.Set(
				flow.GetValue<NudgeType>(nudgeType),
				node.project,
				node.name,
				flow.GetValue<bool>(playImmediately));
			return exit;
		}

		#endregion
	}
}