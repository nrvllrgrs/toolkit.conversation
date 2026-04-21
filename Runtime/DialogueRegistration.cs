using UnityEngine;

namespace ToolkitEngine.Dialogue
{
    [System.Serializable]
    public class DialogueRegistration
    {
		#region Enumerators

		public enum Mode
		{
			Category,
			Type
		};

		#endregion

		#region Fields

		[SerializeField]
		private Mode m_mode = Mode.Category;

		[SerializeField]
		private DialogueCategory m_dialogueCategory;

		[SerializeField]
		private DialogueType m_dialogueType;

		#endregion

		#region Properties

		public Mode mode => m_mode;
		public DialogueCategory dialogueCategory => m_dialogueCategory;
		public DialogueType dialogueType => m_dialogueType;

		#endregion

		#region Methods

		/// <summary>
		/// Indicates whether DialogueType matches DialogueRegistration
		/// </summary>
		/// <param name="dialogueType"></param>
		/// <returns></returns>
		public bool IsValid(DialogueType dialogueType)
		{
			switch (mode)
			{
				case Mode.Category:
					return DialogueManager.TryGetDialogueCategory(dialogueType, out var category)
						&& Equals(category, m_dialogueCategory);

				case Mode.Type:
					return Equals(dialogueType, m_dialogueType);
			}

			return false;
		}

		#endregion
	}
}