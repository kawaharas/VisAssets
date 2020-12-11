using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CustomEditor(typeof(DataField))]
	public class DataFieldEditor : Editor
	{
		SerializedProperty dataType;
		SerializedProperty dataLoaded;
		SerializedProperty elementsInfo;
		SerializedProperty selectedElementInfo;
		int currentIndex;

		private void OnEnable()
		{
			dataType = serializedObject.FindProperty("dataType");
			dataLoaded = serializedObject.FindProperty("dataLoaded");
			elementsInfo = serializedObject.FindProperty("elements");
		}

		public override void OnInspectorGUI()
		{
			var df = target as DataField;

			serializedObject.Update();

			if (elementsInfo != null && elementsInfo.arraySize != 0)
			{
				EditorGUILayout.Space();

				GUILayout.Space(3f);
				EditorGUILayout.PropertyField(dataType, true);
				GUILayout.Space(3f);
				EditorGUILayout.PropertyField(dataLoaded, true);
				//				GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
				GUILayout.Space(5f);

				currentIndex = EditorGUILayout.IntSlider("Element ID: ", currentIndex, 0, elementsInfo.arraySize - 1);
				GUILayout.Space(3f);
				selectedElementInfo = elementsInfo.GetArrayElementAtIndex(currentIndex);
				if (selectedElementInfo != null)
				{
					EditorGUILayout.PropertyField(selectedElementInfo, true);
					selectedElementInfo.isExpanded = true;
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	[System.Serializable, DisallowMultipleComponent]
	public class DataField : MonoBehaviour
	{
		public enum DataType
		{
			UNDEFINED,
			RAW,
			FILTERED
		}

		[SerializeField, ReadOnly]
		public DataType dataType = DataType.UNDEFINED;
		[SerializeField, ReadOnly]
		public bool dataLoaded = false;
		[SerializeField, ReadOnly]
		public DataElement[] elements;

		public void CreateElements(int n)
		{
			elements = new DataElement[n];
			for (int i = 0; i < n; i++)
			{
				elements[i] = new DataElement();
			}
		}

		public void DisposeElements()
		{
			if (elements != null)
			{
				Array.Clear(elements, 0, elements.Length);
			}
			dataLoaded = false;
		}
	}
}