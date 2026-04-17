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
		public override void OnInspectorGUI()
		{
			var extractScalar = target as ExtractScalar;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			var label = new GUIContent("Channel");
			GUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.Width(80f));
			var selectedChannel = EditorGUILayout.Popup("", extractScalar.channel, extractScalar.varNames);
			GUILayout.EndHorizontal();
			selectedChannel = Mathf.Clamp(selectedChannel, 0, extractScalar.varNames.Length);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				extractScalar.SetChannel(selectedChannel);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	[DisallowMultipleComponent]
	public class ExtractScalar : FilterModuleTemplate
	{
		public int      channel  = 0;
		public string[] varNames = { };

		/// <summary>
		/// Initializes the module by creating a single element container for the scalar field.
		/// </summary>
		public override void InitModule()
		{
			df.CreateElements(1);
		}

		/// <summary>
		/// Extracts the selected scalar channel from the parent DataField and copies metadata.
		/// </summary>
		public override int BodyFunc()
		{
			df.elements[0] = pdf.elements[channel].Clone();
			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale  = pdf.scale;
			df.offset = pdf.offset;

			return 1;
		}

		/// <summary>
		/// Reinitializes variable names when the parent dataset structure changes.
		/// </summary>
		public override void ReSetParameters() // runs when parent was updated
		{
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

		/// <summary>
		/// Ensures the selected channel remains within valid bounds when parameters are updated.
		/// </summary>
		public override void SetParameters() // runs when parameters were updated
		{
			if (pdf != null && pdf.elements != null)
			{
				channel = Mathf.Clamp(channel, 0, pdf.elements.Length - 1);
			}
		}

		/// <summary>
		/// Sets the target extraction channel and triggers a module update.
		/// </summary>
		public void SetChannel(int element_id)
		{
			if (!IsDataLoadedToParent()) return;

			channel = element_id;

			ParameterChanged();
		}

		/// <summary>
		/// Resets the associated uGUI dropdown options based on the available data channels.
		/// </summary>
		public override void ResetUI()
		{
			var dropdownObj = UIPanel.transform.Find("ChannelSelector/Dropdown");
			if (dropdownObj == null) return;

			var dropdown = dropdownObj.GetComponent<Dropdown>();
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