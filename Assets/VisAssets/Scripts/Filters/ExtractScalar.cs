using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.ExtracterScalar
{
	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(ExtractScalar))]
	public class ExtractScalarEditor : Editor
	{
		SerializedProperty useOverrideRange;
		SerializedProperty overrideMin;
		SerializedProperty overrideMax;

		public void OnEnable()
		{
			useOverrideRange = serializedObject.FindProperty("useOverrideRange");
			overrideMin      = serializedObject.FindProperty("overrideMin");
			overrideMax      = serializedObject.FindProperty("overrideMax");
		}

		public override void OnInspectorGUI()
		{
			var extractScalar = target as ExtractScalar;

			if (extractScalar == null) return;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			// Style settings
			EditorStyles.popup.fontSize = 11;
			EditorStyles.popup.fixedHeight = 18f;
			EditorStyles.label.fontSize = 11;
			EditorStyles.label.fixedHeight = 18f;

			GUILayout.Space(5f);

			GUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(new GUIContent("Channel"), GUILayout.Width(80f));

			string[] displayedNames = (extractScalar.varNames != null && extractScalar.varNames.Length > 0)
				? extractScalar.varNames
				: new string[] { "No Data Loaded" };

			int currentIdx = (extractScalar.varNames != null && extractScalar.varNames.Length > 0)
				? Mathf.Clamp(extractScalar.channel, 0, extractScalar.varNames.Length - 1)
				: 0;

			int selectedIdx = EditorGUILayout.Popup("", currentIdx, displayedNames);
			
			int newChannel = extractScalar.channel;

			if (extractScalar.varNames != null && extractScalar.varNames.Length > 0)
			{
				if (selectedIdx != currentIdx)
				{
					newChannel = selectedIdx;
				}
			}

			newChannel = EditorGUILayout.IntField(newChannel, GUILayout.Width(40f));
			newChannel = Mathf.Max(0, newChannel);

			GUILayout.EndHorizontal();

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(useOverrideRange, new GUIContent("Override Min/Max"));

			GUILayout.Space(5f);

			if (useOverrideRange.boolValue)
			{
				EditorGUI.indentLevel++;

				float dMin = extractScalar.dataMin;
				float dMax = extractScalar.dataMax;
				float dRange = dMax - dMin;

				float currentMin = overrideMin.floatValue;
				float currentMax = overrideMax.floatValue;

				// Draw the slider only if data is loaded and range is valid
				if (dRange > 0f)
				{
					// Normalize current values to 0.0 - 1.0 ratio
					float normMin = Mathf.Clamp01((currentMin - dMin) / dRange);
					float normMax = Mathf.Clamp01((currentMax - dMin) / dRange);

					EditorGUILayout.MinMaxSlider(new GUIContent("Range Slider"), ref normMin, ref normMax, 0f, 1f);

					// Restore actual values
					currentMin = dMin + normMin * dRange;
					currentMax = dMin + normMax * dRange;
				}
				else
				{
					EditorGUILayout.HelpBox("Data min and max are equal.", MessageType.Warning);
				}

				GUILayout.Space(5f);

				EditorGUILayout.BeginHorizontal();

				float defaultLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 60f;

				currentMin = EditorGUILayout.FloatField(new GUIContent("Min"), currentMin, GUILayout.MinWidth(60f));
				currentMin = Mathf.Clamp(currentMin, dMin, currentMax);

				GUILayout.Space(10f);

				currentMax = EditorGUILayout.FloatField(new GUIContent("Max"), currentMax, GUILayout.MinWidth(60f));
				currentMax = Mathf.Clamp(currentMax, currentMin, dMax);

				EditorGUIUtility.labelWidth = defaultLabelWidth;

				EditorGUILayout.EndHorizontal();

				overrideMin.floatValue = currentMin;
				overrideMax.floatValue = currentMax;

				EditorGUI.indentLevel--;
			}
			else
			{
				EditorGUILayout.HelpBox($"Current Data Range: [{extractScalar.dataMin:F4}, {extractScalar.dataMax:F4}]", MessageType.Info);
			}

			GUILayout.Space(10f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ExtractScalar");
				extractScalar.SetChannel(newChannel);
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	/// <summary>
	/// Filter module that extracts a single scalar field from the parent data field.
	/// Clones the specified component and provides functionality to override its data range for downstream mapping.
	/// </summary>
	[DisallowMultipleComponent]
	public class ExtractScalar : FilterModuleTemplate
	{
		public int      channel  = 0;
		public string[] varNames = { };

		[ReadOnly] public float dataMin = 0f;
		[ReadOnly] public float dataMax = 1f;

		public bool  useOverrideRange = false;
		public float overrideMin      = 0f;
		public float overrideMax      = 1f;

		public override void InitModule()
		{
			df.CreateElements(1);
		}

		public override int BodyFunc()
		{
			if (pdf == null || pdf.elements == null || pdf.elements.Length <= channel) return 0;

			// Shallow copy the selected element metadata and arrays
			df.elements[0] = pdf.elements[channel].Clone();
			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale  = pdf.scale;
			df.offset = pdf.offset;

			// Store original actual data bounds
			dataMin = df.elements[0].min;
			dataMax = df.elements[0].max;

			// Apply user-defined min/max override for downstream color mapping
			if (useOverrideRange)
			{
				df.elements[0].min = overrideMin;
				df.elements[0].max = overrideMax;
			}

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
/*
			if (pdf != null && pdf.elements != null && pdf.elements.Length > 0)
			{
				channel = Mathf.Clamp(channel, 0, pdf.elements.Length - 1);
			}
*/		}

		public override void ResetUI()
		{
			if (UIPanel == null) return;

			var dropdownObj = UIPanel.transform.Find("ChannelSelector/Dropdown");
			if (dropdownObj == null) return;

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

			dropdown.interactable = true;
			dropdown.RefreshShownValue();
		}

		/// <summary>
		/// Sets the target extraction channel and triggers a module update if data is ready.
		/// </summary>
		public void SetChannel(int element_id)
		{
			channel = Mathf.Max(0, element_id); // Prevent negative channels

			if (IsDataLoadedToParent()) ParameterChanged();
		}

		/// <summary>
		/// Toggles the range override feature on or off.
		/// </summary>
		public void SetOverrideRangeState(bool state)
		{
			useOverrideRange = state;

			if (IsDataLoadedToParent()) ParameterChanged();
		}

		/// <summary>
		/// Explicitly sets the min and max values for the range override.
		/// Automatically enables the override state.
		/// </summary>
		public void SetOverrideRangeValues(float min, float max)
		{
			overrideMin = Mathf.Min(min, max);
			overrideMax = Mathf.Max(min, max);
			useOverrideRange = true;

			if (IsDataLoadedToParent()) ParameterChanged();
		}
	}
}