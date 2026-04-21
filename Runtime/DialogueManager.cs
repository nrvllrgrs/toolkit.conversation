using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Yarn.Unity;

#if USE_UNITY_LOCALIZATION
using Yarn.Unity.UnityLocalization;
#endif

namespace ToolkitEngine.Dialogue
{
	public class DialogueManager : ConfigurableSubsystem<DialogueManager, DialogueManagerConfig>, IInstantiableSubsystem
    {
		#region Fields

		private Dictionary<DialogueType, DialogueCategory> m_priorityToCategoryMap;
		private Dictionary<DialogueCategory, RuntimeDialogueCategory> m_runtimeMap;

		private Dictionary<DialogueCategory, DialogueRunnerSettings> m_settingsByCategory;
		private Dictionary<DialogueType, DialogueRunnerSettings> m_settingsByType;

		private Dictionary<Tuple<DialogueType, YarnProject, string>, DialogueRunnerControl> m_spawnMap = new();
		private Dictionary<DialogueSpeakerType, HashSet<DialogueSpeaker>> m_speakerMap = new();
		private Dictionary<string, DialogueSpeakerType> m_characterNameToSpeakerTypeMap = new Dictionary<string, DialogueSpeakerType>(StringComparer.OrdinalIgnoreCase);

		private HashSet<string> m_activeSpeakerNames;

		private const float MIN_DELAY_BETWEEN_REUSE = 0.2f;

#if UNITY_EDITOR
		private static GameObject s_container;
#endif
		#endregion

		#region Events

		public static event Action<DialogueEventArgs> DialogueStarted;
		public static event Action<DialogueEventArgs> DialogueCompleted;
		public static event Action<DialogueEventArgs> NodeStarted;
		public static event Action<DialogueEventArgs> NodeCompleted;
		public static event Action<DialogueEventArgs> Command;

		#endregion

		#region Properties

		/// <summary>
		/// Gets a value that indicates if the dialogue is actively
		/// running.
		/// </summary>
		public static bool isAnyDialogueRunning => CastInstance.m_runtimeMap.Any(x => x.Value.isDialogueRunning);

#if UNITY_EDITOR
		private static Transform container
		{
			get
			{
				if (s_container == null)
				{
					s_container = new GameObject("Dialogues");
					UnityEngine.Object.DontDestroyOnLoad(s_container);
				}
				return s_container.transform;
			}
		}
#endif
		#endregion

		#region Methods

		protected override void Initialize()
		{
			m_priorityToCategoryMap = new();
			m_runtimeMap = new();
			m_settingsByCategory = new();
			m_settingsByType = new();
			m_spawnMap = new();
			m_speakerMap = new();
			m_characterNameToSpeakerTypeMap = new Dictionary<string, DialogueSpeakerType>(StringComparer.OrdinalIgnoreCase);
			m_activeSpeakerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			int categoryPriority = Config.categories.Length;
			foreach (var category in Config.categories)
			{
				//Debug.Log($"Creating {category.name} RuntimeDialogueCategory...");
				foreach (var priority in category.priorities)
				{
					if (priority == null)
						continue;

					//Debug.Log($"Adding {priority.name} to {category.name}...");
					m_priorityToCategoryMap.Add(priority, category);
				}

				var runtimeCategory = new RuntimeDialogueCategory(category, categoryPriority--);
				runtimeCategory.DialogueStarted += RuntimeCategory_DialogueStart;
				runtimeCategory.DialogueCompleted += RuntimeCategory_DialogueComplete;
				runtimeCategory.NodeStarted += RuntimeCategory_NodeStarted;
				runtimeCategory.NodeCompleted += RuntimeCategory_NodeCompleted;
				runtimeCategory.Command += RuntimeCategory_Command;

				m_runtimeMap.Add(category, runtimeCategory);
			}

			foreach (var speaker in Config.speakers)
			{
				if (!m_characterNameToSpeakerTypeMap.ContainsKey(speaker.name))
				{
					m_characterNameToSpeakerTypeMap.Add(speaker.name, speaker);
				}
			}
		}

