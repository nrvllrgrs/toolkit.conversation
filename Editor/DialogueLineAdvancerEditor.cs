using UnityEditor;
using Yarn.Unity.Editor;
using ToolkitEngine.Dialogue;

namespace ToolkitEditor.Dialogue
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(DialogueLineAdvancer))]
	public class DialogueLineAdvancerEditor : YarnEditor
	{ }

	[CanEditMultipleObjects]
	[CustomEditor(typeof(GibberishLinePresenter))]
	public class GibberishLinePresenterEditor : YarnEditor
	{ }
}