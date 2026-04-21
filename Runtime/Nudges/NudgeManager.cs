using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	[SubsystemDependency(typeof(DialogueManager))]
	public class NudgeManager : ConfigurableSubsystem<NudgeManager, NudgeManagerConfig>, IInstantiableSubsystem
	{
		#region Fields

		/// <summary>
		/// Objects pausing nudge countdown timer
		/// </summary>
		private HashSet<object> m_blockers = new();

		private bool m_paused = false;
		private float m_remainingTime = float.PositiveInfinity;

		private Dictionary<NudgeType, NudgeData> m_map = new();
		private NudgeData m_activeData;

		private DialogueRunner m_runner;
		private DialogueRunnerControl m_control;

		#endregion

		#region Events

		public static event Action<bool> PauseChanged;

		#endregion

		#region Properties

		public static bool paused
		{
			get => CastInstance.m_paused || activeData == null;
			private set
			{
				// No change, skip
				if (CastInstance.m_paused == value)
					return;

				CastInstance.m_paused = value;
				PauseChanged?.Invoke(value);
			}
		}

		private static NudgeData activeData
		{
			get => CastInstance.m_activeData;
			set
			{
				// No change, skip
				if (Equals(CastInstance.m_activeData, value))
					return;

				if (runner == null)
				{
					Debug.LogError("DialogRunner in NudgeManagerConfig is undefined!");
					return;
				}

				// Stop if another nudge is actively running
				// Needs to occur before changing runner project
				if (runner.IsDialogueRunning)
				{
					runner.Stop().Forget();
				}

				CastInstance.m_activeData = value;

				if (value != null)
				{
					runner.SetProject(value.project);
					CastInstance.m_remainingTime = value.nudgeType.delayTime;

					if (!string.IsNullOrWhiteSpace(CastInstance.m_activeData.nudgeType.indexVarName))
					{
						runner.VariableStorage.SetValue(CastInstance.m_activeData.nudgeType.indexVarName, 0);
					}
				}
				else
				{
					runner.SetProject(null);
					CastInstance.m_remainingTime = float.PositiveInfinity;
				}
			}
		}

		protected static DialogueRunner runner
		{
			get
			{
				if (CastInstance.m_runner == null)
				{
					CastInstance.m_runner = CastInstance.m_control?.GetComponent<DialogueRunner>();
				}
				return CastInstance.m_runner;
			}
		}

		protected static DialogueRunnerControl control
		{
			get
			{
				if (CastInstance.m_control == null)
				{
					CastInstance.m_control = CastInstance.m_control?.GetComponent<DialogueRunnerControl>();
					if (CastInstance.m_control != null)
					{
						CastInstance.m_control.Set(runner, Config.dialogueType);
					}
				}
				return CastInstance.m_control;
			}
		}

		#endregion

		#region Methods

		protected override void Initialize()
		{
			base.Initialize();

			// Pause when any dialogue starts; unpause when dialogue complete
			DialogueManager.DialogueStarted += DialogueManager_DialogueStart;
			DialogueManager.DialogueCompleted += DialogueManager_DialogueComplete;
			LifecycleSubsystem.Register(this, LifecycleSubsystem.Phase.Update);
		}

		protected override void Terminate()
		{
			base.Terminate();

			LifecycleSubsystem.Unregister(this, LifecycleSubsystem.Phase.Update);
			if (DialogueManager.Exists)
			{
				DialogueManager.DialogueStarted -= DialogueManager_DialogueStart;
				DialogueManager.DialogueCompleted -= DialogueManager_DialogueComplete;
			}
		}

		public void Instantiate()
		{
			m_control = IInstantiableSubsystem.Instantiate(Config?.template);
		}

		public override void Update()
		{
			if (paused)
				return;

			m_remainingTime -= Time.deltaTime;
			if (m_remainingTime <= 0f)
			{
				Play();
			}
		}

		#endregion

		#region Nudge Methods

		public static void Set(NudgeType nudgeType, YarnProject project, string startNode = "Start", bool playImmediately = false)
		{
			var data = new NudgeData()
			{
				nudgeType = nudgeType,
				project = project,
				startNode = startNode,
				priority = Config.GetPriority(nudgeType)
			};

			if (!CastInstance.m_map.ContainsKey(nudgeType))
			{
				CastInstance.m_map.Add(nudgeType, data);
			}
			else
			{
				CastInstance.m_map[nudgeType] = data;
			}

			// Current data has higher priority, skip
			if (CastInstance.m_activeData != null && CastInstance.m_activeData.priority > data.priority)
				return;

			activeData = data;

			// Automatically clear other nudges, if defined by NudgeType
			if (nudgeType.autoClear)
			{
				CastInstance.m_map.Clear();
			}

			if (playImmediately && !paused)
			{
				Play();
			}
		}

		public static void Clear(NudgeType nudgeType)
		{
			if (!CastInstance.m_map.ContainsKey(nudgeType))
				return;

			// Remove data from map
			CastInstance.m_map.Remove(nudgeType);

			if (CastInstance.m_map.Count > 0)
			{
				// Set data with highest priority
				activeData = CastInstance.m_map.Values.OrderByDescending(x => x.priority).First();
			}
			else
			{
				// No active data
				activeData = null;
			}
		}

		public static void ClearAll()
		{
			CastInstance.m_map.Clear();
			activeData = null;
		}

		/// <summary>
		/// Force active nudge to play, ignoring timer
		/// </summary>
		public static void Play()
		{
			if (control != null)
			{
				control.Play(CastInstance.m_activeData.startNode).Forget();
			}
			else
			{
				runner.StartDialogue(CastInstance.m_activeData.startNode).Forget();
			}
			CastInstance.m_remainingTime = CastInstance.m_activeData.nudgeType.delayTime;
		}

		public static void ResetTimer()
		{
			CastInstance.m_remainingTime = CastInstance.m_activeData != null
				? CastInstance.m_activeData.nudgeType.delayTime
				: float.PositiveInfinity;
		}

		#endregion

		#region Control Methods

		public static void Pause(object source)
		{
			if (CastInstance.m_blockers.Add(source))
			{
				paused = true;
			}
		}

		public static void Unpause(object source)
		{
			if (CastInstance.m_blockers.Remove(source))
			{
				paused = CastInstance.m_blockers.Count > 0;
				if (!paused)
				{
					CastInstance.m_remainingTime = Mathf.Max(CastInstance.m_remainingTime, CastInstance.m_activeData.nudgeType.minDelayTime);
				}
			}
		}

		public void ForceUnpause()
		{
			m_blockers.Clear();
			paused = false;
		}

		#endregion

		#region Callbacks

		private void DialogueManager_DialogueStart(DialogueEventArgs e)
		{
			Pause(null);
		}

		private void DialogueManager_DialogueComplete(DialogueEventArgs e)
		{
			Unpause(null);
		}

		#endregion

		#region Structures

		private class NudgeData
		{
			public NudgeType nudgeType;
			public YarnProject project;
			public string startNode;
			public int priority;
		}

		#endregion
	}
}