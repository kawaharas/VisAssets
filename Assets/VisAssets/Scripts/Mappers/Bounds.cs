using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets
{
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Bounds))]
	public class BoundsEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var bounds = target as Bounds;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			var color = EditorGUILayout.ColorField("Color:", bounds.color);
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Bounds");
				bounds.SetColor(color);
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class Bounds : MapperModuleTemplate
	{
		List<Vector3> vertices;
		List<Color>   colors;

		[SerializeField]
		public Color color;
		Material material;
		Mesh mesh;
		DataElement[] elements;

		int[] indices = new int[24]
		{
			0, 1, 2, 3, 0, 2, 1, 3,
			4, 5, 6, 7, 4, 6, 5, 7,
			0, 4, 1, 5, 2, 6, 3, 7
		};

		public override void InitModule()
		{
			vertices = new List<Vector3>();
			colors = new List<Color>();
			color = new Color(1f, 1f, 1f);
			material = new Material(Shader.Find("Sprites/Default"));

			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			var meshFilter = GetComponent<MeshFilter>();
			meshFilter.mesh = mesh;
			meshFilter.hideFlags = HideFlags.HideInInspector;
			var meshRenderer = GetComponent<MeshRenderer>();
			meshRenderer.material = material;
			meshRenderer.hideFlags = HideFlags.HideInInspector;
		}

		public override int BodyFunc()
		{
			Calc();

			return 1;
		}

		public override void ReSetParameters()
		{
			elements = pdf.elements;
		}

		public override void SetParameters()
		{
		}

		public void Calc()
		{
			vertices.Clear();
			colors.Clear();

			float[] min = new float[3];
			float[] max = new float[3];
			for (int i = 0; i < 3; i++)
			{
				min[i] = float.MaxValue;
				max[i] = float.MinValue;
			}

			for (int n = 0; n < pdf.elements.Length; n++)
			{
				FieldType fieldType = pdf.elements[n].fieldType;
				if (fieldType == FieldType.UNIFORM)
				{
					for (int i = 0; i < 3; i++)
					{
						min[i] = Math.Min(min[i], 0);
						max[i] = Math.Max(max[i], (float)(pdf.elements[n].dims[i] - 1));
					}
				}
				else if (fieldType == FieldType.RECTILINEAR)
				{
					for (int i = 0; i < 3; i++)
					{
						min[i] = Math.Min(min[i], pdf.elements[n].coords[i].Min());
						max[i] = Math.Max(max[i], pdf.elements[n].coords[i].Max());
					}
				}
				else if (fieldType == FieldType.IRREGULAR)
				{
					float[] coord3 = pdf.elements[n].coords[3];
					for (int m = 0; m < pdf.elements[n].size; m++)
					{
						for (int i = 0; i < 3; i++)
						{
							float v = coord3[m * 3 + i];
							min[i] = Math.Min(min[i], v);
							max[i] = Math.Max(max[i], v);
						}
					}
				}
				else
				{
					// not implemented yet
				}
			}

			vertices.Add(new Vector3(min[0], min[1], min[2]));
			vertices.Add(new Vector3(max[0], min[1], min[2]));
			vertices.Add(new Vector3(min[0], min[1], max[2]));
			vertices.Add(new Vector3(max[0], min[1], max[2]));
			vertices.Add(new Vector3(min[0], max[1], min[2]));
			vertices.Add(new Vector3(max[0], max[1], min[2]));
			vertices.Add(new Vector3(min[0], max[1], max[2]));
			vertices.Add(new Vector3(max[0], max[1], max[2]));
			for (int i = 0; i < 8; i++)
			{
				colors.Add(color);
			}

			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
			mesh.RecalculateBounds();
		}

		public void SetColor(Color _color)
		{
			color = _color;

			ParameterChanged();
		}

		int GetIndex(int i, int j, int k)
		{
			int[] dims = elements[0].dims;
			int index = dims[1] * dims[0] * k + dims[0] * j + i;

			return index;
		}
	}
}
