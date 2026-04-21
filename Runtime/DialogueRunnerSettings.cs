using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public class DialogueRunnerSettings : MonoBehaviour
    {
		#region Enumerators

		public enum RunSelectedOption
		{
			UseDialogueRunner,
			AsLine,
			NotAsLine
		};

		#endregion

		#region Fields

		[SerializeField]
		private DialogueRegistration m_registration;

		[SerializeField]
		private VariableStorageBehaviour m_variableStorage;

		[SerializeField, FormerlySerializedAs("m_dialogueViews")]
		private DialoguePresenterBase[] m_dialoguePresenters;

		[SerializeField]
		private RunSelectedOption m_runSelectedOption = RunSelectedOption.UseDialogueRunner;

		#endregion

		#region Events

		[SerializeField, Foldout("Events")]
		private UnityEvent<DialogueEventArgs> m_onDialogueStarted;

		[SerializeField, Foldout("Events")]
		private UnityEvent<DialogueEventArgs> m_onDialogueCompleted;

		#endregion

		#region Properties

		public DialogueRegistration registration => m_registration;
		public VariableStorageBehaviour variableStorage => m_variableStorage;
		public DialoguePresenterBase[] dialoguePresenters => m_dialoguePresenters;
		public RunSelectedOption runSelectedOption => m_runSelectedOption;

		public DialogueRunner firstDialogueRunner
		{
			get
			{
				return DialogueManager.TryGetFirstDialogueRunner(registration, out var dialogueRunner)
					? dialogueRunner
					: null;
			}
		}

		#endregion

		#region Methods

		private void OnEnable()
		{
			DialogueManager.Register(this);
			DialogueManager.DialogueStarted += DialogueManager_DialogueStarted;
			DialogueManager.DialogueCompleted += DialogueManager_DialogueCompleted;
		}

		private void OnDisable()
		{
			DialogueManager.DialogueStarted -= DialogueManager_DialogueStarted;
			DialogueManager.DialogueCompleted -= DialogueManager_DialogueCompleted;
			DialogueManager.Unregister(this);
		}

		private void DialogueManager_DialogueStarted(DialogueEventArgs e)
		{
			if (!m_registration.IsValid(e.type))
				return;

			m_onDialogueStarted?.Invoke(e);
		}

		private void DialogueManager_DialogueCompleted(DialogueEventArgs e)
		{
			if (!m_registration.IsValid(e.type))
				return;

			m_onDialogueCompleted?.Invoke(e);
		}

		public void Stop()
		{
			firstDialogueRunner?.Stop();
		}

		#endregion
	}
}