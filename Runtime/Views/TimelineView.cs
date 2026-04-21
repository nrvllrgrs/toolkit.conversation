using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	/// <summary>
	/// A Dialogue View that presents lines of dialogue, using Unity UI
	/// elements.
	/// </summary>
	public class TimelineView : DialoguePresenterBase
	{
		#region Fields

		[SerializeField]
		private DialoguePresenterBase[] m_presenters;

		/// <summary>
		/// Indicates whether dialogue view is waiting for timeline signal
		/// </summary>
		private bool m_waitingForSignal = false;

		/// <summary>
		/// Indicates whether waiting for signal is skipped due to interruption
		/// </summary>
		private bool m_skipWaiting = false;

		private TimelineRunnerControl m_timelineRunnerControl = null;

		#endregion

		#region Methods

		public void StartDialogue(TimelineRunnerControl dialogueRunnerControl)
		{
			m_timelineRunnerControl = dialogueRunnerControl; 
		}

		public override async YarnTask RunLineAsync(LocalizedLine localisedLine, LineCancellationToken token)
		{
			// Need to wait for signal from Timeline before displaying line
			m_waitingForSignal = !m_skipWaiting;
			m_skipWaiting = false;

			await YarnTask.WaitUntil(() => m_waitingForSignal);

			var pendingTasks = new HashSet<YarnTask>();
			foreach (var presenter in m_presenters)
			{
				if (presenter == null || !presenter.enabled)
					continue;

				async YarnTask RunLineAndInvokeCompletion(DialoguePresenterBase view, LocalizedLine line, LineCancellationToken token)
				{
					try
					{
						// Run the line and wait for it to finish
						await view.RunLineAsync(localisedLine, token);
					}
					catch (OperationCanceledException)
					{
						// The line presenter cancelled (rather than returning.)
						// This probably wasn't intended - they should clean up
						// and return null.
						Debug.LogWarning($"Dialogue presenter {view.name} threw an {nameof(OperationCanceledException)} when running its {nameof(DialoguePresenterBase.RunLineAsync)} method. Dialogue presenters should not throw this exception; instead, clean up any needed user-facing content, and return.", view);
					}
					catch (Exception e)
					{
						// If a dialogue presenter throws an exception, we need
						// to return, because the dialogue runner is waiting for
						// our task to complete. We'll log the exception so that
						// it's not lost, and exit here.
						Debug.LogException(e, view);
					}
				}

				pendingTasks.Add(RunLineAndInvokeCompletion(presenter, localisedLine, token));
			}

			// Wait for all line view tasks to finish delivering the line.
			var waitForAllLines = YarnTask.WhenAll(pendingTasks);
			if (!waitForAllLines.IsCompletedSuccessfully())
			{
				await waitForAllLines;
			}
		}

		public void Resume()
		{
			if (!m_waitingForSignal)
			{
				m_skipWaiting = true;
			}

			m_waitingForSignal = false;
		}

		public override YarnTask OnDialogueStartedAsync()
		{
			return YarnTask.CompletedTask;
		}

		public override YarnTask OnDialogueCompleteAsync()
		{
			return YarnTask.CompletedTask;
		}

		#endregion
	}
}
