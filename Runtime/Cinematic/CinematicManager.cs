using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public class CinematicManager : ConfigurableSubsystem<CinematicManager, CinematicManagerConfig>
    {
		#region Fields

		private bool m_skippable = false;
		private string m_skipDestination = null;
		private bool m_waiting = false;

		private float m_remainingTime = 0f;
		private float m_timeout = 0f;

		private DialogueRunnerControl m_cinematicControl;
		private int m_animateStateHash;

		#endregion

		#region Events

		public static event Action<bool> SkippableChanged;

		#endregion

		#region Properties

		public static string skipDestination
		{
			get => CastInstance.m_skipDestination;
			private set
			{
				// No change, skip
				if (CastInstance.m_skipDestination == value)
					return;

				CastInstance.m_skipDestination = value;
				skippable = !string.IsNullOrWhiteSpace(CastInstance.m_skipDestination);
			}
		}

		public static bool skippable
		{
			get => CastInstance.m_skippable;
			private set
			{
				// No change, skip
				if (CastInstance.m_skippable == value)
					return;

				bool wasSkippable = skippable;
				CastInstance.m_skippable = value;

				if (wasSkippable != skippable)
				{
					SkippableChanged?.Invoke(!wasSkippable);
				}
			}
		}

		public static float remainingTime => CastInstance.m_remainingTime;
		public static float normalizedRemainingTime => CastInstance.m_remainingTime / CastInstance.m_timeout;

		#endregion

		#region Methods

		protected override void Initialize()
		{
			DialogueManager.DialogueStarted += DialogueManager_DialogueStarted;
			m_animateStateHash = Animator.StringToHash(Config.animateStateName);
		}

		protected override void Terminate()
		{
			if (DialogueManager.Exists)
			{
				DialogueManager.DialogueStarted -= DialogueManager_DialogueStarted;
			}
		}

		public static void Skip()
		{
			if (!skippable || CastInstance.m_cinematicControl == null)
				return;

			CastInstance.m_cinematicControl.Stop(true);
			if (!string.IsNullOrWhiteSpace(CastInstance.m_skipDestination))
			{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				CastInstance.m_cinematicControl.Play(skipDestination);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				skipDestination = null;
			}
		}

		public static void Continue()
		{
			CastInstance.m_waiting = false;
		}

		#endregion

		#region Callbacks

		private static void DialogueManager_DialogueStarted(DialogueEventArgs e)
		{
			if (Config == null)
				return;

			if (e.control.dialogueType != Config.dialogueType)
				return;

			// Store DialogueRunnerControl associated with "cinematic" DialogueType
			CastInstance.m_cinematicControl = e.control;
		}

		#endregion

		#region Skip

		[YarnCommand("skipAndEnd")]
		public static void SetupSkipAndEnd()
		{
			CleanupSkip();
			skippable = true;

			DialogueManager.DialogueCompleted += Skip_DialogueCompleted;
		}

		[YarnCommand("skip")]
		public static void SetupSkip(string destinationNode)
		{
			CleanupSkip();

			// Store destination of skipping
			skipDestination = destinationNode;

			DialogueManager.NodeStarted += Skip_NodeStarted;
			DialogueManager.DialogueCompleted += Skip_DialogueCompleted;
		}

		private static void Skip_NodeStarted(DialogueEventArgs e)
		{
			if (!Equals(e.control, CastInstance.m_cinematicControl)
				|| !Equals(e.nodeName, skipDestination))
				return;

			CleanupSkip();
		}

		private static void Skip_DialogueCompleted(DialogueEventArgs e)
		{
			// If Cinematic Dialogue ends before reaching skip node, cleanup
			if (!Equals(e.control, CastInstance.m_cinematicControl))
				return;

			CleanupSkip();
		}

		private static void CleanupSkip()
		{
			// Clear destination...cannot skip anymore
			skipDestination = null;

			// Stop watching events
			DialogueManager.NodeStarted -= Skip_NodeStarted;
			DialogueManager.DialogueCompleted -= Skip_DialogueCompleted;
		}

		#endregion

		#region Wait For Continue

		[YarnCommand("waitForContinue")]
		public static IEnumerator WaitForContinue()
		{
			CastInstance.m_waiting = true;
			yield return new WaitWhile(() => CastInstance.m_waiting);
		}

		[YarnCommand("waitForContinueWithTimeout")]
		public static IEnumerator WaitForContinueWithTimeout(float timeout, string variableName)
		{
			CastInstance.m_waiting = true;
			CastInstance.m_remainingTime = CastInstance.m_timeout = timeout;

			while (CastInstance.m_remainingTime > 0f)
			{
				yield return null;

				if (!CastInstance.m_waiting)
				{
					SetVariable(variableName, true);
				}
				CastInstance.m_remainingTime -= Time.deltaTime;
			}

			CastInstance.m_waiting = false;
			SetVariable(variableName, false);
		}

		private static void SetVariable(string variableName, bool value)
		{
			if (CastInstance.m_cinematicControl == null)
				return;

			var variableStorage = CastInstance.m_cinematicControl.dialogueRunner.VariableStorage;
			variableStorage.SetValue(variableName, value);
		}

		#endregion

		#region Animate

		[YarnCommand("animate")]
		public static void Animate(string characterName, string animationKey)
		{
			Animate(characterName, animationKey, Config.animateStateName, CastInstance.m_animateStateHash);
		}

		[YarnCommand("customAnimate")]
		public static void Animate(string characterName, string animationKey, string animStateName)
		{
			Animate(characterName, animationKey, animStateName, Animator.StringToHash(animStateName));
		}

		public static void Animate(string characterName, string animationKey, string animStateName, int animStateHash)
		{
			if (DialogueManager.TryGetDialogueSpeakerTypeByCharacterName(characterName, out var speakerType)
			   && DialogueManager.TryGetDialogueSpeakers(speakerType, out var speakers))
			{
				Animate(speakerType, speakers, animationKey, animStateName, animStateHash);
			}
		}

		public static void Animate(DialogueSpeakerType speakerType, HashSet<DialogueSpeaker> speakers, string animationKey)
		{
			Animate(speakerType, speakers, animationKey, Config.animateStateName, CastInstance.m_animateStateHash);
		}

		public static void Animate(DialogueSpeakerType speakerType, HashSet<DialogueSpeaker> speakers, string animationKey, string animStateName)
		{
			Animate(speakerType, speakers, animationKey, Config.animateStateName, Animator.StringToHash(animStateName));
		}

		public static void Animate(DialogueSpeakerType speakerType, HashSet<DialogueSpeaker> speakers, string animationKey, string animStateName, int animStateHash)
		{
			if (speakers == null)
				return;

			// Set animation for each found speaker
			foreach (var speaker in speakers)
			{
				AnimationClip clip = null;
				if ((speaker.GetComponent<AnimationSetOverride>()?.TryGetClip(animationKey, out clip) ?? false)
					|| (speakerType.animationSet?.TryGetClip(animationKey, out clip) ?? false))
				{
					var animatorStack = speaker.GetComponent<AnimatorStack>();
					if (animatorStack != null)
					{
						animatorStack.Clear();
						animatorStack.Push(clip, animStateName);
						animatorStack.animator.Play(animStateHash, 0);
					}
				}
			}
		}

		#endregion
	}
}