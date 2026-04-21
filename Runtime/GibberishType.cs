using NaughtyAttributes;
using UnityEngine;

namespace ToolkitEngine.Dialogue
{
	[CreateAssetMenu(menuName = "Toolkit/Dialogue/Gibberish Type", order = 403)]
	public class GibberishType : ScriptableObject
    {
		#region Fields

		[SerializeField, Min(1)]
		private int m_frequency = 1;

		[SerializeField, MinMaxSlider(-3f, 3f)]
		private Vector2 m_pitch = Vector2.zero;

		[SerializeField]
		private AudioClip[] m_clips;

		#endregion

		#region Properties

		public int frequency => m_frequency;
		public float minPitch => m_pitch.x;
		public float maxPitch => m_pitch.y;
		public float pitchRange => maxPitch - minPitch;

		protected int minPitchAsInt => (int)(minPitch * 100);
		protected int maxPitchAsInt => (int)(maxPitch * 100);
		protected int pitchRangeAsInt => (int)(pitchRange * 100);

		#endregion

		#region Methods

		public void GetAudioClipAndPitch(char c, out AudioClip clip, out float pitch)
		{
			if (m_clips.Length > 0)
			{
				int hashCode = c.GetHashCode();
				clip = m_clips[hashCode % m_clips.Length];
				pitch = pitchRangeAsInt != 0
					? ((hashCode % pitchRangeAsInt) + minPitchAsInt) / 100f
					: minPitch;
			}
			else
			{
				clip = default;
				pitch = 0;
			}
		}

		public void Play(AudioSource audioSource, char c)
		{
			if (audioSource == null)
				return;

			if (m_clips.Length == 0)
				return;

			GetAudioClipAndPitch(c, out var clip, out var pitch);
			audioSource.pitch = pitch;
			audioSource.PlayOneShot(clip);
		}

		#endregion
	}
}