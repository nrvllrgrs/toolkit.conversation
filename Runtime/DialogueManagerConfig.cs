using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	[CreateAssetMenu(menuName = "Toolkit/Config/DialogueManager Config")]
	public class DialogueManagerConfig : ScriptableObject
    {
		#region Fields

		[SerializeField, Nested]
		private List<DialogueCategory> m_categories = new();

		[SerializeField]
		private List<YarnProject> m_projects = new();

		[SerializeField]
		private List<DialogueSpeakerType> m_speakers = new();

		[Space]

		[SerializeField, Min(0f)]
		[Tooltip("Seconds to wait after dialogue completes before dequeuing next dialogue.")]
		private float m_delayBetweenDequeues = 0.2f;

		[SerializeField]
		private GameObject m_runnerSettingsTemplate;

		[Space]

		[SerializeField]
		private Spawner m_dialogueSpawner;

#if USE_UNITY_LOCALIZATION
		[SerializeField]
		private LocalizedTableMap m_tableMap;
#endif
		#endregion

		#region Properties

		public DialogueCategory[] categories => m_categories.ToArray();
		public YarnProject[] projects => m_projects.ToArray();
		public DialogueSpeakerType[] speakers => m_speakers.ToArray();
		public Spawner dialogueSpawner => m_dialogueSpawner;
		public float delayBetweenDequeues => m_delayBetweenDequeues;
		public GameObject runnerSettingsTemplate => m_runnerSettingsTemplate;

#if USE_UNITY_LOCALIZATION
		public LocalizedTableMap tableMap => m_tableMap;
#endif

		#endregion
	}
}