		public void Instantiate()
		{
			IInstantiableSubsystem.Instantiate(Config?.runnerSettingsTemplate);

			// Any instantiated DialogueRunners should automatically be cleared by PoolItemManager
		}

		protected override void Terminate()
		{
			if (m_runtimeMap == null)
				return;

			foreach (var runtimeCategory in m_runtimeMap.Values)
			{
				runtimeCategory.Dispose();
			}
			m_runtimeMap = null;
		}

		#endregion

		#region Control Methods

		public static bool IsDialogueCategoryRunning(DialogueCategory category)
		{
			return CastInstance.m_runtimeMap.TryGetValue(category, out var runtimeCategory)
				? runtimeCategory.isDialogueRunning
				: false;
		}

		public static bool Play(DialogueType dialogueType, YarnProject project, string startNode, Action<GameObject> onSpawned = null)
		{
			if (!TryGetRuntimeDialogueCategory(dialogueType, out var runtimeCategory))
				return false;

			Config.dialogueSpawner.Instantiate(DialogueSpawned, runtimeCategory, dialogueType, project, startNode, true, onSpawned);
			return false;
		}

		public static async YarnTask<bool> Play(DialogueRunnerControl control, string startNode)
		{
			if (!TryGetRuntimeDialogueCategory(control, out var runtimeCategory))
				return false;

			return await runtimeCategory.Play(control, startNode);
		}

		public static bool Enqueue(DialogueType dialogueType, YarnProject project, string startNode, Action<GameObject> onSpawned = null)
		{
			if (!TryGetRuntimeDialogueCategory(dialogueType, out var runtimeCategory))
				return false;

			Config.dialogueSpawner.Instantiate(DialogueSpawned, runtimeCategory, dialogueType, project, startNode, false, onSpawned);
			return false;
		}

		public static void Enqueue(DialogueRunnerControl control, string startNode)
		{
			if (!TryGetRuntimeDialogueCategory(control, out var runtimeCategory))
				return;

			runtimeCategory.Enqueue(control, startNode);
		}

		public static void Dequeue(DialogueRunnerControl control, string startNode)
		{
			if (!TryGetRuntimeDialogueCategory(control, out var runtimeCategory))
				return;

			runtimeCategory.Dequeue(control);
		}

		public static void ClearQueue(DialogueType dialogueType)
		{
			if (!TryGetRuntimeDialogueCategory(dialogueType, out var runtimeCategory))
				return;

			runtimeCategory.ClearQueue();
		}

		public static void ClearQueue(DialogueRunnerControl control)
		{
			if (!TryGetRuntimeDialogueCategory(control, out var runtimeCategory))
				return;

			runtimeCategory.ClearQueue();
		}

		public static DialogueType[] GetDialogueTypes() => CastInstance.m_priorityToCategoryMap.Keys.ToArray();
		public static YarnProject[] GetYarnProjects() => Config.projects;

		public static bool TryGetDialogueCategory(DialogueType type, out DialogueCategory category)
		{
			if (TryGetRuntimeDialogueCategory(type, out var runtimeCategory))
			{
				category = runtimeCategory.dialogueCategory;
				return true;
			}

			category = null;
			return false;
		}

		private static bool TryGetRuntimeDialogueCategory(DialogueRunnerControl control, out RuntimeDialogueCategory runtimeCategory)
		{
			return TryGetRuntimeDialogueCategory(control?.dialogueType, out runtimeCategory);
		}

		private static bool TryGetRuntimeDialogueCategory(DialogueType type, out RuntimeDialogueCategory runtimeCategory)
		{
			runtimeCategory = null;
			if (!Exists)
				return false;

			if (type == null)
			{
				Debug.LogError("DialogueType is undefined! Cannot play dialogue.");
				return false;
			}

			if (!CastInstance.m_priorityToCategoryMap.TryGetValue(type, out var category)
				|| !CastInstance.m_runtimeMap.TryGetValue(category, out runtimeCategory))
			{
				Debug.LogErrorFormat("DialogueType {0} does not exist in config! Cannot play dialogue.", type.name);
				return false;
			}

			return true;
		}

