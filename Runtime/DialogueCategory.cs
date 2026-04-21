using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ToolkitEngine.Scoring;
using System;

namespace ToolkitEngine.Dialogue
{
	[CreateAssetMenu(menuName = "Toolkit/Dialogue/Dialogue Category", order = 400)]
	public class DialogueCategory : ScriptableObject
	{
		#region Fields

		[SerializeField, HideInInspector]
		private string m_id = Guid.NewGuid().ToString();

		[SerializeField, Nested]
		private List<DialogueType> m_priorities = new();

		[SerializeField]
		private bool m_infiniteSimulatenous = true;

		[SerializeField, Min(1)]
		private int m_maxSimultaneous = 1;

		/// <summary>
		/// Evalutes active DialogueRunners to determine which has the lowest priority to interrupt.
		/// </summary>
		[SerializeField, Tooltip("Evaluates active DialogueRunners to determine which has the lowest priority to interrupt.")]
		private UnityEvaluator m_interruptPriority = new();

		[SerializeField]
		private bool m_queueable = false;

		[SerializeField]
		private UnityEvaluator m_queuePriority = new();

		[SerializeField, MaxInfinity]
		private float m_timeToForget = 0f;

		#endregion

		#region Properties

		public DialogueType[] priorities => m_priorities.ToArray();
		public bool infiniteSimultaneous => m_infiniteSimulatenous;
		public int maxSimultaneous => m_maxSimultaneous;
		public bool queueable => m_queueable;
		public float timeToForget => m_timeToForget;

		#endregion

		#region Methods

		public int GetPriority(DialogueType dialogueType)
		{
			int index = m_priorities.IndexOf(dialogueType);
			return index >= 0
				? (m_priorities.Count - 1) - index
				: index;
		}

		public DialogueRunnerControl GetInterruptable(IEnumerable<DialogueRunnerControl> controls)
		{
			// Find the control with the lowest utility
			return Evaluate(controls, m_interruptPriority, -1);
		}

		public DialogueRunnerControl Next(IEnumerable<DialogueRunnerControl> controls)
		{
			// Find the control with the highest utility
			return Evaluate(controls, m_queuePriority, 1);
		}

		private DialogueRunnerControl Evaluate(IEnumerable<DialogueRunnerControl> controls, UnityEvaluator evaluator, int comparerValue)
		{
			if (controls == null)
			{
				return null;
			}

			int count = controls.Count();
			if (count == 0)
			{
				return null;
			}
			else if (count == 1)
			{
				return controls.ElementAt(0);
			}

			DialogueRunnerControl selected = null;
			float selectedWeight = comparerValue > 0
				? float.NegativeInfinity
				: float.PositiveInfinity;

			// Find the control with the lowest utility
			foreach (var control in controls)
			{
				float weight = evaluator.Evaluate(control.gameObject, null);
				if (weight.CompareTo(selectedWeight) == comparerValue)
				{
					selected = control;
					selectedWeight = weight;
				}
			}
			return selected;
		}

		public bool Equals(DialogueCategory other)
		{
			return other != null
				&& m_id == other.m_id;
		}

		public override bool Equals(object other) => Equals(other as DialogueCategory);

		public override int GetHashCode() => m_id.GetHashCode();

		#endregion
	}
}