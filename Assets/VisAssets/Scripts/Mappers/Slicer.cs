using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Slicer
{
	using FieldType = DataElement.FieldType;
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Slicer))]
	public class SlicerEditor : Editor
	{
		SerializedProperty mode;
		SerializedProperty axis;
		SerializedProperty slice;
		SerializedProperty shift;
		SerializedProperty alphaCutoff;
		SerializedProperty sliceShader;
		SerializedProperty uiPrefab;
		int   previousAxis  = 0;
		float previousSlice = 0;
		float previousCutoff = 0.01f;

		/// <summary>
		/// Initializes serialized properties when the object is selected in the Inspector.
		/// </summary>
		private void OnEnable()
		{
			mode        = serializedObject.FindProperty("filterMode");
			axis        = serializedObject.FindProperty("axis");
			slice       = serializedObject.FindProperty("slice");
			shift       = serializedObject.FindProperty("shift");
			alphaCutoff = serializedObject.FindProperty("alphaCutoff");
			sliceShader = serializedObject.FindProperty("sliceShader");
			uiPrefab    = serializedObject.FindProperty("UIPrefab");
		}

		/// <summary>
		/// Renders the custom Inspector GUI for the Slicer module.
		/// </summary>
		public override void OnInspectorGUI()
		{
			var slicer = target as Slicer;

			if (slicer == null) return;

			serializedObject.Update();
/*
			if (sliceShader != null)
			{
				EditorGUILayout.PropertyField(sliceShader, new GUIContent("Shader"));
			}

			if (uiPrefab != null)
			{
				EditorGUILayout.PropertyField(uiPrefab, new GUIContent("UI Prefab"));
			}
*/
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			if (mode != null)
			{
				EditorGUILayout.PropertyField(mode, new GUIContent("Filter Mode"), true);
			}

			GUILayout.Space(5f);

			var currentAxis  = axis  != null ? EditorGUILayout.IntSlider("Axis", axis.intValue, 0, 2) : 0;

			GUILayout.Space(5f);

			var currentSlice = slice != null ? EditorGUILayout.Slider("Slice", slice.floatValue, 0, 1f) : 0f;

			GUILayout.Space(5f);

			var currentShift = shift != null ? EditorGUILayout.Slider("Color Shift", shift.floatValue, 0, 1f) : 0f;

			GUILayout.Space(5f);

			if (alphaCutoff != null)
			{
				EditorGUILayout.PropertyField(alphaCutoff, new GUIContent("Alpha Cutoff"));
			}

			GUILayout.Space(5f);

			if (sliceShader != null)
			{
				EditorGUILayout.PropertyField(sliceShader, new GUIContent("Shader"));
			}

			GUILayout.Space(5f);

			if (uiPrefab != null)
			{
				EditorGUILayout.PropertyField(uiPrefab, new GUIContent("UI Prefab"));
			}

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				if (currentAxis != previousAxis)
				{
					slicer.SetAxis(currentAxis);
					previousAxis = currentAxis;
				}
				else if (currentSlice != previousSlice)
				{
					slicer.SetSlice(currentSlice);
					previousSlice = currentSlice;
				}
				else
				{
					slicer.SetColorShift(currentShift);
				}

				if (alphaCutoff != null && alphaCutoff.floatValue != previousCutoff)
				{
					slicer.UpdateMaterialShader();
					previousCutoff = alphaCutoff.floatValue;
				}

				if (EditorApplication.isPlaying)
				{
					slicer.UpdateMaterialShader();
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
	public class Slicer : MapperModuleTemplate
	{
		[Serializable]
		public class Slice
		{
			public float slider = 0;
			public float value  = 0;
			public float min    = 0;
			public float max    = 1f;
		}

		public enum FILTER_MODE
		{
			TRILINEAR,
			BILINEAR,
			POINT
		};

//		[Header("Rendering Settings")]
		[SerializeField]
		public Shader sliceShader;

		[Range(0f, 1f)]
		public float alphaCutoff = 0.01f;

		[SerializeField, Range(0, 2)]
		public int axis;

		[SerializeField, Range(0, 1f)]
		public float slice;

		[SerializeField]
		public float value;

		[SerializeField]
		public float min, max;

		[SerializeField]
		public FILTER_MODE filterMode;

		[SerializeField, Range(0, 1f)]
		public float shift; // Shift hue value to calculate RGB color

		[SerializeField]
		public int[] dims;

		[SerializeField]
		public DataElement element;

		[SerializeField]
		public Slice[] slices;

		int   prev_axis;
		float prev_slice;
		float prev_value;

		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		List<int>     triangles;
		List<Vector2> texture_uv;
		Material      material;

		Texture2D texture;
		Color[]   texcolor;
		public int tri_idx = 0;
		public float ratio, ratio2;

		// ==========================================================
		// Core Module Logic
		// ==========================================================

		/// <summary>
		/// Ensures that the slices array is initialized to avoid NullReferenceExceptions.
		/// </summary>
		private void EnsureSlices()
		{
			if (slices == null || slices.Length < 3)
			{
				slices = new Slice[3];
				for (int i = 0; i < 3; i++)
				{
					slices[i] = new Slice();
				}
			}
		}

		/// <summary>
		/// Initializes internal lists, default parameters, and renderer configurations.
		/// </summary>
		public override void InitModule()
		{
			vertices   = new List<Vector3>();
			normals    = new List<Vector3>();
			colors     = new List<Color>();
			triangles  = new List<int>();
			texture_uv = new List<Vector2>();

			filterMode = FILTER_MODE.TRILINEAR;
			axis  = prev_axis  = 0;
			slice = prev_slice = 0;
			value = prev_value = 0;
			min = 0;
			max = 1f;

			EnsureSlices();

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}

			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				meshRenderer.hideFlags = HideFlags.HideInInspector;
			}

			// Delegate material generation and application to the integrated method.
			UpdateMaterialShader();
		}

		/// <summary>
		/// The main execution function of the module that triggers the slice calculation.
		/// </summary>
		public override int BodyFunc()
		{
			Calc();
			return 1;
		}

		/// <summary>
		/// Resets and prepares the slice parameters based on the parent data field elements.
		/// </summary>
		public override void ReSetParameters()
		{
			EnsureSlices();

			if (pdf == null || pdf.elements == null || pdf.elements.Length == 0) return;

			element = pdf.elements[0];
			dims = element.dims;

			for (int i = 0; i < 3; i++)
			{
				slices[i].slider = 0;
				if (element.fieldType == FieldType.RECTILINEAR)
				{
					float first = element.coords[i].First();
					float last  = element.coords[i].Last();
					slices[i].min = Mathf.Min(first, last);
					slices[i].max = Mathf.Max(first, last);
				}
				else if ((element.fieldType == FieldType.UNIFORM) ||
						 (element.fieldType == FieldType.IRREGULAR))
				{
					slices[i].min = 0;
					slices[i].max = (float)element.dims[i] - 1;
				}
				slices[i].value = slices[i].min;
			}

			min   = slices[axis].min;
			max   = slices[axis].max;
			value = prev_value = min + (max - min) * slice;
		}

		/// <summary>
		/// Applies parameter changes triggered by external UI.
		/// </summary>
		public override void SetParameters()
		{
		}

		/// <summary>
		/// Retrieves current parameters to update external UI states.
		/// </summary>
		public override void GetParameters()
		{
		}

		/// <summary>
		/// Validates parameters modified in the Inspector, ensuring values remain within valid limits.
		/// </summary>
		void OnValidate()
		{
			EnsureSlices();

			if (!IsDataLoadedToParent()) return;

			if (axis != prev_axis)
			{
				// Backup current variables
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = min;
				slices[prev_axis].max    = max;

				// Load variables to restore updated axis information
				slice = slices[axis].slider;
				value = slices[axis].value;
				min   = slices[axis].min;
				max   = slices[axis].max;
				prev_slice = slice;
				prev_value = value;
				prev_axis  = axis;
			}
			else
			{
				if (value != prev_value)
				{
					var _value = Mathf.Clamp(value, min, max);
					slice = (_value - min) / (max - min);
					prev_value = _value;
				}

				if (slice != prev_slice)
				{
					value = min + (max - min) * slice;
					prev_slice = slice;
				}
			}

			UpdateMaterialShader();

			if (activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Resets the associated UI components to their default states.
		/// </summary>
		public override void ResetUI()
		{
		}

		/// <summary>
		/// Converts a normalized scalar value into an HSV-based RGB color applying the current hue shift.
		/// </summary>
		Color GetColor(float level)
		{
			level += shift;

			if (level > 1f)
			{
				level -= 1f;
			}

			Color c = Color.HSVToRGB(level, 1f, 1f);

			return new Color(c.r, c.g, c.b, 1f);
		}

		/// <summary>
		/// Sets the texture filtering mode (Point, Bilinear, or Trilinear).
		/// </summary>
		public void SetMode(int mode)
		{
			filterMode = (FILTER_MODE)mode;
			ParameterChanged();
		}

		/// <summary>
		/// Updates the target axis (X=0, Y=1, Z=2) for the slice plane.
		/// </summary>
		public void SetAxis(int _axis)
		{
			EnsureSlices();

			if (_axis != prev_axis)
			{
				// Backup current variables
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = min;
				slices[prev_axis].max    = max;

				// Load variables to restore updated axis information
				slice = slices[_axis].slider;
				value = slices[_axis].value;
				min   = slices[_axis].min;
				max   = slices[_axis].max;

				prev_slice = slice;
				prev_value = value;
				prev_axis  = _axis;
				axis       = _axis;
			}

			ParameterChanged();
		}

		/// <summary>
		/// Sets the normalized slice position along the current axis.
		/// </summary>
		public void SetSlice(float _slice)
		{
			EnsureSlices();

			if (_slice != prev_slice)
			{
				value = min + (max - min) * _slice;
				prev_slice = _slice;
				slice      = _slice;
			}

			ParameterChanged();
		}

		/// <summary>
		/// Sets the color shift (hue offset) used for data mapping.
		/// </summary>
		public void SetColorShift(float _shift)
		{
			shift = _shift;
			ParameterChanged();
		}

		/// <summary>
		/// Calculates the exact cell index and interpolation ratio where the slicing plane intersects the volume grid.
		/// </summary>
		int GetIndexOfCuttingEdge()
		{
			int idx = 0;
			ratio = 0;

			if (element.fieldType == FieldType.RECTILINEAR)
			{
				float[] coord = element.coords[axis];
				int size = element.dims[axis];

				if (coord.First() < coord.Last())
				{
					for (int i = 0; i < size - 1; i++)
					{
						if (coord[i + 1] >= value)
						{
							idx = i;
							ratio = (value - coord[i]) / (coord[i + 1] - coord[i]);

							if (ratio < 0) ratio += 1f;
							if (value == max) ratio = 1f;

							break;
						}
					}
				}
				else
				{
					for (int i = 0; i < size - 1; i++)
					{
						if (coord[i + 1] <= value)
						{
							idx = i;
							ratio = (value - coord[i]) / (coord[i + 1] - coord[i]);

							if (ratio < 0) ratio += 1f;
							if (value == max) ratio = 0;

							break;
						}
					}
				}
			}
			else if ((element.fieldType == FieldType.UNIFORM) ||
					 (element.fieldType == FieldType.IRREGULAR))
			{
				idx = (int)Mathf.Clamp(value, min, max - 1f);
				ratio = (value % 1);

				if (value == max) ratio = 1f;
			}

			return idx;
		}

		/// <summary>
		/// Core calculation method. Generates the slicing plane mesh and interpolates the scalar field onto a 2D texture.
		/// </summary>
		public void Calc()
		{
			int idx = GetIndexOfCuttingEdge();

			if (vertices == null)
			{
				InitModule();
			}

			vertices.Clear();
			normals.Clear();
			colors.Clear();
			triangles.Clear();
			texture_uv.Clear();

			int slice_w = 0; // width
			int slice_h = 0; // height
			int slice_d = 0; // depth

			if (axis == 0)
			{
				slice_w = element.dims[1];
				slice_h = element.dims[2];
				slice_d = element.dims[0];
			}
			else if (axis == 1)
			{
				slice_w = element.dims[0];
				slice_h = element.dims[2];
				slice_d = element.dims[1];
			}
			else
			{
				slice_w = element.dims[0];
				slice_h = element.dims[1];
				slice_d = element.dims[2];
			}

			if (texture != null)
			{
				Destroy(texture);
			}

			texture  = new Texture2D(slice_w, slice_h, TextureFormat.RGBA32, true);
			texcolor = new Color[slice_w * slice_h];
			float[] values      = element.values;
			float   values_min  = element.min;
			float   values_max  = element.max;
			float   values_diff = values_max - values_min;

			for (int j = 0; j < slice_h; j++)
			{
				for (int i = 0; i < slice_w; i++)
				{
					int idx0 = 0;
					int idx1 = 0;

					if (axis == 0)
					{
						idx0 = slice_d * (slice_w * j + i) + idx;
						idx1 = idx0 + 1;
					}
					else if (axis == 1)
					{
						idx0 = slice_w * slice_d * j + i + slice_w * idx;
						idx1 = idx0 + slice_w;
					}
					else
					{
						idx0 = slice_w * slice_h * idx + slice_w * j + i;
						idx1 = idx0 + slice_w * slice_h;
					}

					float ansf = 0;

					if (slice_d == 1)
					{
						ansf = values[idx0];
					}
					else
					{
						bool is_undef = false;
						if (element.useUndef)
						{
							if ((values[idx0] == element.undef) ||
								(values[idx1] == element.undef))
							{
								ansf = element.undef;
								is_undef = true;
							}
						}
						if (!is_undef)
						{
							if (element.fieldType == FieldType.RECTILINEAR)
							{
								ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
							}
							else if ((element.fieldType == FieldType.UNIFORM) ||
									 (element.fieldType == FieldType.IRREGULAR))
							{
								if (value == min)
								{
									ansf = values[idx0];
								}
								else if (value == max)
								{
									ansf = values[idx1];
								}
								else
								{
									ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
								}
							}
						}
					}

					if ((element.useUndef) && (ansf == element.undef))
					{
						texcolor[slice_w * j + i] = new Color(1f, 1f, 1f, 0);
					}
					else
					{
						var color = Mathf.Clamp((ansf - values_min) / values_diff, 0, 1f);
						texcolor[slice_w * j + i] = GetColor(color);
					}

					float v0, v1, v2;
					float[] coord3 = element.coords[3];

					if (slice_d == 1)
					{
						v0 = coord3[idx0 * 3 + 0];
						v1 = coord3[idx0 * 3 + 1];
						v2 = coord3[idx0 * 3 + 2];
						vertices.Add(new Vector3(v0, v1, v2));
					}
					else
					{
						v0 = coord3[idx0 * 3 + 0] + (coord3[idx1 * 3 + 0] - coord3[idx0 * 3 + 0]) * ratio;
						v1 = coord3[idx0 * 3 + 1] + (coord3[idx1 * 3 + 1] - coord3[idx0 * 3 + 1]) * ratio;
						v2 = coord3[idx0 * 3 + 2] + (coord3[idx1 * 3 + 2] - coord3[idx0 * 3 + 2]) * ratio;
						vertices.Add(new Vector3(v0, v1, v2));
					}
				}
			}
			texture.SetPixels(texcolor, 0);
			texture.Apply();

			if (filterMode == FILTER_MODE.POINT)
			{
				texture.filterMode = FilterMode.Point;
			}
			else if (filterMode == FILTER_MODE.BILINEAR)
			{
				texture.filterMode = FilterMode.Bilinear;
			}
			else
			{
				texture.filterMode = FilterMode.Trilinear;
			}
			texture.wrapMode = TextureWrapMode.Clamp;

			// Set triangle indices
			tri_idx = 0;
			for (int j = 0; j < slice_h - 1; j++)
			{
				for (int i = 0; i < slice_w - 1; i++)
				{
					int v0 = slice_w * j + i;
					int v1 = slice_w * (j + 1) +  i;
					int v2 = slice_w * j +      (i + 1);
					int v3 = slice_w * (j + 1) + (i + 1);
					triangles.Add(v0);
					triangles.Add(v1);
					triangles.Add(v3);
					triangles.Add(v0);
					triangles.Add(v3);
					triangles.Add(v2);
					tri_idx += 2;
				}
			}

			// Set texture UVs
			float du = 1f / (float)(slice_w - 1);
			float dv = 1f / (float)(slice_h - 1);
			for (int j = 0; j < slice_h; j++)
			{
				float v = dv * (float)j;
				for (int i = 0; i < slice_w; i++)
				{
					texture_uv.Add(new Vector2(du * (float)i, v));
				}
			}

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.sharedMesh = CreatePlane();
			}

			if (material != null)
			{
				material.mainTexture = texture;
				material.mainTexture.wrapMode = TextureWrapMode.Clamp;
			}
		}

		/// <summary>
		/// Constructs the plane mesh using the calculated vertices and UVs.
		/// </summary>
		Mesh CreatePlane()
		{
			var mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetUVs(0, texture_uv);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			return mesh;
		}

		/// <summary>
		/// Dynamically generates or updates the slice material, ensuring robust memory management.
		/// </summary>
		public void UpdateMaterialShader()
		{
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer == null) return;

			// Regenerate material if it's missing or the shader assignment has changed in the Inspector
			if (material == null || (sliceShader != null && material.shader != sliceShader))
			{
				// Destroy the old material to prevent memory leaks in the Editor and at runtime
				if (material != null)
				{
					if (Application.isPlaying)
					{
						Destroy(material);
					}
					else
					{
						DestroyImmediate(material);
					}
				}

				// Create the new material with the specified shader or fallback to Standard
				if (sliceShader != null)
				{
					material = new Material(sliceShader);
				}
				else
				{
					material = new Material(Shader.Find("Standard"));
				}

				// Apply to Renderer (always use sharedMaterial to prevent unintended instantiation in the Editor)
				meshRenderer.sharedMaterial = material;

				// Reassign existing texture if available
				if (texture != null)
				{
					material.mainTexture = texture;
					material.mainTexture.wrapMode = TextureWrapMode.Clamp;
				}
			}

			// Update shader properties
			if (material != null && material.HasProperty("_Cutoff"))
			{
				material.SetFloat("_Cutoff", alphaCutoff);
			}
		}

		/// <summary>
		/// Cleans up dynamically generated materials and textures to release memory upon destruction.
		/// </summary>
		private void OnDestroy()
		{
			if (material != null)
			{
				Destroy(material);
			}

			if (texture != null)
			{
				Destroy(texture);
			}
		}
	}
}