		public static int GetCategoryPriority(DialogueCategory category)
		{
			return CastInstance.m_runtimeMap.TryGetValue(category, out var runtimeCategory)
				? runtimeCategory.priority
				: -1;
		}

		public static int GetPriority(DialogueType dialogueType)
		{
			return TryGetRuntimeDialogueCategory(dialogueType, out var runtimeCategory)
				? runtimeCategory.dialogueCategory.GetPriority(dialogueType)
				: -1;
		}

		public static float GetQueueAge(DialogueRunnerControl control)
		{
			return TryGetRuntimeDialogueCategory(control, out var runtimeCategory)
				? runtimeCategory.GetQueueAge(control)
				: float.PositiveInfinity;
		}

		#endregion

		#region Settings Methods

		public static void Register(DialogueRunnerSettings settings)
		{
			switch (settings.registration.mode)
			{
				case DialogueRegistration.Mode.Category:
					if (!CastInstance.m_settingsByCategory.ContainsKey(settings.registration.dialogueCategory))
					{
						CastInstance.m_settingsByCategory.Add(settings.registration.dialogueCategory, settings);
					}
					else
					{
						CastInstance.m_settingsByCategory[settings.registration.dialogueCategory] = settings;
					}
					break;

				case DialogueRegistration.Mode.Type:
					if (!CastInstance.m_settingsByType.ContainsKey(settings.registration.dialogueType))
					{
						CastInstance.m_settingsByType.Add(settings.registration.dialogueType, settings);
					}
					else
					{
						CastInstance.m_settingsByType[settings.registration.dialogueType] = settings;
					}
					break;
			}
		}

		public static void Unregister(DialogueRunnerSettings settings)
		{
			if (!Exists)
				return;

			switch (settings.registration.mode)
			{
				case DialogueRegistration.Mode.Category:
					CastInstance.m_settingsByCategory.Remove(settings.registration.dialogueCategory);
					break;

				case DialogueRegistration.Mode.Type:
					CastInstance.m_settingsByType.Remove(settings.registration.dialogueType);
					break;
			}
		}

		public static bool TryGetFirstDialogueRunner(DialogueRegistration registration, out DialogueRunner runner)
		{
			runner = null;

			if (registration == null)
				return false;

			RuntimeDialogueCategory runtimeCategory = null;
			switch (registration.mode)
			{
				case DialogueRegistration.Mode.Category:
					if (registration.dialogueCategory != null
						&& CastInstance.m_runtimeMap.TryGetValue(registration.dialogueCategory, out runtimeCategory))
					{ }
					break;

				case DialogueRegistration.Mode.Type:
					if (registration.dialogueType != null
						&& TryGetRuntimeDialogueCategory(registration.dialogueType, out runtimeCategory))
					{ }
					break;
			}

			if (runtimeCategory?.isDialogueRunning ?? false)
			{
				runner = runtimeCategory.activeRunnerControls[0].dialogueRunner;
				return true;
			}
			return false;
		}

		public static bool TryGetDialogueRunnerSettings(DialogueRegistration registration, out DialogueRunnerSettings settings)
		{
			settings = null;

			if (registration == null)
				return false;

			switch (registration.mode)
			{
				case DialogueRegistration.Mode.Category:
					return TryGetDialogueRunnerSettings(registration.dialogueCategory, out settings);

				case DialogueRegistration.Mode.Type:
					return TryGetDialogueRunnerSettings(registration.dialogueType, out settings);
			}
			return false;
		}

		public static bool TryGetDialogueRunnerSettings(DialogueCategory category, out DialogueRunnerSettings settings)
		{
			return CastInstance.m_settingsByCategory.TryGetValue(category, out settings);
		}

		public static bool TryGetDialogueRunnerSettings(DialogueType type, out DialogueRunnerSettings settings)
		{
			if (CastInstance.m_settingsByType.TryGetValue(type, out settings))
				return true;

			return TryGetDialogueCategory(type, out var category)
				&& TryGetDialogueRunnerSettings(category, out settings);
		}

