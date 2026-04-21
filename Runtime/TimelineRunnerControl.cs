using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace ToolkitEngine.Dialogue
{
	public class TimelineRunnerControl : DialogueRunnerControl
	{
		#region Fields

		[SerializeField]
		private List<PlayableDirector> m_directors = new();

		private HashSet<TimelineView> m_timelineViews = new();
		private PlayableDirector m_playingDirector = null;

		#endregion

		#region Events

		[SerializeField]
		private UnityEvent<DialogueEventArgs> m_onSkipped;

		#endregion

		#region Properties

		public bool playing => m_playingDirector != null;
		public UnityEvent<DialogueEventArgs> onSkipped => m_onSkipped;

		#endregion

		#region Methods

		protected override void Awake()
		{
			base.Awake();

			if (m_directors.Count == 0)
			{
				m_directors.AddRange(GetComponentsInChildren<PlayableDirector>());
			}

			foreach (var director in m_directors)
			{
				director.played += PlayableDirector_Played;
				director.stopped += PlayableDirector_Stopped;
			}
		}

		protected virtual void OnDestroy()
		{
			foreach (var director in m_directors)
			{
				director.played -= PlayableDirector_Played;
				director.stopped -= PlayableDirector_Stopped;
			}
		}

		internal override void PlayInternal(string startNode)
		{
			base.PlayInternal(startNode);

			m_timelineViews.Clear();
			foreach (var view in dialogueRunner.DialoguePresenters)
			{
				if (view is not TimelineView timelineView)
					continue;

				timelineView.StartDialogue(this);
				m_timelineViews.Add(timelineView);
			}
		}

		public void Resume()
		{
			foreach (var view in m_timelineViews)
			{
				view.Resume();
			}
		}

		public override void Stop(bool skipping)
		{
			if (!isDialogueRunning)
				return;

			m_isSkipping = skipping;
			dialogueRunner.Stop();

			if (skipping)
			{
				onSkipped?.Invoke(new DialogueEventArgs(this));
			}
		}

		#endregion

		#region PlayableDirector Callbacks

		private void PlayableDirector_Played(PlayableDirector director)
		{
			m_playingDirector = director;
		}

		private void PlayableDirector_Stopped(PlayableDirector director)
		{
			m_playingDirector = null;
		}

		#endregion

		#region Editor-Only
#if UNITY_EDITOR

		[ContextMenu("Populate Directors")]
		private void PopulateDirectors()
		{
			m_directors.Clear();
			m_directors.AddRange(GetComponentsInChildren<PlayableDirector>());
		}

#endif
		#endregion
	}
}