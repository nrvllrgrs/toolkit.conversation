using UnityEngine;
using NaughtyAttributes;

namespace ToolkitEngine.Dialogue
{
    public class DialogueSpeaker : MonoBehaviour
    {
		#region Fields

		[SerializeField, Required]
		private DialogueSpeakerType m_speakerType;

		[SerializeField]
		private AudioSource m_audioSource;

		#endregion

		#region Properties

		public DialogueSpeakerType speakerType => m_speakerType;
		public AudioSource audioSource => m_audioSource;

		#endregion

		#region Methods

		private void Awake()
		{
			if (audioSource == null)
			{
				// If we don't have an audio source, add one. 
				m_audioSource = gameObject.AddComponent<AudioSource>();

				// Additionally, we'll assume that the user didn't place the
				// game object that this component is attached to deliberately,
				// so we'll set the spatial blend to 1 (which means the audio
				// will be positioned in 3D space.)
				m_audioSource.spatialBlend = 1f;
			}
		}

		private void OnEnable()
		{
			DialogueManager.Register(this);
		}

		private void OnDisable()
		{
			DialogueManager.Unregister(this);
		}

		#endregion
	}
}