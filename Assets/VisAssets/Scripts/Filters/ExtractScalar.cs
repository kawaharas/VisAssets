using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets.SciVis.Structured.ExtracterScalar
{
#if UNITY_EDITOR
	[CustomEditor(typeof(ExtractScalar))]
	public class ExtractScalarEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var extractScalar = target as ExtractScalar;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.Space();

			var label = new GUIContent("Channel");
			GUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.Width(80f));
			var selectedChannel = EditorGUILayout.Popup("", extractScalar.channel, extractScalar.varNames);
			GUILayout.EndHorizontal();
			selectedChannel = Mathf.Clamp(selectedChannel, 0, extractScalar.varNames.Length);

			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
				extractScalar.SetChannel(selectedChannel);
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	[DisallowMultipleComponent]
	public class ExtractScalar : FilterModuleTemplate
	{
		public int      channel  = 0;
		public string[] varNames = { };

		public override void InitModule()
		{
			df.CreateElements(1);
		}

		public override int BodyFunc()
		{
			df.elements[0] = pdf.elements[channel].Clone();
			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale  = pdf.scale;
			df.offset = pdf.offset;

			return 1;
		}

		public override void ReSetParameters() // runs when parent was updated
		{
			// データセットが変更になった場合: channnelの初期が必要
			// タイムステップが変更になった場合: channnelの初期は不要
//			channel = 0;

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
			channel = Mathf.Clamp(channel, 0, pdf.elements.Length - 1);
		}

		public void SetChannel(int element_id)
		{
			if (!IsDataLoadedToParent()) return;

			channel = element_id;

			ParameterChanged();
		}

		public override void ResetUI()
		{
			var dropdown = UIPanel.transform.Find("ChannelSelector/Dropdown").GetComponent<Dropdown>();
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
			dropdown.interactable = true;
			dropdown.RefreshShownValue();
		}
	}
}