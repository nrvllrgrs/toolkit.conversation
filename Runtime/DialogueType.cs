using System;
using UnityEngine;

namespace ToolkitEngine.Dialogue
{
	[CreateAssetMenu(menuName = "Toolkit/Dialogue/Dialogue Type", order = 402)]
	public class DialogueType : ScriptableObject
	{
		#region Enumerators

		[Flags]
		public enum InterruptRule
		{
			Equal = 1 << 1,
			LessThan = 1 << 2,
			GreaterThan = 1 << 3,
		}

		#endregion

		#region Fields

		[SerializeField, HideInInspector]
		private string m_id = Guid.NewGuid().ToString();

		[SerializeField, Tooltip("Specifies whether incoming DialogueType can interrupt active dialogue with other priority.")]
		private InterruptRule m_interruptPriority;

		[SerializeField, Tooltip("Indicates whether incoming DialogueType is enqueued (if possible) when blocked.")]
		private bool m_enqueueIfBlocked = false;

		[SerializeField, Tooltip("Indicates whether queue should be cleared when DialogueType is played.")]
		private bool m_autoClearQueue = false;

		#endregion

		#region Properties

		public InterruptRule interruptPriority => m_interruptPriority;
		public bool enqueueIfBlocked => m_enqueueIfBlocked;
		public bool autoClearQueue => m_autoClearQueue;

		#endregion

		#region Methods

		public bool Equals(DialogueType other)
		{
			return other != null
				&& m_id == other.m_id;
		}

		public override bool Equals(object other) => Equals(other as DialogueType);

		public override int GetHashCode() => m_id.GetHashCode();

		#endregion
	}
}
