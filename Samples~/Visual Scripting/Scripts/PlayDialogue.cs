using UnityEngine;
using Yarn.Unity;
using System;

namespace ToolkitEngine.Dialogue.VisualScripting
{
	public class PlayDialogue : BasePlayDialogueUnit
    {
		#region Properties

		public override Func<DialogueType, YarnProject, string, Action<GameObject>, bool> trigger => DialogueManager.Play;

		#endregion
	}
}