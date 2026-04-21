using UnityEngine;
using ToolkitEngine.Scoring;

namespace ToolkitEngine.Dialogue.Scoring
{
	[EvaluableCategory("Dialogue")]
	public class DialogueSmartPriorityEvaluator : BaseEvaluator
	{
		#region Methods

		protected override float CalculateNormalizedScore(GameObject actor, GameObject target, Vector3 position)
		{
			if (actor.TryGetComponent(out DialogueRunnerControl control))
			{
				int priority = DialogueManager.GetPriority(control.dialogueType);
				if (priority > 0)
				{
					return MathUtil.GetPercent(priority, 0f, GetMax(control.dialogueType));
				}
			}
			return 0f;
		}

		private float GetMax(DialogueType dialogueType)
		{
			return DialogueManager.TryGetDialogueCategory(dialogueType, out var category)
				? category.priorities.Length
				: 0f;
		}

		#endregion
	}
}