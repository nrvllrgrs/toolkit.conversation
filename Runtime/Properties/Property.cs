using System;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	[Serializable]
	[GenericMenuCategory("Dialogue")]
	public class YarnProjectProperty : BaseProperty<YarnProject>
	{
		public YarnProjectProperty()
			: base() { }

		public YarnProjectProperty(YarnProject value)
			: base(value) { }
	}

	[Serializable]
	[GenericMenuCategory("Dialogue")]
	public class YarnNodeProperty : BaseProperty<YarnNode>
	{
		public YarnNodeProperty()
			: base() { }

		public YarnNodeProperty(YarnNode value)
			: base(value) { }
	}

	[Serializable]
	[GenericMenuCategory("Dialogue")]
	public class DialogueTypeProperty : BaseProperty<DialogueType>
	{
		public DialogueTypeProperty()
			: base() { }

		public DialogueTypeProperty(DialogueType value)
			: base(value) { }
	}

	[Serializable]
	[GenericMenuCategory("Dialogue")]
	public class DialogueSpeakerTypeProperty : BaseProperty<DialogueSpeakerType>
	{
		public DialogueSpeakerTypeProperty()
			: base() { }

		public DialogueSpeakerTypeProperty(DialogueSpeakerType value)
			: base(value) { }
	}
}