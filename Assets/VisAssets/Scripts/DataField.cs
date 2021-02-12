using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VIS
{
	using FieldType = DataElement.FieldType;

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

//		public float[] min;
//		public float[] max;

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

		public void CheckRegion()
		{
			if (!dataLoaded) return;

			var min = new float[3];
			var max = new float[3];
			for (int i = 0; i < 3; i++)
			{
				min[i] = float.MaxValue;
				max[i] = float.MinValue;
			}

			for (int n = 0; n < elements.Length; n++)
			{
				FieldType fieldType = elements[n].fieldType;
				if (fieldType == FieldType.UNIFORM)
				{
					for (int i = 0; i < 3; i++)
					{
						min[i] = Math.Min(min[i], 0);
						max[i] = Math.Max(max[i], (float)(elements[n].dims[i] - 1));
					}
				}
				else if (fieldType == FieldType.RECTILINEAR)
				{
					for (int i = 0; i < 3; i++)
					{
						min[i] = Math.Min(min[i], elements[n].coords[i].Min());
						max[i] = Math.Max(max[i], elements[n].coords[i].Max());
					}
				}
				else if (fieldType == FieldType.IRREGULAR)
				{
					float[] coord3 = elements[n].coords[3];
					for (int m = 0; m < elements[n].size; m++)
					{
						for (int i = 0; i < 3; i++)
						{
							float v = coord3[m * 3 + i];
							min[i] = Math.Min(min[i], v);
							max[i] = Math.Max(max[i], v);
						}
					}
				}
				else if (fieldType == FieldType.UNSTRUCTURE)
				{
					// not implemented yet
				}
				else
				{
					// not implemented yet
				}
			}
		}
	}
}