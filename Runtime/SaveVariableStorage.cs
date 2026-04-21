using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using ToolkitEngine.SaveManagement;

using BoolDictionary = System.Collections.Generic.Dictionary<string, bool>;
using FloatDictionary = System.Collections.Generic.Dictionary<string, float>;
using StringDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace ToolkitEngine.Dialogue
{
	[Serializable]
	public class SaveBoolVariableMap : SerializableDictionary<string, SaveBool>
	{ }

	[Serializable]
	public class SaveNumberVariableMap : SerializableDictionary<string, SaveFloat>
	{ }

	[Serializable]
	public class SaveStringVariableMap : SerializableDictionary<string, SaveString>
	{ }

	[AddComponentMenu("Toolkit/Dialogue/Save Variable Storage")]
	public class SaveVariableStorage : VariableStorageBehaviour
	{
		#region Fields

		[SerializeField]
		private SaveBoolVariableMap m_boolMap = new();

		[SerializeField]
		private SaveNumberVariableMap m_numberMap = new();

		[SerializeField]
		private SaveStringVariableMap m_stringMap = new();

		#endregion

		// -----------------------------------------------------------------
		// Runtime state
		// -----------------------------------------------------------------

		/// <summary>
		/// Reverse lookup: saveVariable.id → Yarn variable name.
		/// Built once in <see cref="Awake"/> so that the
		/// <see cref="SaveManager.VariableChanged"/> handler is O(1).
		/// </summary>
		private Dictionary<string, string> m_saveIdToYarnName = new();

		// -----------------------------------------------------------------
		// Unity lifecycle
		// -----------------------------------------------------------------

		private void Awake()
		{
			RebuildReverseLookup();
		}

		private void OnEnable()
		{
			SaveManager.VariableChanged += OnSaveManagerVariableChanged;
		}

		private void OnDisable()
		{
			SaveManager.VariableChanged -= OnSaveManagerVariableChanged;
		}

		// -----------------------------------------------------------------
		// VariableStorageBehaviour — core overrides
		// -----------------------------------------------------------------

		/// <inheritdoc/>
		public override bool TryGetValue<T>(string variableName, out T result)
		{
			if (m_numberMap.TryGetValue(variableName, out var floatVar))
			{
				return SaveManager.TryGetValue(floatVar, out result);
			}

			if (m_stringMap.TryGetValue(variableName, out var stringVar))
			{
				return SaveManager.TryGetValue(stringVar, out result);
			}

			if (m_boolMap.TryGetValue(variableName, out var boolVar))
			{
				return SaveManager.TryGetValue(boolVar, out result);
			}

			result = default;
			return false;
		}

		/// <inheritdoc/>
		public override void SetValue(string variableName, float floatValue)
		{
			if (m_numberMap.TryGetValue(variableName, out var floatVar))
			{
				SaveManager.TrySetValue(floatVar, floatValue);
			}
		}

		/// <inheritdoc/>
		public override void SetValue(string variableName, string stringValue)
		{
			if (m_stringMap.TryGetValue(variableName, out var stringVar))
			{
				SaveManager.TrySetValue(stringVar, stringValue);
			}
		}

		/// <inheritdoc/>
		public override void SetValue(string variableName, bool boolValue)
		{
			if (m_boolMap.TryGetValue(variableName, out var boolVar))
			{
				SaveManager.TrySetValue(boolVar, boolValue);
			}
		}

		/// <summary>
		/// Resets every mapped variable to its <see cref="SaveManager"/>
		/// default value.  Unrelated save data is left untouched.
		/// </summary>
		public override void Clear()
		{
			ResetValues(m_numberMap);
			ResetValues(m_stringMap);
			ResetValues(m_boolMap);
		}

		private void ResetValues<T>(Dictionary<string, T> map)
			where T : SaveVariable
		{
			foreach (var saveVar in map.Values)
			{
				if (saveVar != null)
				{
					SaveManager.ResetValue(saveVar);
				}
			}
		}

		/// <inheritdoc/>
		public override bool Contains(string variableName)
		{
			return m_numberMap.TryGetValue(variableName, out var saveVar)
				&& saveVar != null
				&& saveVar.isDefined;
		}

		// -----------------------------------------------------------------
		// VariableStorageBehaviour — bulk access
		// -----------------------------------------------------------------

		/// <inheritdoc/>
		public override void SetAllVariables(FloatDictionary floats, StringDictionary strings, BoolDictionary bools, bool clear = true)
		{
			if (clear)
			{
				Clear();
			}

			foreach (var (name, value) in floats)
			{
				SetValue(name, value);
			}

			foreach (var (name, value) in strings)
			{
				SetValue(name, value);
			}

			foreach (var (name, value) in bools)
			{
				SetValue(name, value);
			}
		}

		/// <inheritdoc/>
		public override (FloatDictionary FloatVariables, StringDictionary StringVariables, BoolDictionary BoolVariables) GetAllVariables()
		{
			var floats = new FloatDictionary();
			var strings = new StringDictionary();
			var bools = new BoolDictionary();

			GetVariables(m_numberMap, floats);
			GetVariables(m_stringMap, strings);
			GetVariables(m_boolMap, bools);

			return (floats, strings, bools);
		}

		private void GetVariables<T, K>(Dictionary<string, K> map, Dictionary<string, T> target)
			where K : SaveVariable<T>
		{
			foreach (var (yarnName, saveVar) in map)
			{
				// Retrieve the value as object so we can dispatch on its
				// runtime type without risking an InvalidCastException.
				if (!SaveManager.TryGetValue<T>(saveVar, out var value) || value == null)
					continue;

				target[yarnName] = value;
			}
		}

		// -----------------------------------------------------------------
		// External change propagation
		// -----------------------------------------------------------------

		/// <summary>
		/// Called when <see cref="SaveManager"/> reports a variable change
		/// (e.g. from another system writing to the same save slot).
		/// Propagates the change to any Yarn change listeners registered for
		/// the corresponding Yarn variable.
		/// </summary>
		private void OnSaveManagerVariableChanged(VariableEventArgs e)
		{
			if (!m_saveIdToYarnName.TryGetValue(e.variableId, out var yarnName))
			{
				return;
			}

			// Dispatch to the correctly typed NotifyVariableChanged overload
			// so that Yarn's typed Action<T> delegates receive the right type.
			switch (e.value)
			{
				case float f:
					NotifyVariableChanged(yarnName, f);
					break;

				case double d:
					NotifyVariableChanged(yarnName, (float)d);
					break;

				case int i:
					NotifyVariableChanged(yarnName, (float)i);
					break;

				case string s:
					NotifyVariableChanged(yarnName, s);
					break;

				case bool b:
					NotifyVariableChanged(yarnName, b);
					break;
			}
		}

		// -----------------------------------------------------------------
		// Helpers
		// -----------------------------------------------------------------

		/// <summary>
		/// Builds (or rebuilds) the reverse lookup from
		/// <c>saveVariable.id → Yarn variable name</c>.
		/// Call this if the map is modified at runtime.
		/// </summary>
		public void RebuildReverseLookup()
		{
			m_saveIdToYarnName.Clear();

			foreach (var (yarnName, saveVar) in m_numberMap)
			{
				if (saveVar == null || string.IsNullOrWhiteSpace(saveVar.id))
				{
					continue;
				}

				if (!m_saveIdToYarnName.TryAdd(saveVar.id, yarnName))
				{
					Debug.LogWarning(
						$"[SaveVariableStorage] Save variable id '{saveVar.id}' is mapped " +
						$"to more than one Yarn variable. Only the first mapping will " +
						$"receive external change notifications.", this);
				}
			}
		}
	}
}