		public static bool ReplicateSettings(DialogueRunnerControl control, bool appendDialogueViews, bool keepVariableStorage)
		{
			if (TryGetDialogueRunnerSettings(control.dialogueType, out var settings) && settings != null)
			{
				if (!appendDialogueViews)
				{
					control.dialogueRunner.DialoguePresenters = settings.dialoguePresenters;
				}
				else
				{
					// Going to append, skipping DialogueViews of the same type
					var existingTypes = control.dialogueRunner.DialoguePresenters.Select(x => x.GetType()).ToHashSet();

					// Appended list
					var list = new List<DialoguePresenterBase>(control.dialogueRunner.DialoguePresenters);
					foreach (var dialogueView in settings.dialoguePresenters)
					{
						if (existingTypes.Contains(dialogueView.GetType()))
							continue;

						list.Add(dialogueView);
					}

					control.dialogueRunner.DialoguePresenters = list.ToArray();
				}

				if (!keepVariableStorage && settings.variableStorage != null)
				{
					control.dialogueRunner.VariableStorage = settings.variableStorage;
				}

				switch (settings.runSelectedOption)
				{
					case DialogueRunnerSettings.RunSelectedOption.AsLine:
						control.dialogueRunner.runSelectedOptionAsLine = true;
						break;

					case DialogueRunnerSettings.RunSelectedOption.NotAsLine:
						control.dialogueRunner.runSelectedOptionAsLine = false;
						break;
				}

				return true;
			}
			return false;
		}

		#endregion

		#region Spawner Methods

		internal static void UpdateLineProvider(DialogueRunnerControl control)
		{
#if USE_UNITY_LOCALIZATION
			if (control.dialogueRunner.YarnProject.localizationType == LocalizationType.Unity
				&& control.dialogueRunner.LineProvider is UnityLocalisedLineProvider localizedLineProvider
				&& (Config.tableMap?.TryGetTables(control.dialogueRunner.YarnProject, out var tables) ?? false))
			{
				ReflectionUtil.TrySetFieldValue(localizedLineProvider, "stringsTable", tables.stringTable);
				ReflectionUtil.TrySetFieldValue(localizedLineProvider, "assetTable", tables.audioTable);
			}
#endif
		}

		private static void DialogueSpawned(GameObject obj, params object[] args)
		{
			UnityEngine.Object.DontDestroyOnLoad(obj);

#if UNITY_EDITOR
			obj.transform.SetParent(container);
#endif

			var control = obj.GetComponent<DialogueRunnerControl>();
			if (control == null)
				return;

			control.dialogueType = args[1] as DialogueType;
			control.dialogueRunner.SetProject(args[2] as YarnProject);
			string startNode = (string)args[3];

			// Map parameters to spawned object so it can be referenced
			var key = new Tuple<DialogueType, YarnProject, string>(control.dialogueType, control.dialogueRunner.YarnProject, startNode);
			if (CastInstance.m_spawnMap.TryGetValue(key, out var prevControl) && prevControl == null)
			{
				CastInstance.m_spawnMap.Remove(key);
			}

			CastInstance.m_spawnMap.Add(key, control);

			UpdateLineProvider(control);

			// Notify custom behaviour that DialogueRunnerControl has been spawned
			(args[5] as Action<GameObject>)?.Invoke(obj);

			// Control may have been been able to play (e.g. blocked by simultaneous limit, "forgotten" in queue)
			// Unsubscribe before subscribing to release pool item
			control.DialogueLateCompleted -= Instance_DialogueLateCompleted;
			control.DialogueLateCompleted += Instance_DialogueLateCompleted;

			// Play versus enqueue
			var runtimeCategory = args[0] as RuntimeDialogueCategory;
			if ((bool)args[4])
			{
				_ = runtimeCategory.Play(control, startNode);
			}
			else
			{
				runtimeCategory.Enqueue(control, startNode);
			}
		}

		private static void Instance_DialogueLateCompleted(object sender, DialogueEventArgs e)
		{
			if (e?.control == null)
				return;

			e.control.DialogueLateCompleted -= Instance_DialogueLateCompleted;
			PoolItem.Destroy(e.control.gameObject);
		}

