using System;
using System.Collections.Generic;
using UnityEngine;
using VisAssets.SciVis.Structured.Common;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.ContourLines
{
	using FieldType = DataElement.FieldType;
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(ContourLines))]
	public class ContourLinesEditor : Editor
	{
		SerializedProperty axis;
		SerializedProperty slice;
		SerializedProperty numLevels;
		SerializedProperty shift;
		SerializedProperty builtinMaterial;
		SerializedProperty urpMaterial;
		SerializedProperty useSolidColor;
		SerializedProperty solidColor;

		private void OnEnable()
		{
			axis            = serializedObject.FindProperty("sliceHelper.axis");
			slice           = serializedObject.FindProperty("sliceHelper.slice");
			numLevels       = serializedObject.FindProperty("numLevels");
			shift           = serializedObject.FindProperty("shift");
			builtinMaterial = serializedObject.FindProperty("builtinMaterial");
			urpMaterial     = serializedObject.FindProperty("urpMaterial");
			useSolidColor   = serializedObject.FindProperty("useSolidColor");
			solidColor      = serializedObject.FindProperty("solidColor");
		}

		public override void OnInspectorGUI()
		{
			var contours = target as ContourLines;

			if (contours == null) return;

			serializedObject.Update();

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (axis != null) EditorGUILayout.IntSlider(axis, 0, 2, new GUIContent("Axis: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ContourLines");
				serializedObject.ApplyModifiedProperties();
				contours.SetAxis(axis.intValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (slice != null) EditorGUILayout.Slider(slice, 0f, 1f, new GUIContent("Slice: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ContourLines");
				serializedObject.ApplyModifiedProperties();
				contours.SetSlice(slice.floatValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (numLevels != null) EditorGUILayout.IntSlider(numLevels, 1, 100, new GUIContent("Number of Levels: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ContourLines");
				serializedObject.ApplyModifiedProperties();
				contours.SetNumLevels(numLevels.intValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (shift != null) EditorGUILayout.Slider(shift, 0f, 1f, new GUIContent("Color Shift: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ContourLines");
				serializedObject.ApplyModifiedProperties();
				contours.SetColorShift(shift.floatValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(useSolidColor, new GUIContent("Use Solid Color"));

			if (contours.useSolidColor)
			{
				GUILayout.Space(5f);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(solidColor, new GUIContent("Color"));
				EditorGUI.indentLevel--;
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ContourLines");
				serializedObject.ApplyModifiedProperties();
				contours.SetUseSolidColor(contours.useSolidColor);
				contours.SetSolidColor(contours.solidColor);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(builtinMaterial, new GUIContent("Built-in Material"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(urpMaterial, new GUIContent("URP Material"));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ContourLines");
				serializedObject.ApplyModifiedProperties();
				contours.UpdateMaterialShader();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	/// <summary>
	/// Generates contour lines (isolines) on a 2D cross-section from a 3D scalar field
	/// using the Marching Squares algorithm.
	/// </summary>
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class ContourLines : MapperModuleTemplate
	{
		[SerializeField]
		public SliceHelper sliceHelper = new SliceHelper();

		[SerializeField] private Material builtinMaterial;
		[SerializeField] private Material urpMaterial;

		[Range(1, 100)]
		public int numLevels = 10;
		[SerializeField, Range(0, 1f)]
		public float shift = 0f;

		[SerializeField]
		public int[] dims;
		[SerializeField]
		public DataElement element;

		List<Vector3> vertices;
		List<Color>   colors;
		List<int>     indices;

		private Material instancedMaterial;

		public bool useSolidColor = false;
		public Color solidColor = Color.white;

		// Marching Squares Lookup Table
		// Maps the 4-bit state (0-15) of a quad's corners to the edges the contour intersects.
		// Edges: 0 (bottom), 1 (right), 2 (top), 3 (left)
		private readonly int[][] edgeTable = new int[][]
		{
			new int[] {},                // 0000
			new int[] {0, 3},            // 0001
			new int[] {0, 1},            // 0010
			new int[] {1, 3},            // 0011
			new int[] {1, 2},            // 0100
			new int[] {0, 3, 1, 2},      // 0101 (Saddle)
			new int[] {0, 2},            // 0110
			new int[] {2, 3},            // 0111
			new int[] {2, 3},            // 1000
			new int[] {0, 2},            // 1001
			new int[] {0, 1, 2, 3},      // 1010 (Saddle)
			new int[] {1, 2},            // 1011
			new int[] {1, 3},            // 1100
			new int[] {0, 1},            // 1101
			new int[] {0, 3},            // 1110
			new int[] {}                 // 1111
		};

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
		}
#endif

		public override void InitModule()
		{
			vertices  = new List<Vector3>();
			colors    = new List<Color>();
			indices   = new List<int>();

			if (sliceHelper == null) sliceHelper = new SliceHelper();
			sliceHelper.Init();

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null) meshFilter.hideFlags = HideFlags.HideInInspector;

			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null) meshRenderer.hideFlags = HideFlags.HideInInspector;

			UpdateMaterialShader();
		}

		public override int BodyFunc()
		{
			Calc();
			return 1;
		}

		public override void IdleFunc() {}
		public override void SetParameters() {}
		public override void GetParameters() {}

		public override void ReSetParameters()
		{
			if (pdf == null || pdf.elements == null || pdf.elements.Length == 0) return;

			element = pdf.elements[0];
			dims    = element.dims;

			if (sliceHelper == null) sliceHelper = new SliceHelper();
			sliceHelper.Reset(element);
		}

		public override void ResetUI() {}

		private void OnDestroy()
		{
			if (instancedMaterial != null) Destroy(instancedMaterial);
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;
			if (sliceHelper != null) sliceHelper.Validate();
			UpdateMaterialShader();
			if (activation != null) activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

		public void UpdateMaterialShader()
		{
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer == null) return;

			var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ?? UnityEngine.QualitySettings.renderPipeline;
			bool isURP = pipeline != null;

			Material targetBaseMaterial = isURP ? urpMaterial : builtinMaterial;

			if (targetBaseMaterial == null) return;

			if (instancedMaterial != null)
			{
				if (Application.isPlaying) Destroy(instancedMaterial);
				else DestroyImmediate(instancedMaterial);
			}

			instancedMaterial = new Material(targetBaseMaterial);
			meshRenderer.sharedMaterial = instancedMaterial;
		}

		Color GetColor(float level)
		{
			level += shift;
			if (level > 1f) level -= 1f;

			Color c = Color.HSVToRGB(level, 1f, 1f);
			return new Color(c.r, c.g, c.b, 1f);
		}

		public void SetAxis(int _axis)
		{
			if (sliceHelper == null) sliceHelper = new SliceHelper();
			if (sliceHelper.SetAxis(_axis))
			{
				if (IsDataLoadedToParent()) ParameterChanged();
			}
		}

		public void SetSlice(float _slice)
		{
			if (sliceHelper == null) sliceHelper = new SliceHelper();
			if (sliceHelper.SetSlice(_slice))
			{
				if (IsDataLoadedToParent()) ParameterChanged();
			}
		}

		public void SetNumLevels(int _levels)
		{
			numLevels = Mathf.Max(1, _levels);
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void SetColorShift(float _shift)
		{
			shift = _shift;
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void SetUseSolidColor(bool _use)
		{
			useSolidColor = _use;
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void SetSolidColor(Color _color)
		{
			solidColor = _color;
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void Calc()
		{
			if (element == null || element.values == null) return;

			if (vertices == null) InitModule();
			if (sliceHelper == null) sliceHelper = new SliceHelper();

			float ratio;
			int idx = sliceHelper.GetIndexOfCuttingEdge(element, out ratio);

			vertices.Clear();
			colors.Clear();
			indices.Clear();

			int slice_w, slice_h, slice_d;
			sliceHelper.GetSliceDimensions(element, out slice_w, out slice_h, out slice_d);

			// Step 1: Extract the 2D grid data (scalar values and 3D positions)
			float[,] gridValues = new float[slice_w, slice_h];
			Vector3[,] gridPositions = new Vector3[slice_w, slice_h];

			float[] values = element.values;

			for (int j = 0; j < slice_h; j++)
			{
				for (int i = 0; i < slice_w; i++)
				{
					int idx0, idx1;
					sliceHelper.GetIndices(i, j, slice_w, slice_h, slice_d, idx, out idx0, out idx1);

					float ansf = 0;
					bool is_undef = false;

					if (slice_d == 1)
					{
						ansf = values[idx0];
					}
					else
					{
						if (element.useUndef && (values[idx0] == element.undef || values[idx1] == element.undef))
						{
							ansf = element.undef;
							is_undef = true;
						}

						if (!is_undef)
						{
							if (element.fieldType == FieldType.RECTILINEAR)
							{
								ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
							}
							else if (element.fieldType == FieldType.UNIFORM || element.fieldType == FieldType.IRREGULAR)
							{
								if (sliceHelper.value == sliceHelper.value_min) ansf = values[idx0];
								else if (sliceHelper.value == sliceHelper.value_max) ansf = values[idx1];
								else ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
							}
						}
					}

					gridValues[i, j] = is_undef ? float.NaN : ansf;

					float[] coord3 = element.coords[3];
					if (slice_d == 1)
					{
						gridPositions[i, j] = new Vector3(coord3[idx0 * 3 + 0], coord3[idx0 * 3 + 1], coord3[idx0 * 3 + 2]);
					}
					else
					{
						gridPositions[i, j] = new Vector3(
							coord3[idx0 * 3 + 0] + (coord3[idx1 * 3 + 0] - coord3[idx0 * 3 + 0]) * ratio,
							coord3[idx0 * 3 + 1] + (coord3[idx1 * 3 + 1] - coord3[idx0 * 3 + 1]) * ratio,
							coord3[idx0 * 3 + 2] + (coord3[idx1 * 3 + 2] - coord3[idx0 * 3 + 2]) * ratio
						);
					}
				}
			}

			// Step 2: Determine contour levels
			float minVal = element.min;
			float maxVal = element.max;
			List<float> contourLevels = new List<float>();

			for (int l = 1; l <= numLevels; l++)
			{
				contourLevels.Add(minVal + (maxVal - minVal) * ((float)l / (numLevels + 1)));
			}

			// Step 3: Run Marching Squares for each level
			float values_diff = element.max - element.min;

			foreach (float level in contourLevels)
			{
//				float colorRatio = Mathf.Clamp((level - minVal) / values_diff, 0, 1f);
//				Color contourColor = GetColor(colorRatio);
				Color contourColor;

				if (useSolidColor)
				{
					contourColor = solidColor;
				}
				else
				{
					float colorRatio = Mathf.Clamp((level - minVal) / values_diff, 0, 1f);
					contourColor = GetColor(colorRatio);
				}

				for (int j = 0; j < slice_h - 1; j++)
				{
					for (int i = 0; i < slice_w - 1; i++)
					{
						float v0 = gridValues[i, j];
						float v1 = gridValues[i + 1, j];
						float v2 = gridValues[i + 1, j + 1];
						float v3 = gridValues[i, j + 1];

						// Skip quad if any corner is undefined
						if (float.IsNaN(v0) || float.IsNaN(v1) || float.IsNaN(v2) || float.IsNaN(v3)) continue;

						int state = 0;
						if (v0 > level) state |= 1;
						if (v1 > level) state |= 2;
						if (v2 > level) state |= 4;
						if (v3 > level) state |= 8;

						int[] edges = edgeTable[state];

						for (int e = 0; e < edges.Length; e += 2)
						{
							Vector3 pA = GetEdgeIntersection(edges[e], i, j, gridPositions, gridValues, level);
							Vector3 pB = GetEdgeIntersection(edges[e + 1], i, j, gridPositions, gridValues, level);

							int vIndex = vertices.Count;
							vertices.Add(pA);
							vertices.Add(pB);

							colors.Add(contourColor);
							colors.Add(contourColor);

							indices.Add(vIndex);
							indices.Add(vIndex + 1);
						}
					}
				}
			}

			// Step 4: Build Mesh
			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null) meshFilter.sharedMesh = CreateLinesMesh();
		}

		private Vector3 GetEdgeIntersection(int edge, int i, int j, Vector3[,] pos, float[,] vals, float level)
		{
			Vector3 p1 = Vector3.zero, p2 = Vector3.zero;
			float v1 = 0, v2 = 0;

			switch (edge)
			{
				case 0: // Bottom (v0 to v1)
					p1 = pos[i, j]; p2 = pos[i + 1, j];
					v1 = vals[i, j]; v2 = vals[i + 1, j];
					break;
				case 1: // Right (v1 to v2)
					p1 = pos[i + 1, j]; p2 = pos[i + 1, j + 1];
					v1 = vals[i + 1, j]; v2 = vals[i + 1, j + 1];
					break;
				case 2: // Top (v2 to v3)
					p1 = pos[i + 1, j + 1]; p2 = pos[i, j + 1];
					v1 = vals[i + 1, j + 1]; v2 = vals[i, j + 1];
					break;
				case 3: // Left (v3 to v0)
					p1 = pos[i, j + 1]; p2 = pos[i, j];
					v1 = vals[i, j + 1]; v2 = vals[i, j];
					break;
			}

			float t = (Mathf.Approximately(v1, v2)) ? 0.5f : (level - v1) / (v2 - v1);
			return Vector3.Lerp(p1, p2, t);
		}

		Mesh CreateLinesMesh()
		{
			var mesh = new Mesh();

			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);

			// Use Lines topology to draw discrete line segments
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
			mesh.RecalculateBounds();

			return mesh;
		}
	}
}