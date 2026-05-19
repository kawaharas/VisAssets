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
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
				EditorUtility.SetDirty(target);
			}
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
			TRANSFORM_CHANGED,
			CONNECTION_CHANGED,
			VISIBILITY_CHANGED,
			UNDEFINED
		}

		[ReadOnly]
		public ModuleType moduleType = ModuleType.UNDEFINED;

		public ModuleState parent_changed    = ModuleState.UNCHANGED;
		public ModuleState parameter_changed = ModuleState.UNCHANGED;

		public void SetModuleType(ModuleType type)
		{
			moduleType = type;
		}

		public void SetParentChanged(ModuleState moduleState)
		{
			parent_changed = moduleState;
		}

		public ModuleState GetParentChanged()
		{
			return parent_changed;
		}

		public void SetParameterChanged(ModuleState moduleState)
		{
			parameter_changed = moduleState;
		}

		public ModuleState GetParameterChanged()
		{
			return parameter_changed;
		}
	}
}