		public static DialogueRunnerControl GetDialogueRunnerControl(DialogueType dialogueType, YarnProject project, string startNode)
		{
			var key = new Tuple<DialogueType, YarnProject, string>(dialogueType, project, startNode);
			return CastInstance.m_spawnMap.TryGetValue(key, out var control)
				? control
				: null;
		}

		#endregion

		#region Speaker Methods

		public static void Register(DialogueSpeaker speaker)
		{
			Assert.IsNotNull(speaker);
			Assert.IsNotNull(speaker.speakerType);

			if (!CastInstance.m_speakerMap.TryGetValue(speaker.speakerType, out var set))
			{
				set = new();
				CastInstance.m_speakerMap.Add(speaker.speakerType, set);
			}

			set.Add(speaker);

			// Need to map characterName to speakerType
			// This can be permanent
			if (!CastInstance.m_characterNameToSpeakerTypeMap.ContainsKey(speaker.speakerType.name))
			{
				CastInstance.m_characterNameToSpeakerTypeMap.Add(speaker.speakerType.name, speaker.speakerType);
			}
		}

		public static void Unregister(DialogueSpeaker speaker)
		{
			Assert.IsNotNull(speaker);
			Assert.IsNotNull(speaker.speakerType);

			if (!CastInstance.m_speakerMap.TryGetValue(speaker.speakerType, out var set))
				return;

			set.Remove(speaker);
		}

		public static bool TryGetDialogueSpeakers(DialogueSpeakerType speakerType, out HashSet<DialogueSpeaker> speakers)
		{
			speakers = null;
			return speakerType != null && CastInstance.m_speakerMap.TryGetValue(speakerType, out speakers);
		}

		public static bool TryGetDialogueSpeakerTypeByCharacterName(string characterName, out DialogueSpeakerType speakerType)
		{
			speakerType = null;
			return characterName != null && CastInstance.m_characterNameToSpeakerTypeMap.TryGetValue(characterName, out speakerType);
		}

		public static bool TryGetDialogueSpeakersByCharacterName(string characterName, out HashSet<DialogueSpeaker> speakers)
		{
			speakers = null;
			return TryGetDialogueSpeakerTypeByCharacterName(characterName, out var speakerType)
				&& TryGetDialogueSpeakers(speakerType, out speakers);
		}

		public static bool IsAnyDialogueSpeakerRunning() => CastInstance.m_activeSpeakerNames.Any();

		internal static void ActivateSpeaker(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
				return;

			CastInstance.m_activeSpeakerNames.Add(characterName);
		}
		internal static void DeactivateSpeaker(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
				return;

			CastInstance.m_activeSpeakerNames.Remove(characterName);
		}

		#endregion

		#region Callbacks

		private static void RuntimeCategory_DialogueStart(object sender, DialogueEventArgs e)
		{
			DialogueStarted?.Invoke(e);
		}

		private static void RuntimeCategory_DialogueComplete(object sender, DialogueEventArgs e)
		{
			DialogueCompleted?.Invoke(e);
		}

		private static void RuntimeCategory_NodeStarted(object sender, DialogueEventArgs e)
		{
			NodeStarted?.Invoke(e);
		}

		private static void RuntimeCategory_NodeCompleted(object sender, DialogueEventArgs e)
		{
			NodeCompleted?.Invoke(e);
		}

		private static void RuntimeCategory_Command(object sender, DialogueEventArgs e)
		{
			Command?.Invoke(e);
		}

		#endregion

		#region Structures

		[Serializable]
		public class RuntimeDialogueCategory : IDisposable
		{
			#region Fields

			private List<DialogueRunnerControl> m_activeRunnerControls = new();
			private Dictionary<DialogueRunnerControl, Tuple<string, float>> m_queue = new();
			private bool m_interrupted = false;

			private bool m_disposed;

			#endregion

			#region Events

			internal event EventHandler<DialogueEventArgs> DialogueStarted;
			internal event EventHandler<DialogueEventArgs> DialogueCompleted;
			internal event EventHandler<DialogueEventArgs> NodeStarted;
			internal event EventHandler<DialogueEventArgs> NodeCompleted;
			internal event EventHandler<DialogueEventArgs> Command;

