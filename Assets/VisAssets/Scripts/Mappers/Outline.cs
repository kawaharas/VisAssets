using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Outline))]
	public class OutlineEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var outline = target as Outline;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			var color = EditorGUILayout.ColorField("Color:", outline.color);
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Outline");
				outline.SetColor(color);
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class Outline : MapperModuleTemplate
	{
		List<Vector3> vertices;
		List<Color>   colors;

		[SerializeField]
		public Color color;
		Material material;
		Mesh mesh;
		DataElement element;

		int[] indices = new int[24]
		{
			0, 1, 2, 3, 0, 2, 1, 3,
			4, 5, 6, 7, 4, 6, 5, 7,
			0, 4, 1, 5, 2, 6, 3, 7
		};

		public override void InitModule()
		{
			vertices = new List<Vector3>();
			colors   = new List<Color>();
			color    = new Color(1f, 1f, 1f);
			material = new Material(Shader.Find("Sprites/Default"));

			var transform = GetComponent<Transform>();
			transform.hideFlags = HideFlags.HideInInspector;
			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			var meshFilter   = GetComponent<MeshFilter>();
			meshFilter.mesh  = mesh;
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
			element = pdf.elements[0];
		}

		public override void SetParameters()
		{
		}

		public void Calc()
		{
			vertices.Clear();
			colors.Clear();

			FieldType fieldType = element.fieldType;
			if (fieldType == FieldType.UNIFORM)
			{
				float[] min = new float[3];
				float[] max = new float[3];
				for (int i = 0; i < 3; i++)
				{
					min[i] = 0;
					max[i] = (float)(element.dims[i] - 1);
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
			}
			else if (fieldType == FieldType.RECTILINEAR)
			{
				float[] min = new float[3];
				float[] max = new float[3];
				for (int i = 0; i < 3; i++)
				{
					min[i] = Math.Min(float.MaxValue, element.coords[i].Min());
					max[i] = Math.Max(float.MinValue, element.coords[i].Max());
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
			}
			else if (fieldType == FieldType.IRREGULAR)
			{
				float[] coord3 = element.coords[3];
				int[] dims = element.dims;
				List<int> lines = new List<int>();
				int line_id = 0;
				int idx = 0;

				for (int axis = 0; axis < 3; axis++)
				{
					for (int n = 0; n < 4; n++)
					{
						for (int i = 0; i < dims[axis]; i++)
						{
							if (axis == 0)
							{
								if (n == 0)
								{
									idx = GetIndex(i, 0, 0);
								}
								if (n == 1)
								{
									idx = GetIndex(i, 0, dims[2] - 1);
								}
								if (n == 2)
								{
									idx = GetIndex(i, dims[1] - 1, 0);
								}
								if (n == 3)
								{
									idx = GetIndex(i, dims[1] - 1, dims[2] - 1);
								}
							}
							else if (axis == 1)
							{
								if (n == 0)
								{
									idx = GetIndex(0, i, 0);
								}
								if (n == 1)
								{
									idx = GetIndex(0, i, dims[2] - 1);
								}
								if (n == 2)
								{
									idx = GetIndex(dims[0] - 1, i, 0);
								}
								if (n == 3)
								{
									idx = GetIndex(dims[0] - 1, i, dims[2] - 1);
								}
							}
							else
							{
								if (n == 0)
								{
									idx = GetIndex(0, 0, i);
								}
								if (n == 1)
								{
									idx = GetIndex(0, dims[1] - 1, i);
								}
								if (n == 2)
								{
									idx = GetIndex(dims[0] - 1, 0, i);
								}
								if (n == 3)
								{
									idx = GetIndex(dims[0] - 1, dims[1] - 1, i);
								}
							}
							float v0 = coord3[idx * 3 + 0];
							float v1 = coord3[idx * 3 + 1];
							float v2 = coord3[idx * 3 + 2];
							vertices.Add(new Vector3(v0, v1, v2));
							colors.Add(color);
							if (i != dims[axis] - 1)
							{
								lines.Add(line_id);
								lines.Add(line_id + 1);
								line_id++;
							}
						}
						line_id++;
					}
				}

				indices = lines.ToArray();
			}
			else
			{
				// not implemented yet
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
			int[] dims = element.dims;
			int index = dims[1] * dims[0] * k + dims[0] * j + i;

			return index;
		}
	}
}
