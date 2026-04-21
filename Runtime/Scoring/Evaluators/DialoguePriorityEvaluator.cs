using UnityEngine;
using ToolkitEngine.Scoring;

namespace ToolkitEngine.Dialogue.Scoring
{
	[EvaluableCategory("Dialogue")]
	public class DialoguePriorityEvaluator : BaseEvaluator
	{
		#region Fields

		[SerializeField, MinMax(0f, float.PositiveInfinity, "Min", "Max")]
		private Vector2Int m_range = new Vector2Int(0, 100);

		#endregion

		#region Properties

		public int min => m_range.x;
		public int max => m_range.y;

		#endregion

		#region Methods

		protected override float CalculateNormalizedScore(GameObject actor, GameObject target, Vector3 position)
		{
			if (actor.TryGetComponent(out DialogueRunnerControl control))
			{
				int priority = DialogueManager.GetPriority(control.dialogueType);
				if (priority > 0)
				{
					return MathUtil.GetPercent(priority, min, max);
				}
			}
			return 0f;
		}

		#endregion
	}
}