			#endregion

			#region Properties

			public DialogueCategory dialogueCategory { get; private set; }
			public int priority { get; private set; }
			public bool isDialogueRunning => m_activeRunnerControls.Count > 0;
			public int dialogueRunningCount => m_activeRunnerControls.Count;
			public DialogueRunnerControl[] activeRunnerControls => m_activeRunnerControls.ToArray();

			#endregion

			#region Constructors

			public RuntimeDialogueCategory(DialogueCategory category, int priority)
			{
				dialogueCategory = category;
				this.priority = priority;
			}

			#endregion

			#region Methods

			internal async YarnTask<bool> Play(DialogueRunnerControl control, string startNode)
			{
				// Remove all "null" active runners
				m_activeRunnerControls.Clean();

				// Under allowed simultaneous runners
				if (dialogueCategory.infiniteSimultaneous || m_activeRunnerControls.Count < dialogueCategory.maxSimultaneous)
				{
					PlayInternal(control, startNode);
					return true;
				}
				else
				{
					// Determine if interruption should occur
					var interruptable = GetInterruptable(dialogueCategory, control);
					if (interruptable != null)
					{
						m_interrupted = true;
						if (interruptable.dialogueRunner.IsDialogueRunning)
						{
							interruptable.Stop();
							if (interruptable == control)
							{
								await YarnTask.Delay(TimeSpan.FromSeconds(MIN_DELAY_BETWEEN_REUSE));
							}
						}
						Remove(interruptable);

						return await Play(control, startNode);
					}
					// Determine if enqueuing should occur
					else if (dialogueCategory.queueable && control.dialogueType.enqueueIfBlocked)
					{
						Enqueue(control, startNode);
						return true;
					}
				}
				return false;
			}

			private DialogueRunnerControl GetInterruptable(DialogueCategory category, DialogueRunnerControl control)
			{
				// DialogueType cannot interrupt, skip
				if (control.dialogueType.interruptPriority == 0)
				{
					return null;
				}

				int priority = category.GetPriority(control.dialogueType);

				HashSet<DialogueRunnerControl> interruptableControls = new();
				foreach (var activeControl in m_activeRunnerControls)
				{
					int activePriority = category.GetPriority(activeControl.dialogueType);
					if ((activePriority < priority && (control.dialogueType.interruptPriority & DialogueType.InterruptRule.LessThan) != 0)
						|| (activePriority == priority && (control.dialogueType.interruptPriority & DialogueType.InterruptRule.Equal) != 0)
						|| (activePriority > priority && (control.dialogueType.interruptPriority & DialogueType.InterruptRule.GreaterThan) != 0))
					{
						interruptableControls.Add(activeControl);
					}
				}

				return category.GetInterruptable(interruptableControls);
			}

			private void PlayInternal(DialogueRunnerControl control, string startNode)
			{
				control.DialogueCompleting += DialogueRunnerControl_DialogueCompleting;
				control.onDialogueStarted.AddListener(DialogueRunnerControl_DialogueStarted);
				control.onDialogueCompleted.AddListener(DialogueRunnerControl_DialogueCompleted);
				control.onNodeStarted.AddListener(DialogueRunnerControl_NodeStarted);
				control.onNodeCompleted.AddListener(DialogueRunnerControl_NodeCompleted);
				control.onCommand.AddListener(DialogueRunnerControl_Command);

				m_activeRunnerControls.Add(control);

				control.PlayInternal(startNode);

				if (dialogueCategory.queueable && control.dialogueType.autoClearQueue)
				{
					ClearQueue();
				}
			}

			internal void Enqueue(DialogueRunnerControl control, string startNode)
			{
				// Under allowed simultaneous runners
				if (dialogueCategory.infiniteSimultaneous || m_activeRunnerControls.Count < dialogueCategory.maxSimultaneous)
				{
					PlayInternal(control, startNode);
				}
				else if (dialogueCategory.queueable)
				{
					EnqueueInternal(control, startNode);
				}
			}

			private void EnqueueInternal(DialogueRunnerControl control, string startNode)
			{
				m_queue.Add(control, new Tuple<string, float>(startNode, Time.time));
			}

