using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using ToolkitEngine.Rendering;

namespace ToolkitEngine.Dialogue
{
	public class Portrait : MonoBehaviour
    {
		#region Fields

		[SerializeField]
		protected InterfaceReference<ISpriteDisplay> m_display;

		[SerializeField]
		[Tooltip("If true, only the speaker types listed are used by this portrait. If false, all speaker types defined in Config are used except those listed.")]
		protected bool m_isAllowList = true;

		[SerializeField]
		protected List<DialogueSpeakerType> m_speakerTypes;

		private IEnumerable<DialogueSpeakerType> m_cachedList = null;

		#endregion

		#region Properties

		public ISpriteDisplay display => m_display.Value;

		public IEnumerable<DialogueSpeakerType> speakerTypes
		{
			get
			{
				if (m_isAllowList)
					return m_speakerTypes;

				if (m_cachedList == null)
				{
					m_cachedList = DialogueManager.Config.speakers.Except(m_speakerTypes);
				}
				return m_cachedList;
			}
		}

		#endregion

		#region Methods

		private void Awake()
		{
			if (m_display.Value == null)
			{
				m_display.Value = GetComponent<ISpriteDisplay>();
			}
			Assert.IsNotNull(m_display);
		}

		private void OnEnable()
		{
			PortraitManager.Register(this);
		}

		private void OnDisable()
		{
			PortraitManager.Unregister(this);
		}

		#endregion
	}
}