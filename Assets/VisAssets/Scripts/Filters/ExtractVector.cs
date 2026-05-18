using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.ExtractVector
{
	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(ExtractVector))]
	public class ExtractVectorEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var extractVector = target as ExtractVector;

			if (extractVector == null) return;

			if (extractVector.channels == null || extractVector.channels.Length < 3)
			{
				extractVector.channels = new int[3] { 0, 0, 0 };
			}

			if (extractVector.channelStates == null || extractVector.channelStates.Length < 3)
			{
				extractVector.channelStates = new bool[3] { false, false, false };
			}

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			EditorStyles.popup.fontSize = 11;
			EditorStyles.popup.fixedHeight = 18f;
//			EditorStyles.popup.margin = new RectOffset(0, 0, 5, 5);
			EditorStyles.label.fontSize = 11;
			EditorStyles.label.fixedHeight = 18f;

			GUILayout.Space(10f);

			string[] displayedNames = (extractVector.varNames != null && extractVector.varNames.Length > 0)
				? extractVector.varNames
				: new string[] { "No Data Loaded" };

			bool[] newStates   = new bool[3];
			int[]  newChannels = new int[3];

			for (int i = 0; i < 3; ++i)
			{
				EditorGUILayout.BeginHorizontal();

				// Toggle
				newStates[i] = EditorGUILayout.ToggleLeft($"Channel {i}", extractVector.channelStates[i], GUILayout.Width(80f));

				EditorGUI.BeginDisabledGroup(!newStates[i]);

				// Dropdown
				int currentIdx;
				if (extractVector.varNames != null && extractVector.varNames.Length > 0)
				{
					currentIdx = Mathf.Clamp(extractVector.channels[i], 0, extractVector.varNames.Length - 1);
				}
				else
				{
					currentIdx = 0;
				}

				int selectedIdx = EditorGUILayout.Popup("", currentIdx, displayedNames);

				newChannels[i] = extractVector.channels[i];

				if (extractVector.varNames != null && extractVector.varNames.Length > 0)
				{
					if (selectedIdx != currentIdx)
					{
						newChannels[i] = selectedIdx;
					}
				}

				// IntField (Direct Input)
				newChannels[i] = EditorGUILayout.IntField(newChannels[i], GUILayout.Width(40f));
				newChannels[i] = Mathf.Max(0, newChannels[i]);

				EditorGUI.EndDisabledGroup();

				EditorGUILayout.EndHorizontal();

				GUILayout.Space(10f);
			}

			EditorGUILayout.HelpBox("Grid dimensions and coordinate topologies must match across all input elements.", MessageType.Info);

			GUILayout.Space(10f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ExtractVector");

				for (int i = 0; i < 3; i++)
				{
					extractVector.SetChannel(i, newChannels[i]);
					extractVector.SetActive(i, newStates[i]);
				}

				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	[DisallowMultipleComponent]
	public class ExtractVector : FilterModuleTemplate
	{
		[SerializeField]
		public bool[] channelStates = new bool[3] { false, false, false };

		[SerializeField]
		public int[]  channels      = new int[3] { 0, 0, 0 };

		[ReadOnly]
		public string[] varNames = { };

		private List<int> activeChannels;

		[SerializeField]
		private int activeChannelNum;

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();

			channelStates = new bool[3] { false, false, false };
			channels      = new int[3] { 0, 0, 0 };
		}
#endif

		public override void InitModule()
		{
			activeChannels   = new List<int>();
			activeChannelNum = 0;

			df.CreateElements(3);
		}

		public override int BodyFunc()
		{
			if (pdf == null || pdf.elements == null) return 0;

			for (int i = 0; i < 3; i++)
			{
				// Prevent out-of-bounds if df.elements is temporarily smaller than 3
				if (df.elements == null || i >= df.elements.Length) break;

				// Prevent out-of-bounds error if a preset channel exceeds loaded data
				if (channels[i] < pdf.elements.Length)
				{
					df.elements[i] = pdf.elements[channels[i]].Clone();
				}
			}
			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale  = pdf.scale;
			df.offset = pdf.offset;

			CheckActiveElements();

			return 1;
		}

		public override void ReSetParameters()
		{
			if (pdf == null || pdf.elements == null) return;

			int varNum = pdf.elements.Length;
			varNames = new string[varNum];

			for (int i = 0; i < varNum; i++)
			{
				var varName = pdf.elements[i].varName;

				if (!string.IsNullOrEmpty(varName))
				{
					varNames[i] = varName;
				}
				else
				{
					varNames[i] = $"variable {i}";
				}
			}
		}

		public override void SetParameters()
		{
		}

		public override void ResetUI()
		{
			if (UIPanel == null) return;

			for (int n = 0; n < 3; n++)
			{
				char axis = (char)('U' + n);
				var toggleObj   = UIPanel.transform.Find($"Channels/Toggle {axis}");
				var dropdownObj = UIPanel.transform.Find($"Channel{n}/Dropdown");

				if (toggleObj == null || dropdownObj == null) continue;

				var toggle   = toggleObj.GetComponent<Toggle>();
				var dropdown = dropdownObj.GetComponent<Dropdown>();

				dropdown.ClearOptions();

				for (int i = 0; i < pdf.elements.Length; i++)
				{
					var varName = pdf.elements[i].varName;

					if (!string.IsNullOrEmpty(varName))
					{
						dropdown.options.Add(new Dropdown.OptionData { text = varName });
					}
					else
					{
						dropdown.options.Add(new Dropdown.OptionData { text = $"variable #{i}" });
					}
				}

				dropdown.interactable = toggle.isOn;
				dropdown.RefreshShownValue();
			}
		}

		public void SetChannel(int channel_id, int element_id)
		{
			channels[channel_id] = Mathf.Max(0, element_id);

			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void SetActive(int channel_id, bool state)
		{
			channelStates[channel_id] = state;

			if (IsDataLoadedToParent()) ParameterChanged();
		}

		/// <summary>
		/// Validates if all selected elements share the exact same dimensions and undefined value settings.
		/// If validation fails, downstream modules will not receive active vector data.
		/// </summary>
		private void CheckActiveElements()
		{
			// deactivates all selected elements and make a list of active elements
			activeChannelNum = 0;
			if (activeChannels == null) activeChannels = new List<int>();
			activeChannels.Clear();

			// Safety guard for null or empty elements array
			if (df.elements == null || df.elements.Length == 0) return;

			int limit = Mathf.Min(3, df.elements.Length);
			for (int i = 0; i < limit; i++)
//			for (int i = 0; i < 3; i++)
			{
				if (df.elements[i] != null)
				{
					df.elements[i].isActive = false;
				}

				if (channelStates[i])
				{
					activeChannels.Add(i);
				}
			}

			if (activeChannels.Count == 0) return;

			// check if all active elements have the same dimension
			bool isCompatible = true;
			int[] dims = new int[3] { -1, -1, -1 };

			// get variables in the first active element
			int idx = activeChannels[0];
//			if (df.elements[idx] == null || df.elements[idx].dims == null || df.elements[idx].dims.Length < 3) return;
			if (idx >= df.elements.Length || df.elements[idx] == null || df.elements[idx].dims == null || df.elements[idx].dims.Length < 3) return;

			for (int i = 0; i < 3; i++)
			{
				dims[i] = df.elements[idx].dims[i];
			}

			bool useUndef = df.elements[idx].useUndef;
			float undef   = df.elements[idx].undef;

			// compare variables in the first active element 
			// with variables in other active elements
			for (int i = 1; i < activeChannels.Count; i++)
			{
				idx = activeChannels[i];
//				if (df.elements[idx] == null || df.elements[idx].dims == null || df.elements[idx].dims.Length < 3)
				if (idx >= df.elements.Length || df.elements[idx] == null || df.elements[idx].dims == null || df.elements[idx].dims.Length < 3)
				{
					isCompatible = false;
					break;
				}

				for (int n = 0; n < 3; n++)
				{
					if (dims[n] != df.elements[idx].dims[n])
					{
						isCompatible = false;
					}
				}

				if (useUndef != df.elements[idx].useUndef || (useUndef && undef != df.elements[idx].undef))
				{
					isCompatible = false;
				}
			}

			// activate elements of the selected channels
			if (isCompatible)
			{
				for (int i = 0; i < activeChannels.Count; i++)
				{
					idx = activeChannels[i];
//					df.elements[idx].isActive = true;
					// Safety check before activation
					if (idx < df.elements.Length && df.elements[idx] != null)
					{
						df.elements[idx].isActive = true;
					}
				}

				activeChannelNum = activeChannels.Count;
			}
		}
	}
}