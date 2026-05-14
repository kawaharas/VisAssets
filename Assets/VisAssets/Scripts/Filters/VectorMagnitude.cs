using System;
using System.Collections.Generic;
using UnityEngine;
using VisAssets.SciVis.Structured.Common;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.VectorMagnitude
{
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	/// <summary>
	/// Custom editor for the VectorMagnitude module.
	/// Provides UI for vector component selection, toggling, and data range overriding.
	/// </summary>
	[CustomEditor(typeof(VectorMagnitude))]
	public class VectorMagnitudeEditor : Editor
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
			var vectorMagnitude = target as VectorMagnitude;

			if (vectorMagnitude == null) return;

			if (vectorMagnitude.channels == null || vectorMagnitude.channels.Length < 3)
			{
				vectorMagnitude.channels = new int[3] { 0, 0, 0 };
			}

			if (vectorMagnitude.channelStates == null || vectorMagnitude.channelStates.Length < 3)
			{
				vectorMagnitude.channelStates = new bool[3] { false, false, false };
			}

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			// Style settings
			EditorStyles.popup.fontSize = 11;
			EditorStyles.popup.fixedHeight = 18f;
			EditorStyles.label.fontSize = 11;
			EditorStyles.label.fixedHeight = 18f;

			GUILayout.Space(10f);

			string[] displayedNames;
			if (vectorMagnitude.varNames != null && vectorMagnitude.varNames.Length > 0)
			{
				displayedNames = vectorMagnitude.varNames;
			}
			else
			{
				displayedNames = new string[] { "No Data Loaded" };
			}

			bool[] newStates   = new bool[3];
			int[]  newChannels = new int[3];

			for (int i = 0; i < 3; ++i)
			{
				EditorGUILayout.BeginHorizontal();
/*
				// Toggle
				vectorMagnitude.channelStates[i] = EditorGUILayout.ToggleLeft($"Channel {i}", vectorMagnitude.channelStates[i], GUILayout.Width(95f));

				EditorGUI.BeginDisabledGroup(!vectorMagnitude.channelStates[i]);

				// Dropdown
				int currentIdx = 0;
				if (vectorMagnitude.varNames != null && vectorMagnitude.varNames.Length > 0)
				{
					currentIdx = Mathf.Clamp(vectorMagnitude.channels[i], 0, vectorMagnitude.varNames.Length - 1);
				}

				vectorMagnitude.channels[i] = EditorGUILayout.Popup("", currentIdx, displayedNames);
*/
				// Toggle
				newStates[i] = EditorGUILayout.ToggleLeft($"Channel {i}", vectorMagnitude.channelStates[i], GUILayout.Width(80f));

				EditorGUI.BeginDisabledGroup(!newStates[i]);

				// Dropdown
				int currentIdx = 0;
				if (vectorMagnitude.varNames != null && vectorMagnitude.varNames.Length > 0)
				{
					currentIdx = Mathf.Clamp(vectorMagnitude.channels[i], 0, vectorMagnitude.varNames.Length - 1);
				}

				int selectedIdx = EditorGUILayout.Popup("", currentIdx, displayedNames);

				newChannels[i] = vectorMagnitude.channels[i];

				if (vectorMagnitude.varNames != null && vectorMagnitude.varNames.Length > 0)
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

				GUILayout.Space(5f);
			}

			EditorGUILayout.HelpBox("Grid dimensions and coordinate topologies must match across all input elements.", MessageType.Info);

			GUILayout.Space(10f);

			EditorGUILayout.PropertyField(useOverrideRange, new GUIContent("Override Min/Max"));

			GUILayout.Space(5f);

			if (useOverrideRange.boolValue)
			{
				EditorGUI.indentLevel++;

				float dMin = vectorMagnitude.dataMin;
				float dMax = vectorMagnitude.dataMax;
				float dRange = dMax - dMin;

				float currentMin = overrideMin.floatValue;
				float currentMax = overrideMax.floatValue;

				// Draw the slider only if the data range is greater than zero to prevent division by zero
				if (dRange > 0f)
				{
					// 1. Convert current actual values to a normalized ratio (0.0 to 1.0)
					float normMin = Mathf.Clamp01((currentMin - dMin) / dRange);
					float normMax = Mathf.Clamp01((currentMax - dMin) / dRange);
/*
					// 2. Draw the normalized MinMaxSlider with limits from 0f to 1f
					EditorGUILayout.MinMaxSlider(new GUIContent("Range Slider"), ref normMin, ref normMax, 0f, 1f);

					// 3. Restore the actual values from the normalized slider ratio
					currentMin = dMin + normMin * dRange;
					currentMax = dMin + normMax * dRange;
*/
					// ★追加：スライダーが「マウスで直接操作された時だけ」値を反映する
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.MinMaxSlider(new GUIContent("Range Slider"), ref normMin, ref normMax, 0f, 1f);
					if (EditorGUI.EndChangeCheck())
					{
						currentMin = dMin + normMin * dRange;
						currentMax = dMin + normMax * dRange;
					}
				}
				else
				{
					EditorGUILayout.HelpBox("Data min and max are equal.", MessageType.Warning);
				}

				GUILayout.Space(5f);

				EditorGUILayout.BeginHorizontal();

				float defaultLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 60f;
/*
				currentMin = EditorGUILayout.FloatField(new GUIContent("Min"), currentMin, GUILayout.MinWidth(60f));
				currentMin = Mathf.Clamp(currentMin, dMin, currentMax);

				GUILayout.Space(10f);

				currentMax = EditorGUILayout.FloatField(new GUIContent("Max"), currentMax, GUILayout.MinWidth(60f));
				currentMax = Mathf.Clamp(currentMax, currentMin, dMax);
*/
/*
				currentMin = EditorGUILayout.FloatField(new GUIContent("Min"), currentMin, GUILayout.MinWidth(60f));
				// データの最小値(dMin)による制限を撤廃し、Maxを超えないことだけを保証する
				currentMin = Mathf.Min(currentMin, currentMax); 

				GUILayout.Space(10f);

				currentMax = EditorGUILayout.FloatField(new GUIContent("Max"), currentMax, GUILayout.MinWidth(60f));
				// データの最大値(dMax)による制限を撤廃し、Minを下回らないことだけを保証する
				currentMax = Mathf.Max(currentMin, currentMax);
*/
				currentMin = EditorGUILayout.FloatField(new GUIContent("Min"), currentMin, GUILayout.MinWidth(60f));
				GUILayout.Space(10f);
				currentMax = EditorGUILayout.FloatField(new GUIContent("Max"), currentMax, GUILayout.MinWidth(60f));

				// ★追加：データがロードされている（実際のデータ範囲が判明している）時のみクランプする
				if (vectorMagnitude.IsDataLoadedToParent())
				{
					currentMin = Mathf.Clamp(currentMin, dMin, dMax);
					currentMax = Mathf.Clamp(currentMax, dMin, dMax);
				}

				// 最小値が最大値を逆転しないように最終チェック
				currentMin = Mathf.Min(currentMin, currentMax);

				EditorGUIUtility.labelWidth = defaultLabelWidth;

				EditorGUILayout.EndHorizontal();
				overrideMin.floatValue = currentMin;
				overrideMax.floatValue = currentMax;

				EditorGUI.indentLevel--;
			}
/*
			else
			{
//				EditorGUILayout.HelpBox($"Current Data Range: [{vectorMagnitude.dataMin:F4}, {vectorMagnitude.dataMax:F4}]", MessageType.Info);
			}
*/

			GUILayout.Space(5f);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));
			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "VectorMagnitude");

				for (int i = 0; i < 3; i++)
				{
					vectorMagnitude.SetChannel(i, newChannels[i]);
					vectorMagnitude.SetActive(i, newStates[i]);
				}

				// Request recalculation only if data is loaded (prevents errors before runtime)
				if (vectorMagnitude.IsDataLoadedToParent())
				{
					vectorMagnitude.ParameterChanged();
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
	/// <summary>
	/// Filter module that calculates the magnitude of selected vector components.
	/// Supports 1D, 2D, and 3D vector magnitude calculations and outputs a single scalar field.
	/// </summary>
	[DisallowMultipleComponent]
	public class VectorMagnitude : FilterModuleTemplate
	{
		[SerializeField] public bool[] channelStates = new bool[3] { false, false, false };
		[SerializeField] public int[]  channels      = new int[3] { 0, 0, 0 };

		[ReadOnly] public string[] varNames = { };
		[ReadOnly] public float dataMin = 0f;
		[ReadOnly] public float dataMax = 1f;

		public bool  useOverrideRange = false;
		public float overrideMin      = 0f;
		public float overrideMax      = 1f;

		private List<int> activeChannels = new List<int>();

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
			if (activeChannels == null)
			{
				activeChannels = new List<int>();
			}

			df.CreateElements(1);
		}

		public override int BodyFunc()
		{
			if (pdf == null || pdf.elements == null || pdf.elements.Length < 1) return 0;

			// Identify active channels safely
			activeChannels.Clear();
			for (int i = 0; i < 3; i++)
			{
				if (channelStates[i] && channels[i] < pdf.elements.Length)
				{
					activeChannels.Add(i);
				}
			}

			if (activeChannels.Count == 0) return 0;

			// Clone the base element
			var baseElem = pdf.elements[channels[activeChannels[0]]];
			df.elements[0] = baseElem.Clone();
			df.elements[0].varName = "Magnitude";
			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale  = pdf.scale;
			df.offset = pdf.offset;

			int dataSize = baseElem.dims[0] * baseElem.dims[1] * baseElem.dims[2];
			float[] newValues = new float[dataSize];
			bool  useUndef = baseElem.useUndef;
			float undef    = baseElem.undef;
			df.elements[0].useUndef = useUndef;
			df.elements[0].undef    = undef;

			float calcMin = float.MaxValue;
			float calcMax = float.MinValue;
			double sum = 0.0;
			int validCount = 0;

			// Calculate the magnitude (scalarization)
			for (int i = 0; i < dataSize; i++)
			{
				bool isUndefLoc = false;
				float sumSq = 0f;

				foreach (int c in activeChannels)
				{
					float val = pdf.elements[channels[c]].values[i];
					if (useUndef && val == undef) { isUndefLoc = true; break; }
					sumSq += val * val;
				}

				if (isUndefLoc)
				{
					newValues[i] = undef;
				}
				else
				{
					float mag = Mathf.Sqrt(sumSq);
					newValues[i] = mag;
					if (mag < calcMin) calcMin = mag;
					if (mag > calcMax) calcMax = mag;
					sum += mag;
					validCount++;
				}
			}

			df.elements[0].values = newValues;
			this.dataMin = (validCount > 0) ? calcMin : 0f;
			this.dataMax = (validCount > 0) ? calcMax : 1f;

			if (validCount > 0)
			{
				df.elements[0].min = useOverrideRange ? overrideMin : calcMin;
				df.elements[0].max = useOverrideRange ? overrideMax : calcMax;
				df.elements[0].average = (float)(sum / validCount);
			}

			df.elements[0].isActive = true;
			return 1;
		}

		public override void ReSetParameters()
		{
			if (pdf == null || pdf.elements == null) return;

			int varNum = pdf.elements.Length;
			varNames = new string[varNum];
			for (int i = 0; i < varNum; i++)
			{
				varNames[i] = !string.IsNullOrEmpty(pdf.elements[i].varName) ? pdf.elements[i].varName : $"variable {i}";
			}
		}

		public override void SetParameters()
		{
		}

		public override void ResetUI()
		{
			if (UIPanel == null) return;
/*
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
*/
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

		public void SetOverrideRangeState(bool state)
		{
			useOverrideRange = state;

			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void SetOverrideRangeValues(float min, float max)
		{
			overrideMin = Mathf.Min(min, max);
			overrideMax = Mathf.Max(min, max);
			useOverrideRange = true;

			if (IsDataLoadedToParent()) ParameterChanged();
		}
	}
}