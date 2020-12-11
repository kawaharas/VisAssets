using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CustomEditor(typeof(ExtractVector))]
	public class ExtractVectorEditor : Editor
	{
		SerializedProperty activeChannelNum;
		bool[] channelStates;
		int[]  channels;
		int[]  selectedChannels;

		public void OnEnable()
		{
			channelStates = new bool[3];
			channels = new int[3];
			selectedChannels = new int[3];
			for (int i = 0; i < 3; i++)
			{
				channelStates[i] = false;
				channels[i] = 0;
				selectedChannels[i] = 0;
			}
			activeChannelNum = serializedObject.FindProperty("activeChannelNum");
		}

		public override void OnInspectorGUI()
		{
			var extractVector = target as ExtractVector;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorStyles.popup.fontSize = 11;
			EditorStyles.popup.fixedHeight = 18f;
			EditorStyles.popup.alignment = TextAnchor.MiddleLeft;
//			EditorStyles.popup.margin = new RectOffset(0, 0, 5, 5);
			EditorStyles.label.fontSize = 11;
			EditorStyles.label.fixedHeight = 18f;
			EditorStyles.label.alignment = TextAnchor.MiddleLeft;

			//			GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));

			/*
						GUIContent[] labels = new GUIContent[3];
						int[] channels = new int[3];
						bool[] states = new bool[3];
						GUILayout.Space(10f);

						for (int i = 0; i < extractVector.channelStates.Length; ++i)
						{
							labels[i] = new GUIContent("Channel " + i);
							EditorGUILayout.BeginHorizontal();
			//				{
								states[i] = EditorGUILayout.ToggleLeft(labels[i], extractVector.channelStates[i], GUILayout.Width(95f));
								EditorGUI.BeginDisabledGroup(!extractVector.channelStates[i]);
								channels[i] = EditorGUILayout.Popup("", extractVector.channels[i], extractVector.varNames);
								EditorGUI.EndDisabledGroup();
			//				}
							EditorGUILayout.EndHorizontal();
							GUILayout.Space(10f);
						}
			*/

			for (int i = 0; i < extractVector.channelStates.Length; ++i)
			{
				channelStates[i] = extractVector.channelStates[i];
				channels[i]      = extractVector.channels[i];
			}

			GUILayout.Space(10f);
			for (int i = 0; i < 3; ++i)
			{
				var label = new GUIContent("Channel " + i);
				EditorGUILayout.BeginHorizontal();
				channelStates[i] = EditorGUILayout.ToggleLeft(label, channelStates[i], GUILayout.Width(95f));
				EditorGUI.BeginDisabledGroup(!channelStates[0]);
				selectedChannels[i] = EditorGUILayout.Popup("", extractVector.channels[i], extractVector.varNames);
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndHorizontal();
				GUILayout.Space(10f);
			}

			var message = new GUIContent("All input elements must have the same number of grids.");
			EditorGUILayout.BeginHorizontal(GUI.skin.box);
			GUILayout.Space(5f);
			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.alignment = TextAnchor.MiddleLeft;
			style.wordWrap = true;
			style.CalcSize(message);
			if (activeChannelNum.intValue > 0)
			{
				EditorGUILayout.LabelField("", style);
			}
			else
			{
				EditorGUILayout.LabelField(message, style);
			}
			GUILayout.Space(5f);
			EditorGUILayout.EndHorizontal();
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				for (int i = 0; i < 3; i++)
				{
					extractVector.SetChannel(i, selectedChannels[i]);
					extractVector.SetActive(i, channelStates[i]);
				}

//				EditorUtility.SetDirty(extractVector);
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	[DisallowMultipleComponent]
	public class ExtractVector : FilterModuleTemplate
	{
		public bool[]   channelStates;
		public int[]    channels;
		[ReadOnly]
		public string[] varNames = { };

		bool[] previousChannelStates;
		int[]  previousChannels;

		List<int> activeChannels;
		[SerializeField]
		private int activeChannelNum; // for EditorGUI

		public override void InitModule()
		{
			channels       = new int[3] { 0, 0, 0 };
			channelStates  = new bool[3] { false, false, false };
			activeChannels = new List<int>();
			activeChannelNum = 0;
			previousChannels = new int[] { 0, 0, 0 };
			previousChannelStates = new bool[] { false, false, false };

			df.CreateElements(3);

			var transform = GetComponent<Transform>();
			transform.hideFlags = HideFlags.HideInInspector;
		}

		public override int BodyFunc()
		{
			for (int i = 0; i < 3; i++)
			{
				df.elements[i] = pdf.elements[channels[i]].Clone();
			}

			CheckActiveElements();

			return 1;
		}

		public override void ReSetParameters() // runs when parent was updated
		{
			// データセットが変更になった場合: channnel等の初期が必要
			// タイムステップが変更になった場合: channnel等の初期は不要
			// initialize variables
/*
			for (int i = 0; i < 3; i++)
			{
				channels[i] = 0;
				channelStates[i] = false;
				previousChannels[i] = 0;
				previousChannelStates[i] = false;
				df.elements[i] = pdf.elements[channels[i]].Clone();
				df.elements[i].isActive = false;
			}
*/
			// create a string list of variable names (for UI)
			int varNum = pdf.elements.Length;
			varNames = new string[varNum];
			for (int i = 0; i < varNum; i++)
			{
				var varName = pdf.elements[i].varName;
				if (varName.Length != 0)
				{
					varNames[i] = varName;
				}
				else
				{
					varNames[i] = "variable " + i.ToString();
				}
			}
		}

		public override void SetParameters() // runs when parameters were updated
		{
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

			for (int i = 0; i < 3; i++)
			{
				if (previousChannels[i] != channels[i])
				{
					previousChannels[i] = channels[i];
					SetChannel(i, channels[i]);
					break;
				}
			}
			for (int i = 0; i < 3; i++)
			{
				if (previousChannelStates[i] != channelStates[i])
				{
					previousChannelStates[i] = channelStates[i];
					SetActive(i, channelStates[i]);
					break;
				}
			}

			ParameterChanged();
		}

		public void SetChannel(int channel_id, int element_id)
		{
			if (!IsDataLoadedToParent()) return;

			channels[channel_id] = element_id;
			df.elements[channel_id] = pdf.elements[element_id].Clone();

			ParameterChanged();
		}

		public void SetActive(int channel_id, bool state)
		{
			if (!IsDataLoadedToParent()) return;

			channelStates[channel_id] = state;

			ParameterChanged();
		}

		private void CheckActiveElements()
		{
			// deactivates all selected elements and make a list of active elements
			activeChannelNum = 0;
			activeChannels.Clear();
			for (int i = 0; i < 3; i++)
			{
				df.elements[i].isActive = false;

				if (channelStates[i])
				{
					activeChannels.Add(i);
				}
			}

			// check if all active elements have the same dimension
			if (activeChannels.Count > 0)
			{
				bool check_result = true;
				int[] dims = new int[3] { -1, -1, -1};
				// get variables in the first active element
				int idx = activeChannels[0];
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
					for (int n = 0; n < 3; n++)
					{
						if (dims[n] != df.elements[idx].dims[n])
						{
							check_result = false;
						}
					}
					if (useUndef == df.elements[idx].useUndef)
					{
						if (undef != df.elements[idx].undef)
						{
							check_result = false;
						}
					}
					else
					{
						check_result = false;
					}
				}

				// activate elements of the selected channels
				if (check_result)
				{
					for (int i = 0; i < activeChannels.Count; i++)
					{
						idx = activeChannels[i];
						df.elements[idx].isActive = true;
					}
					activeChannelNum = activeChannels.Count;
				}
			}
		}

		public override void ResetUI()
		{
			for (int n = 0; n < 3; n++)
			{
				char axis = (char)('U' + n);
				string obj_name = "Channels/Toggle " + axis.ToString();
				var toggle = UIPanel.transform.Find(obj_name).GetComponent<Toggle>();
//				toggle.isOn = false;
//			}

//			for (int n = 0; n < 3; n++)
//			{
//				string obj_name = "Channel" + n.ToString() + "/Dropdown";
				obj_name = "Channel" + n.ToString() + "/Dropdown";
				var dropdown = UIPanel.transform.Find(obj_name).GetComponent<Dropdown>();
				dropdown.ClearOptions();
				for (int i = 0; i < pdf.elements.Length; i++)
				{
					if (pdf.elements[i].varName == "")
					{
						dropdown.options.Add(new Dropdown.OptionData { text = "variable #" + i.ToString() });
					}
					else
					{
						dropdown.options.Add(new Dropdown.OptionData { text = pdf.elements[i].varName });
					}
				}
//				dropdown.interactable = true;
				dropdown.interactable = toggle.isOn;
				dropdown.RefreshShownValue();
			}
		}
	}
}
