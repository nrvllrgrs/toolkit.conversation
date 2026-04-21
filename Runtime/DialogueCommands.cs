using System;
using System.Linq;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
	public static class DialogueCommands
    {
		#region Methods

		[YarnCommand("runDialogue")]
		public static void RunDialogue(string dialogueType, string yarnProject, string startNode)
		{
			var type = DialogueManager.GetDialogueTypes()
				.FirstOrDefault(x => string.Equals(x.name, dialogueType, StringComparison.OrdinalIgnoreCase));
			if (type == null)
				return;

			var project = DialogueManager.GetYarnProjects()
				.FirstOrDefault(x => string.Equals(x.name, yarnProject, StringComparison.OrdinalIgnoreCase));
			if (project == null)
				return;

			DialogueManager.Play(type, project, startNode);
		}

		[YarnCommand("print")]
		public static void Print(string value)
		{
			UnityEngine.Debug.Log(value);
		}

		#endregion
	}
}
