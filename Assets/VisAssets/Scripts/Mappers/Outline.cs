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

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Outline))]
	public class OutlineEditor : Editor
	{
		SerializedProperty color;
		SerializedProperty drawOuterMesh;
		SerializedProperty drawInnerMesh;
		SerializedProperty lineShader;

		private void OnEnable()
		{
			color         = serializedObject.FindProperty("color");
			drawOuterMesh = serializedObject.FindProperty("drawOuterMesh");
			drawInnerMesh = serializedObject.FindProperty("drawInnerMesh");
			lineShader    = serializedObject.FindProperty("lineShader");
		}

		public override void OnInspectorGUI()
		{
			var outline = target as Outline;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			color.colorValue = EditorGUILayout.ColorField("Color:", color.colorValue);

			GUILayout.Space(5f);

			drawOuterMesh.boolValue = EditorGUILayout.Toggle("Draw Outer Mesh", drawOuterMesh.boolValue);

			GUILayout.Space(5f);

			drawInnerMesh.boolValue = EditorGUILayout.Toggle("Draw Inner Mesh", drawInnerMesh.boolValue);

			GUILayout.Space(5f);

			if (lineShader != null)
			{
				EditorGUILayout.PropertyField(lineShader, new GUIContent("Material Shader"));
			}

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			if (GUILayout.Button("Load Default Values"))
			{
				color.colorValue = new Color(1f, 1f, 1f, 0.3f);
				drawOuterMesh.boolValue = false;
				drawInnerMesh.boolValue = false;
			}

			GUILayout.Space(5f);

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

	// =========================================================================
	// Main Class
	// =========================================================================
	public class Outline : MapperModuleTemplate
	{
		private DataElement   element;
		private int[]         dims;
		private float[][]     coords;

		private List<Vector3> vertices;
		private List<Color>   colors;
		private List<int>     indices;
		private int           vertexCount;
		private Mesh          mesh;
		private Material      material;

		[SerializeField]
		public Color color = new Color(1f, 1f, 1f, 0.3f);

		[SerializeField]
		public Shader lineShader;

		[SerializeField]
		public bool drawOuterMesh;

		[SerializeField]
		public bool drawInnerMesh;

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();

			EnsureCorrectShader();
		}
#endif

#if UNITY_EDITOR
		/// <summary>
		/// Automatically detects the current Render Pipeline and returns the appropriate default shader.
		/// </summary>
		private void EnsureCorrectShader()
		{
			bool isURP = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null;
			string expectedShaderName = isURP ? "Universal Render Pipeline/Unlit" : "Sprites/Default";

			if (lineShader == null || lineShader.name != expectedShaderName)
			{
				lineShader = Shader.Find(expectedShaderName);
			}
		}
#endif

		private void OnValidate()
		{
#if UNITY_EDITOR
			EnsureCorrectShader();
#endif
		}

		public override void InitModule()
		{
			vertices = new List<Vector3>();
			colors   = new List<Color>();
			indices  = new List<int>();

#if UNITY_EDITOR
			EnsureCorrectShader();
#else
			if (lineShader == null)
			{
				bool isURP = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null;
				lineShader = Shader.Find(isURP ? "Universal Render Pipeline/Unlit" : "Sprites/Default");
			}
#endif
			material = new Material(lineShader);

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

		/// <summary>
		/// Calculates the vertices and indices for the inner and outer grid lines.
		/// </summary>
		public void Calc()
		{
			vertices.Clear();
			colors.Clear();
			indices.Clear();
			vertexCount = 0;

			FieldType fieldType = element.fieldType;
			bool isIrregular = (fieldType == FieldType.IRREGULAR);

			if (fieldType == FieldType.UNIFORM || fieldType == FieldType.RECTILINEAR || isIrregular)
			{
				for (int axis = 0; axis < 3; axis++)
				{
					// Determine the other two axes defining the cross-section
					int dimA = (axis == 0) ? 1 : 0;
					int dimB = (axis == 2) ? 1 : 2;

					int sizeA = dims[dimA];
					int sizeB = dims[dimB];

					for (int b = 0; b < sizeB; b++)
					{
						for (int a = 0; a < sizeA; a++)
						{
							bool isEdge  = (a == 0 || a == sizeA - 1) && (b == 0 || b == sizeB - 1);
							bool isOuter = (a == 0 || a == sizeA - 1) || (b == 0 || b == sizeB - 1);

							// Edges are always drawn. Outer and Inner lines depend on UI toggles.
							bool shouldDraw = isEdge || (isOuter && drawOuterMesh) || (!isOuter && drawInnerMesh);

							if (shouldDraw)
							{
								int[] idx0 = new int[3];
								int[] idx1 = new int[3];

								// Assign generalized cross-section coordinates
								idx0[dimA] = a; idx0[dimB] = b;
								idx1[dimA] = a; idx1[dimB] = b;

								if (!isIrregular)
								{
									idx0[axis] = 0;
									idx1[axis] = dims[axis] - 1;
									AddLine(idx0[0], idx0[1], idx0[2], idx1[0], idx1[1], idx1[2]);
								}
								else
								{
									for (int n = 0; n < dims[axis] - 1; n++)
									{
										idx0[axis] = n;
										idx1[axis] = n + 1;
										AddLine2(idx0[0], idx0[1], idx0[2], idx1[0], idx1[1], idx1[2]);
									}
								}
							}
						}
					}
				}
			}

			// For safety: mesh must be cleared every time it is recalculated.
			mesh.Clear();
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
			mesh.RecalculateBounds();
		}

		/// <summary>
		/// Updates the solid color of the outline mesh and triggers a parameter change event.
		/// </summary>
		public void SetColor(Color _color)
		{
			color = _color;

			ParameterChanged();
		}

		/// <summary>
		/// Retrieves the 1D array index corresponding to 3D grid coordinates.
		/// </summary>
		private int GetIndex(int i, int j, int k)
		{
			int[] d = element.dims;
			int index = d[1] * d[0] * k + d[0] * j + i;

			return index;
		}

		/// <summary>
		/// Toggles the rendering of the outer boundary mesh.
		/// </summary>
		public void SetStateOuterMesh(bool state)
		{
			drawOuterMesh = state;

			ParameterChanged();
		}

		/// <summary>
		/// Toggles the rendering of the inner grid mesh.
		/// </summary>
		public void SetStateInnerMesh(bool state)
		{
			drawInnerMesh = state;

			ParameterChanged();
		}

		/// <summary>
		/// Adds a line segment for Uniform and Rectilinear grid types.
		/// </summary>
		private void AddLine(int i0, int j0, int k0, int i1, int j1, int k1)
		{
			vertices.Add(new Vector3(coords[0][i0], coords[1][j0], coords[2][k0]));
			vertices.Add(new Vector3(coords[0][i1], coords[1][j1], coords[2][k1]));
			colors.Add(color);
			colors.Add(color);
			indices.Add(vertexCount);
			indices.Add(vertexCount + 1);
			vertexCount += 2;
		}

		/// <summary>
		/// Adds a line segment for Irregular grid types.
		/// </summary>
		private void AddLine2(int i0, int j0, int k0, int i1, int j1, int k1)
		{
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