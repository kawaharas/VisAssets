using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets
{
	using ModuleState = Activation.ModuleState;
	using ModuleType  = ModuleTemplate.ModuleType;

#if UNITY_EDITOR
	[CustomEditor(typeof(Activation))]
	public class ActivationEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var activation = target as Activation;
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			if (GUILayout.Button("Force Module Update"))
			{
				activation.SetParameterChanged(1);
			}
			EditorGUILayout.Space();

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class Activation : MonoBehaviour
	{
		public enum ModuleState
		{
			UNCHANGED = 0,
			PARAMETER_CHANGED,
			DATAFIELD_CHANGED,
			TIMESTEP_CHANGED,
			UNDEFINED
		}

		[ReadOnly]
		public ModuleType moduleType = ModuleType.UNDEFINED;

		public int parent_changed    = (int)(ModuleState.UNCHANGED);
		public int parameter_changed = (int)(ModuleState.UNCHANGED);

		public void SetModuleType(ModuleType type)
		{
			moduleType = type;
		}

		public void SetParentChanged(int i)
		{
			parent_changed = i;
		}
		public int  GetParentChanged()
		{
			return parent_changed;
		}

		public void SetParameterChanged(int i)
		{
			parameter_changed = i;
		}

		public int GetParameterChanged()
		{
			return parameter_changed;
		}
	}
}