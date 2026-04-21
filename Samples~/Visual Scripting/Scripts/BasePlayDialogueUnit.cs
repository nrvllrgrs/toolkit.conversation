using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	[UnitCategory("Dialogue")]
	public abstract class BasePlayDialogueUnit : Unit
	{
		#region Properties

		[UnitHeaderInspectable("Coroutine")]
		public bool coroutine;

		[DoNotSerialize, PortLabelHidden]
		public ControlInput enter { get; private set; }

		[DoNotSerialize, PortLabelHidden]
		public ControlOutput exit { get; private set; }

		[DoNotSerialize]
		public ControlOutput started { get; private set; }

		[DoNotSerialize]
		public ControlOutput completed { get; private set; }

		[DoNotSerialize]
		public ValueInput dialogueType;

		[DoNotSerialize]
		public ValueInput yarnProject;

		[DoNotSerialize]
		public ValueInput startNode;

		[DoNotSerialize, PortLabelHidden]
		public ValueOutput runnerControl;

		#endregion

		#region Properties

		public abstract Func<DialogueType, YarnProject, string, Action<GameObject>, bool> trigger { get; }

		#endregion

		#region Methods

		protected override void Definition()
		{
			if (!coroutine)
			{
				enter = ControlInput(nameof(enter), Trigger);
				exit = ControlOutput(nameof(exit));
				Succession(enter, exit);
			}
			else
			{
				enter = ControlInputCoroutine(nameof(enter), TriggerCoroutine);
				exit = ControlOutput(nameof(exit));
				Succession(enter, exit);

				started = ControlOutput(nameof(started));
				completed = ControlOutput(nameof(completed));
				Succession(enter, started);
				Succession(enter, completed);

				runnerControl = ValueOutput(nameof(runnerControl), GetDialogueRunnerControl);
			}

			dialogueType = ValueInput<DialogueType>(nameof(dialogueType), null);
			yarnProject = ValueInput<YarnProject>(nameof(yarnProject), null);
			startNode = ValueInput<string>(nameof(startNode), null);

			Requirement(dialogueType, enter);
			Requirement(yarnProject, enter);
			Requirement(startNode, enter);
		}

		private ControlOutput Trigger(Flow flow)
		{
			trigger.Invoke(
				flow.GetValue<DialogueType>(dialogueType),
				flow.GetValue<YarnProject>(yarnProject),
				flow.GetValue<string>(startNode),
				null);

			return exit;
		}

		protected IEnumerator TriggerCoroutine(Flow flow)
		{
			bool spawned = false;
			DialogueRunnerControl control = null;
			trigger.Invoke(
				flow.GetValue<DialogueType>(dialogueType),
				flow.GetValue<YarnProject>(yarnProject),
				flow.GetValue<string>(startNode),
				(obj) =>
				{
					control = obj.GetComponent<DialogueRunnerControl>();
					spawned = true;
				});

			yield return exit;
			yield return new WaitUntil(() => spawned);
			yield return new WaitUntil(() => control.isDialogueRunning);
			yield return started;
			yield return new WaitWhile(() => control.isDialogueRunning);
			yield return completed;
		}

		protected DialogueRunnerControl GetDialogueRunnerControl(Flow flow)
		{
			return DialogueManager.GetDialogueRunnerControl(
				flow.GetValue<DialogueType>(dialogueType),
				flow.GetValue<YarnProject>(yarnProject),
				flow.GetValue<string>(startNode));
		}

		#endregion
	}
}