using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(Sample))]
public class SampleInspector : Editor
{
	// OnInspectorGUI
	public override void OnInspectorGUI()
	{
		// update
		serializedObject.Update();

		var sample = this.target as Sample;
		{
			// count
			sample.m_Count = EditorGUILayout.IntField("Count", sample.m_Count);

			// array count
			drawArrayProperty("m_CountArray");

			// delta time
			sample.m_DeltaTime = EditorGUILayout.FloatField("DeltaTime", sample.m_DeltaTime);
		}

		serializedObject.ApplyModifiedProperties();
	}

	// draw array property
	private void drawArrayProperty(string prop_name)
	{
//		EditorGUIUtility.LookLikeInspector();
		EditorGUIUtility.labelWidth = 0;
		EditorGUIUtility.fieldWidth = 0;
		SerializedProperty prop = this.serializedObject.FindProperty(prop_name);
		EditorGUI.BeginChangeCheck();
		EditorGUILayout.PropertyField(prop, true);
		if (EditorGUI.EndChangeCheck())
		{
		}
//		EditorGUIUtility.LookLikeControls();
//		EditorGUIUtility.labelWidth = 25;
//		EditorGUIUtility.fieldWidth = 50;
	}
}
#endif

public class Sample : MonoBehaviour
{
	// member
	public int m_Count;
	public int[] m_CountArray = new int[4];
	public float m_DeltaTime;
}
