using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets.SciVis.Structured.Outline
{
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CustomEditor(typeof(Outline))]
	public class OutlineEditor : Editor
	{
		SerializedProperty color;
		SerializedProperty drawOuterMesh;
		SerializedProperty drawInnerMesh;
		private void OnEnable()
		{
			color         = serializedObject.FindProperty("color");
			drawOuterMesh = serializedObject.FindProperty("drawOuterMesh");
			drawInnerMesh = serializedObject.FindProperty("drawInnerMesh");
		}

		public override void OnInspectorGUI()
		{
			var outline = target as Outline;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			color.colorValue = EditorGUILayout.ColorField("Color:", color.colorValue);
			GUILayout.Space(10f);
			drawOuterMesh.boolValue = EditorGUILayout.Toggle("Draw Outer Mesh", drawOuterMesh.boolValue);
			GUILayout.Space(10f);
			drawInnerMesh.boolValue = EditorGUILayout.Toggle("Draw Inner Mesh", drawInnerMesh.boolValue);
			GUILayout.Space(10f);
			if (GUILayout.Button("Load Default Values"))
			{
				color.colorValue = new Color(1f, 1f, 1f, 0.3f);
				drawOuterMesh.boolValue = false;
				drawInnerMesh.boolValue = false;
			}
			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Outline");
				if (EditorApplication.isPlaying)
				{
					outline.SetColor(color.colorValue);
					outline.SetStateOuterMesh(drawOuterMesh.boolValue);
					outline.SetStateInnerMesh(drawInnerMesh.boolValue);
				}
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class Outline : MapperModuleTemplate
	{
		// accessors for input data loaded to the parent module
		DataElement   element;
		int []        dims;
		float [][]    coords;

		List<Vector3> vertices;
		List<Color>   colors;
		List<int>     indices;
		int           vertexCount;
		Mesh          mesh;
		Material      material;

		[SerializeField]
		public Color  color = new Color(1f, 1f, 1f, 0.3f);
		[SerializeField]
		public bool   drawOuterMesh;
		[SerializeField]
		public bool   drawInnerMesh;

		public override void InitModule()
		{
			vertices = new List<Vector3>();
			colors   = new List<Color>();
			indices  = new List<int>();
			material = new Material(Shader.Find("Sprites/Default"));

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
			dims    = element.dims;
			coords  = element.coords;
		}

		public void Calc()
		{
			vertices.Clear();
			colors.Clear();
			indices.Clear();
			vertexCount = 0;

			FieldType fieldType = element.fieldType;

			if ((fieldType == FieldType.UNIFORM) ||
				(fieldType == FieldType.RECTILINEAR))
			{
				for (int axis = 0; axis < 3; axis++)
				{
					int NI, NJ;

					if (axis == 0)
					{
						NI = dims[1];
						NJ = dims[2];
					}
					else if (axis == 1)
					{
						NI = dims[0];
						NJ = dims[2];
					}
					else
					{
						NI = dims[1];
						NJ = dims[0];
					}

					for (int j = 0; j < NJ; j++)
					{
						for (int i = 0; i < NI; i++)
						{
							if ((j == 0) || (j == NJ - 1))
							{
								if ((i != 0) && (i != NI - 1))
								{
									if (drawOuterMesh)
									{
										if (axis == 0)
										{
											AddLine(0, i, j, dims[axis] - 1, i, j);
										}
										else if (axis == 1)
										{
											AddLine(i, 0, j, i, dims[axis] - 1, j);
										}
										else
										{
											AddLine(i, j, 0, i, j, dims[axis] - 1);
										}
									}
								}
								else
								{
									if (axis == 0)
									{
										AddLine(0, i, j, dims[axis] - 1, i, j);
									}
									else if (axis == 1)
									{
										AddLine(i, 0, j, i, dims[axis] - 1, j);
									}
									else
									{
										AddLine(i, j, 0, i, j, dims[axis] - 1);
									}
								}
							}
							else
							{
								if ((i == 0) || (i == NI - 1))
								{
									if (drawOuterMesh)
									{
										if (axis == 0)
										{
											AddLine(0, i, j, dims[axis] - 1, i, j);
										}
										else if (axis == 1)
										{
											AddLine(i, 0, j, i, dims[axis] - 1, j);
										}
										else
										{
											AddLine(i, j, 0, i, j, dims[axis] - 1);
										}
									}
								}
								else
								{
									if (drawInnerMesh)
									{
										if (axis == 0)
										{
											AddLine(0, i, j, dims[axis] - 1, i, j);
										}
										else if (axis == 1)
										{
											AddLine(i, 0, j, i, dims[axis] - 1, j);
										}
										else
										{
											AddLine(i, j, 0, i, j, dims[axis] - 1);
										}
									}
								}
							}
						}
					}
				}
			}
			else if (fieldType == FieldType.IRREGULAR)
			{
				for (int axis = 0; axis < 3; axis++)
				{
					int NI, NJ;

					if (axis == 0)
					{
						NI = dims[1];
						NJ = dims[2];
					}
					else if (axis == 1)
					{
						NI = dims[0];
						NJ = dims[2];
					}
					else
					{
						NI = dims[1];
						NJ = dims[0];
					}

					for (int j = 0; j < NJ; j++)
					{
						for (int i = 0; i < NI; i++)
						{
							if ((j == 0) || (j == NJ - 1))
							{
								if ((i != 0) && (i != NI - 1))
								{
									if (drawOuterMesh)
									{
										for (int n = 0; n < dims[axis] - 2; n++)
										{
											if (axis == 0)
											{
												AddLine2(n, i, j, n + 1, i, j);
											}
											else if (axis == 1)
											{
												AddLine2(i, n, j, i, n + 1, j);
											}
											else
											{
												AddLine2(i, j, n, i, j, n + 1);
											}
										}
									}
								}
								else
								{
									for (int n = 0; n < dims[axis] - 2; n++)
									{
										if (axis == 0)
										{
											AddLine2(n, i, j, n + 1, i, j);
										}
										else if (axis == 1)
										{
											AddLine2(i, n, j, i, n + 1, j);
										}
										else
										{
											AddLine2(i, j, n, i, j, n + 1);
										}
									}
								}
							}
							else
							{
								if ((i == 0) || (i == NI - 1))
								{
									if (drawOuterMesh)
									{
										for (int n = 0; n < dims[axis] - 2; n++)
										{
											if (axis == 0)
											{
												AddLine2(n, i, j, n + 1, i, j);
											}
											else if (axis == 1)
											{
												AddLine2(i, n, j, i, n + 1, j);
											}
											else
											{
												AddLine2(i, j, n, i, j, n + 1);
											}
										}
									}
								}
								else
								{
									if (drawInnerMesh)
									{
										for (int n = 0; n < dims[axis] - 2; n++)
										{
											if (axis == 0)
											{
												AddLine2(n, i, j, n + 1, i, j);
											}
											else if (axis == 1)
											{
												AddLine2(i, n, j, i, n + 1, j);
											}
											else
											{
												AddLine2(i, j, n, i, j, n + 1);
											}
										}
									}
								}
							}
						}
					}
				}
			}
			else
			{
				// not implemented yet
			}

			mesh.Clear(); // for safety : mesh must be clear every time it is recalculated.
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

		public void SetStateOuterMesh(bool state)
		{
			drawOuterMesh = state;
			
			ParameterChanged();
		}

		public void SetStateInnerMesh(bool state)
		{
			drawInnerMesh = state;
			
			ParameterChanged();
		}

		void AddLine(int i0, int j0, int k0, int i1, int j1, int k1)
		{
			// for uniform and rectilinear
			vertices.Add(new Vector3(coords[0][i0], coords[1][j0], coords[2][k0]));
			vertices.Add(new Vector3(coords[0][i1], coords[1][j1], coords[2][k1]));
			colors.Add(color);
			colors.Add(color);
			indices.Add(vertexCount);
			indices.Add(vertexCount + 1);
			vertexCount += 2;
		}

		void AddLine2(int i0, int j0, int k0, int i1, int j1, int k1)
		{
			// for irregular
			int idx0 = GetIndex(i0, j0, k0) * 3;
			int idx1 = GetIndex(i1, j1, k1) * 3;
			vertices.Add(new Vector3(coords[3][idx0], coords[3][idx0 + 1], coords[3][idx0 + 2]));
			vertices.Add(new Vector3(coords[3][idx1], coords[3][idx1 + 1], coords[3][idx1 + 2]));
			colors.Add(color);
			colors.Add(color);
			indices.Add(vertexCount);
			indices.Add(vertexCount + 1);
			vertexCount += 2;
		}
	}
}
