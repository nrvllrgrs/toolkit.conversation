using UnityEngine;
using Yarn.Unity;

namespace ToolkitEngine.Dialogue
{
    [System.Serializable]
    public class YarnNode
    {
		#region Fields

		[SerializeField]
		private YarnProject m_project;

		[SerializeField]
		private string m_name;

		#endregion

		#region Properties

		public YarnProject project => m_project;
		public string name { get => m_name; internal set => m_name = value; }
		public bool isDefined => m_project != null && m_project.NodeExists(name);

		#endregion

		#region Constructors

		public YarnNode()
		{
			m_project = null;
			m_name = string.Empty;
		}

		#endregion
	}
}