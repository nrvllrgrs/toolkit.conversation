using UnityEngine;
using ToolkitEngine.Scoring;

namespace ToolkitEngine.Dialogue.Scoring
{
	[EvaluableCategory("Dialogue")]
	public class DialogueQueueAgeEvaluator : BaseEvaluator
	{
		#region Fields

		[SerializeField, MinMax(0f, float.PositiveInfinity, "Min", "Max")]
		private Vector2Int m_age = new Vector2Int(0, 100);

		#endregion

		#region Properties

		public int min => m_age.x;
		public int max => m_age.y;

		#endregion

		#region Methods

		protected override float CalculateNormalizedScore(GameObject actor, GameObject target, Vector3 position)
		{
			if (actor.TryGetComponent(out DialogueRunnerControl control))
			{
				return MathUtil.GetPercent(DialogueManager.GetQueueAge(control), min, max);
			}
			return 0f;
		}

		#endregion
	}
}