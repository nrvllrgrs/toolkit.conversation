using System;
using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	public class EnqueueDialogue : BasePlayDialogueUnit
	{
		#region Properties

		public override Func<DialogueType, YarnProject, string, Action<GameObject>, bool> trigger => DialogueManager.Enqueue;

		#endregion
	}
}