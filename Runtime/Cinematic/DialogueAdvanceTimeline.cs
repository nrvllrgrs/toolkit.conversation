using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public class DialogueAdvanceTimeline : MonoBehaviour, INotificationReceiver
	{
		#region Fields

		[SerializeField]
		private DialogueRunner m_dialogueRunner;

		[SerializeField]
		private SignalAsset m_signal;

		private TimelineView m_lineView;

		#endregion

		#region Properties

		public bool tracked { get; private set; }

		#endregion

		#region Methods

		private void OnEnable()
		{
			TimelineManager.CastInstance.PlayableDirectorTracked += TimelineManager_PlayableDirectorTracked;
			TimelineManager.CastInstance.PlayableDirectorUntracked += TimelineManager_PlayableDirectorUntracked;
		}

		private void OnDisable()
		{
			TimelineManager.CastInstance.PlayableDirectorTracked -= TimelineManager_PlayableDirectorTracked;
			TimelineManager.CastInstance.PlayableDirectorUntracked -= TimelineManager_PlayableDirectorUntracked;
		}

		private void TimelineManager_PlayableDirectorTracked(object sender, PlayableDirector e)
		{
			if (m_lineView == null)
			{
				foreach (var dialogueView in m_dialogueRunner.DialoguePresenters)
				{
					if (dialogueView is TimelineView lineView)
					{
						m_lineView = lineView;
						break;
					}
				}
			}

			tracked = true;
		}

		private void TimelineManager_PlayableDirectorUntracked(object sender, PlayableDirector e)
		{
			tracked = false;
		}

		public void OnNotify(Playable origin, INotification notification, object context)
		{
			if (tracked
				&& notification is SignalEmitter signal
				&& Equals(signal?.asset, m_signal))
			{
				m_lineView?.Resume();
			}
		}

		#endregion
	}
}