			internal void Dequeue(DialogueRunnerControl control)
			{
				if (m_queue.ContainsKey(control))
				{
					m_queue.Remove(control);
				}
			}

			internal void ClearQueue()
			{
				m_queue.Clear();
			}

			internal float GetQueueAge(DialogueRunnerControl control)
			{
				return m_queue.TryGetValue(control, out var tuple)
					? Time.time - tuple.Item2
					: float.PositiveInfinity;
			}

			private void Remove(DialogueRunnerControl control)
			{
				if (!m_activeRunnerControls.Contains(control))
					return;

				m_activeRunnerControls.Remove(control);
			}

			#endregion

			#region Callbacks

			private bool IsActiveDialogueRunnerControl(DialogueEventArgs e)
			{
				return e?.control != null && (m_activeRunnerControls?.Contains(e.control) ?? false);
			}

			private void DialogueRunnerControl_DialogueStarted(DialogueEventArgs e)
			{
				if (!IsActiveDialogueRunnerControl(e))
					return;

				DialogueStarted?.Invoke(this, e);
			}

			private void DialogueRunnerControl_DialogueCompleting(object sender, DialogueEventArgs e)
			{
				if (!IsActiveDialogueRunnerControl(e))
					return;

				e.control.DialogueCompleting -= DialogueRunnerControl_DialogueCompleting;
				e.control.onDialogueStarted.RemoveListener(DialogueRunnerControl_DialogueStarted);
				e.control.onDialogueCompleted.RemoveListener(DialogueRunnerControl_DialogueCompleted);
				e.control.onNodeStarted.AddListener(DialogueRunnerControl_NodeStarted);
				e.control.onNodeCompleted.AddListener(DialogueRunnerControl_NodeCompleted);
				e.control.onCommand.AddListener(DialogueRunnerControl_Command);

				Remove(e.control);
				DialogueRunnerControl_DialogueCompleted(e);
			}

			private async void DialogueRunnerControl_DialogueCompleted(DialogueEventArgs e)
			{
				DialogueCompleted?.Invoke(this, e);

				// Check if dialogue is queued and start next
				// But if interrupted, skip
				if (!m_interrupted && m_queue != null && m_queue.Count > 0)
				{
					// Find "forgotten" keys
					var forgottenKeys = m_queue.Keys.Where(x => GetQueueAge(x) > dialogueCategory.timeToForget).ToArray();
					foreach (var key in forgottenKeys)
					{
						m_queue.Remove(key);
					}

					// Find next runner to play
					var next = dialogueCategory.Next(m_queue.Keys);
					if (next != null && m_queue.TryGetValue(next, out var tuple))
					{
						await YarnTask.Delay(TimeSpan.FromSeconds(Config.delayBetweenDequeues));

						m_queue.Remove(next);
						m_interrupted = false;
						PlayInternal(next, tuple.Item1);
					}
				}

				m_interrupted = false;
			}

			private void DialogueRunnerControl_NodeStarted(DialogueEventArgs e)
			{
				if (!IsActiveDialogueRunnerControl(e))
					return;

				e.control = e.runner.GetComponent<DialogueRunnerControl>();
				NodeStarted?.Invoke(this, e);
			}

			private void DialogueRunnerControl_NodeCompleted(DialogueEventArgs e)
			{
				if (!IsActiveDialogueRunnerControl(e))
					return;

				e.control = e.runner.GetComponent<DialogueRunnerControl>();
				NodeCompleted?.Invoke(this, e);
			}

			private void DialogueRunnerControl_Command(DialogueEventArgs e)
			{
				if (!IsActiveDialogueRunnerControl(e))
					return;

				e.control = e.runner.GetComponent<DialogueRunnerControl>();
				Command?.Invoke(this, e);
			}

			#endregion

			#region IDisposable Methods

			~RuntimeDialogueCategory()
			{
				Dispose(false);
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing)
			{
				if (m_disposed)
					return;

				m_activeRunnerControls = null;
				m_queue = null;
				m_disposed = true;
			}

			#endregion
		}

		#endregion
	}
}