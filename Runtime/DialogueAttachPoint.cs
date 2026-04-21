using UnityEngine;

namespace ToolkitEngine.Dialogue
{
    public class DialogueAttachPoint : MonoBehaviour
    {
		#region Fields

		[SerializeField]
		private DialogueRegistration m_registration;

		[SerializeField]
		private AttachPoint m_attachPoint;

		#endregion

		#region Methods

		private void Awake()
		{
			m_attachPoint.Initalize(transform);
		}

		private void OnEnable()
		{
			DialogueManager.DialogueStarted += DialogueManager_DialogueStarted;
		}

		private void OnDisable()
		{
			if (DialogueManager.Exists)
			{
				DialogueManager.DialogueStarted -= DialogueManager_DialogueStarted;
			}
		}

		private void DialogueManager_DialogueStarted(DialogueEventArgs e)
		{
			// Not matching DialogueType, skip
			if (!m_registration.IsValid(e.type))
				return;

			DialogueManager.DialogueStarted -= DialogueManager_DialogueStarted;

			if (DialogueManager.TryGetDialogueRunnerSettings(m_registration, out var settings))
			{
				m_attachPoint.Attach(settings.transform);
			}
		}

		#endregion
	}
}