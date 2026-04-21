using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public class DialogueEventArgs : System.EventArgs
	{
		#region Properties

		public DialogueRunnerControl control { get; internal set; }
		public string nodeName { get; private set; }
		public string command { get; private set; }
		public DialogueType type => control?.dialogueType;
		public DialogueRunner runner => control?.dialogueRunner;

		#endregion

		#region Constructors

		public DialogueEventArgs(DialogueRunnerControl control)
			: this(control, null)
		{ }

		public DialogueEventArgs(DialogueRunnerControl control, string nodeName)
			: this(control, nodeName, null)
		{ }

		public DialogueEventArgs(DialogueRunnerControl control, string nodeName, string command)
		{
			this.control = control;
			this.nodeName = nodeName;
			this.command = command;
		}

		#endregion
	}

	[RequireComponent(typeof(DialogueRunner))]
    public class DialogueRunnerControl : MonoBehaviour
    {
		#region Fields

		[SerializeField]
		protected DialogueType m_dialogueType;

		[SerializeField]
		protected YarnNode m_startNode;

		[SerializeField]
		protected bool m_playOnStart;

		[SerializeField]
		protected bool m_replicateSettings = false;

		[SerializeField]
		protected bool m_appendDialogueViews = false;

		[SerializeField]
		protected bool m_keepVariableStorage = false;

		[SerializeField]
		protected UnityEvent<DialogueEventArgs> m_onDialogueStarted;

		[SerializeField]
		protected UnityEvent<DialogueEventArgs> m_onDialogueCompleted;

		[SerializeField]
		protected UnityEvent<DialogueEventArgs> m_onNodeStarted;

		[SerializeField]
		protected UnityEvent<DialogueEventArgs> m_onNodeCompleted;

		[SerializeField]
		protected UnityEvent<DialogueEventArgs> m_onCommand;

		private DialogueRunner m_dialogueRunner;
		private bool m_isDialogueRunning = false;
		private float m_startTime = Mathf.NegativeInfinity;
		protected bool m_isSkipping = false;

		#endregion

		#region Events

		public UnityEvent<DialogueEventArgs> onDialogueStarted => m_onDialogueStarted;
		public UnityEvent<DialogueEventArgs> onDialogueCompleted => m_onDialogueCompleted;
		public UnityEvent<DialogueEventArgs> onNodeStarted => m_onNodeStarted;
		public UnityEvent<DialogueEventArgs> onNodeCompleted => m_onNodeCompleted;
		public UnityEvent<DialogueEventArgs> onCommand => m_onCommand;

		/// <summary>
		/// Used by DialogueManager to cleanup active runners before other subscribers are notified
		/// </summary>
		internal event System.EventHandler<DialogueEventArgs> DialogueCompleting;

		/// <summary>
		/// Used by DialogueManager to cleanup pooled runners after other subscribers are notified
		/// </summary>
		internal event System.EventHandler<DialogueEventArgs> DialogueLateCompleted;

		#endregion

		#region Properties

		public DialogueRunner dialogueRunner => m_dialogueRunner;
		public DialogueType dialogueType { get => m_dialogueType; set => m_dialogueType = value; }
		public bool isDialogueRunning => m_isDialogueRunning;
		public bool startNodeExists => !string.IsNullOrWhiteSpace(m_startNode?.name) && m_dialogueRunner.NodeExists(m_startNode?.name);

		/// <summary>
		/// Time when DialogueRunner last started
		/// </summary>
		public float age => Time.time - m_startTime;

		#endregion

		#region Methods

		protected virtual void Awake()
		{
			m_dialogueRunner = GetComponent<DialogueRunner>();
		}

		protected virtual void OnEnable()
		{
			m_dialogueRunner.onDialogueStart.AddListener(DialogueRunner_DialogueStart);
			m_dialogueRunner.onDialogueComplete.AddListener(DialogueRunner_DialogueComplete);
			m_dialogueRunner.onNodeStart.AddListener(DialogueRunner_NodeStart);
			m_dialogueRunner.onNodeComplete.AddListener(DialogueRunner_NodeComplete);
			m_dialogueRunner.onUnhandledCommand.AddListener(DialogueRunner_Command);
		}

		protected virtual void OnDisable()
		{
			if (isDialogueRunning)
			{
				Stop();
			}

			m_dialogueRunner.onDialogueStart.RemoveListener(DialogueRunner_DialogueStart);
			m_dialogueRunner.onDialogueComplete.RemoveListener(DialogueRunner_DialogueComplete);
			m_dialogueRunner.onNodeStart.RemoveListener(DialogueRunner_NodeStart);
			m_dialogueRunner.onNodeComplete.RemoveListener(DialogueRunner_NodeComplete);
			m_dialogueRunner.onUnhandledCommand.RemoveListener(DialogueRunner_Command);
		}

		public void Set(DialogueRunner runner, DialogueType dialogueType)
		{
			m_dialogueRunner = runner;
			m_dialogueType = dialogueType;
		}

		private void Start()
		{
			if (m_playOnStart)
			{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				Play();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			}
		}

		public void QuickPlay()
		{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Play();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}

		public void QuickPlay(string startNode)
		{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Play(startNode);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}

		[ContextMenu("Play")]
		public async YarnTask<bool> Play()
		{
			return await Play(m_startNode.name);
		}

		public async YarnTask<bool> Play(string nodeName)
		{
			// Node doesn't exist, skip
			if (string.IsNullOrWhiteSpace(nodeName) || !m_dialogueRunner.NodeExists(nodeName))
				return false;

			return await DialogueManager.Play(this, nodeName);
		}

		public async YarnTask<bool> Play(YarnNode node)
		{
			if (m_dialogueRunner.YarnProject != node.project)
			{
				m_dialogueRunner.SetProject(node.project);
			}
			return await Play(node.name);
		}

		internal virtual void PlayInternal(string startNode)
		{
			if (m_replicateSettings)
			{
				DialogueManager.ReplicateSettings(this, m_appendDialogueViews, m_keepVariableStorage);
			}
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			m_dialogueRunner.StartDialogue(startNode);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}

		[ContextMenu("Enqueue")]
		public void Enqueue()
		{
			Enqueue(m_startNode.name);
		}

		public void Enqueue(string startNode)
		{
			// Node doesn't exist, skip
			if (!m_dialogueRunner.NodeExists(startNode))
				return;

			DialogueManager.Enqueue(this, startNode);
		}

		[ContextMenu("Dequeue")]
		public void Dequeue()
		{
			Dequeue(m_startNode.name);
		}

		public void Dequeue(string startNode)
		{
			// Node doesn't exist, skip
			if (!m_dialogueRunner.NodeExists(startNode))
				return;

			DialogueManager.Dequeue(this, startNode);
		}

		[ContextMenu("Clear Queue")]
		public void ClearQueue()
		{
			DialogueManager.ClearQueue(this);
		}

		[ContextMenu("Stop")]
		public void Stop()
		{
			Stop(false);
		}

		public virtual void Stop(bool skipping)
		{
			if (!m_isDialogueRunning)
				return;

			m_isSkipping = skipping;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			m_dialogueRunner.Stop();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}

		public void SetStartNode(YarnProject project, string nodeName)
		{
			if (Application.isPlaying)
			{
				m_dialogueRunner.SetProject(project);
			}
#if UNITY_EDITOR
			else
			{
				GetComponent<DialogueRunner>().SetProject(project);
			}		
#endif
			m_startNode.name = nodeName;
		}

		#endregion

		#region Callbacks

		private void DialogueRunner_DialogueStart()
		{
			// Skipping is not officially completing...
			if (m_isSkipping)
			{
				// ...we've skipped, so don't invoke events
				m_isSkipping = false;
				return;
			}

			m_startTime = Time.time;
			m_isDialogueRunning = true;

			// Notify method/subscribers dialogue started
			DialogueStarted();
			m_onDialogueStarted?.Invoke(new DialogueEventArgs(this));
		}

		protected virtual void DialogueStarted()
		{ }

		private void DialogueRunner_DialogueComplete()
		{
			// Skipping is not officially completing, skip
			if (m_isSkipping)
				return;

			m_isDialogueRunning = false;

			var e = new DialogueEventArgs(this);
			DialogueCompleting?.Invoke(this, e);

			// Notify method/subscribers dialogue completed
			DialogueCompleted();
			m_onDialogueCompleted?.Invoke(e);
			DialogueLateCompleted?.Invoke(this, e);
		}

		protected virtual void DialogueCompleted()
		{ }

		private void DialogueRunner_NodeStart(string nodeName)
		{
			NodeStarted(nodeName);
			m_onNodeStarted?.Invoke(new DialogueEventArgs(this, nodeName));
		}

		protected virtual void NodeStarted(string nodeName)
		{ }

		private void DialogueRunner_NodeComplete(string nodeName)
		{
			NodeCompleted(nodeName);
			m_onNodeCompleted?.Invoke(new DialogueEventArgs(this, nodeName));
		}

		protected virtual void NodeCompleted(string nodeName)
		{ }

		private void DialogueRunner_Command(string command)
		{
			m_onCommand?.Invoke(new DialogueEventArgs(this, null, command));
		}

		#endregion

		#region Editor-Only
#if UNITY_EDITOR

		[ContextMenu("Rename GameObject")]
		private void RenameGameObject()
		{
			if (string.IsNullOrWhiteSpace(m_startNode.name))
				return;

			gameObject.name = $"Dialogue_{m_startNode.name}";
		}

#endif
		#endregion
	}
}