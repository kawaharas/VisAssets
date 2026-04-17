using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using VisAssets.SciVis.Structured.Common;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Slicer
{
	using FieldType   = DataElement.FieldType;
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
		SerializedProperty builtinCustomShaderProp;
		SerializedProperty sliceShaderProp;

		private void OnEnable()
		{
			mode        = serializedObject.FindProperty("filterMode");
			axis        = serializedObject.FindProperty("sliceHelper.axis");
			slice       = serializedObject.FindProperty("sliceHelper.slice");
			shift       = serializedObject.FindProperty("shift");
			alphaCutoff = serializedObject.FindProperty("alphaCutoff");
			builtinCustomShaderProp = serializedObject.FindProperty("builtinCustomShader");
			sliceShaderProp         = serializedObject.FindProperty("sliceShader");
		}

		public override void OnInspectorGUI()
		{
			var slicer = target as Slicer;

			if (slicer == null) return;

			serializedObject.Update();

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (mode != null) EditorGUILayout.PropertyField(mode, new GUIContent("Filter Mode"), true);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				serializedObject.ApplyModifiedProperties();
				slicer.SetMode(mode.enumValueIndex);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (axis != null) EditorGUILayout.IntSlider(axis, 0, 2, new GUIContent("Axis: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				serializedObject.ApplyModifiedProperties();
				slicer.SetAxis(axis.intValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (slice != null) EditorGUILayout.Slider(slice, 0f, 1f, new GUIContent("Slice: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				serializedObject.ApplyModifiedProperties();
				slicer.SetSlice(slice.floatValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (shift != null) EditorGUILayout.Slider(shift, 0f, 1f, new GUIContent("Color Shift: "));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				serializedObject.ApplyModifiedProperties();
				slicer.SetColorShift(shift.floatValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (alphaCutoff != null)
			{
				EditorGUILayout.PropertyField(alphaCutoff, new GUIContent("Alpha Cutoff"));
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				serializedObject.ApplyModifiedProperties();
				slicer.UpdateMaterialShader();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			// Built-in environment shader registration slot
			EditorGUILayout.PropertyField(builtinCustomShaderProp, new GUIContent("Built-in Custom Shader"));

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(sliceShaderProp, new GUIContent("Current Render Shader"));
			EditorGUI.EndDisabledGroup();

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				serializedObject.ApplyModifiedProperties();
				slicer.UpdateMaterialShader();
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
	/// Mapper module that extracts a 2D cross-section from a 3D scalar field, mapping the data
	/// onto a dynamically generated texture and an intersecting plane mesh.
	/// Accurately maintains non-uniform physical distances (RECTILINEAR grid structure).
	/// </summary>
	public class Slicer : MapperModuleTemplate
	{
		public enum FILTER_MODE
		{
			TRILINEAR,
			BILINEAR,
			POINT
		};

		[SerializeField]
		public SliceHelper sliceHelper = new SliceHelper();

		[SerializeField] public Shader builtinCustomShader;
		public Shader sliceShader;

		[Range(0f, 1f)]
		public float alphaCutoff = 0.01f;
		[SerializeField]
		public FILTER_MODE filterMode;
		[SerializeField, Range(0, 1f)]
		public float shift;

		[SerializeField]
		public int[] dims;
		[SerializeField]
		public DataElement element;

		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		List<int>     triangles;
		List<Vector2> texture_uv;
		Material      material;

		Texture2D texture;
		Color[]   texcolor;
		public int tri_idx = 0;

		/// <summary>
		/// Automatically determines the current render pipeline (Built-in or URP) and assigns the appropriate shader.
		/// Prevents compilation errors by using Unity's standard unlit shader for URP,
		/// and a custom pre-assigned shader for the Built-in Render Pipeline to avoid build stripping.
		/// </summary>
#if UNITY_EDITOR
		private void EnsureCorrectShader()
		{
			bool isURP = GraphicsSettings.renderPipelineAsset != null;

			if (isURP)
			{
				string expectedShaderName = "Universal Render Pipeline/Unlit";
				if (sliceShader == null || sliceShader.name != expectedShaderName)
				{
					sliceShader = Shader.Find(expectedShaderName);
				}
			}
			else
			{
				if (sliceShader == null || sliceShader != builtinCustomShader)
				{
					sliceShader = builtinCustomShader;
				}
			}
		}
#endif

		protected override void Reset()
		{
#if UNITY_EDITOR
			base.Reset();

			EnsureCorrectShader();
#endif
		}

		public override void InitModule()
		{
			vertices   = new List<Vector3>();
			normals    = new List<Vector3>();
			colors     = new List<Color>();
			triangles  = new List<int>();
			texture_uv = new List<Vector2>();

			filterMode = FILTER_MODE.TRILINEAR;

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

		public override void IdleFunc()
		{
		}

		public override void SetParameters()
		{
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (pdf == null || pdf.elements == null || pdf.elements.Length == 0) return;

			element = pdf.elements[0];
			dims    = element.dims;

			if (sliceHelper == null) sliceHelper = new SliceHelper();
			sliceHelper.Reset(element);
		}

		public override void ResetUI()
		{
		}

		private void OnDestroy()
		{
			if (material != null) Destroy(material);
			if (texture != null) Destroy(texture);
		}

		void OnValidate()
		{
#if UNITY_EDITOR
			EnsureCorrectShader();
#endif
			if (!IsDataLoadedToParent()) return;

			if (sliceHelper != null) sliceHelper.Validate();

			UpdateMaterialShader();

			if (activation != null) activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

		/// <summary>
		/// Updates or creates the shared material used by the Slicer.
		/// Dynamically configures material properties (Cutout, Culling) for URP compatibility.
		/// </summary>
		public void UpdateMaterialShader()
		{
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer == null) return;

#if UNITY_EDITOR
			EnsureCorrectShader();
#endif

			if (sliceShader == null) return;

			if (material == null || material.shader != sliceShader)
			{
				if (material != null)
				{
					if (Application.isPlaying) Destroy(material);
					else DestroyImmediate(material);
				}

				material = new Material(sliceShader);

				bool isURP = GraphicsSettings.renderPipelineAsset != null;
				if (isURP)
				{
					// Configure URP Unlit shader to act like a cutout double-sided shader
					if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0); // Double-sided
					if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1); // Enable Cutout
				}

				meshRenderer.sharedMaterial = material;
				AssignTextureToMaterial();
			}

			if (material != null)
			{
				if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", alphaCutoff);
			}
		}

		/// <summary>
		/// Assigns the generated dynamic texture to the material, handling URP's _BaseMap difference.
		/// </summary>
		private void AssignTextureToMaterial()
		{
			if (material == null || texture == null) return;

			material.mainTexture = texture;
			material.mainTexture.wrapMode = TextureWrapMode.Clamp;

			// URP shaders use _BaseMap instead of _MainTex
			if (material.HasProperty("_BaseMap"))
			{
				material.SetTexture("_BaseMap", texture);
			}
		}

		Color GetColor(float level)
		{
			level += shift;
			if (level > 1f) level -= 1f;

			Color c = Color.HSVToRGB(level, 1f, 1f);
			return new Color(c.r, c.g, c.b, 1f);
		}

		public void SetMode(int mode)
		{
			filterMode = (FILTER_MODE)mode;
			if (IsDataLoadedToParent()) ParameterChanged();
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

		public void SetColorShift(float _shift)
		{
			shift = _shift;
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void Calc()
		{
			if (element == null || element.values == null) return;

			if (vertices == null) InitModule();
			if (sliceHelper == null) sliceHelper = new SliceHelper();

			float ratio;
			int   idx = sliceHelper.GetIndexOfCuttingEdge(element, out ratio);

			vertices.Clear();
			normals.Clear();
			colors.Clear();
			triangles.Clear();
			texture_uv.Clear();

			int slice_w, slice_h, slice_d;
			sliceHelper.GetSliceDimensions(element, out slice_w, out slice_h, out slice_d);

			if (texture != null) Destroy(texture);

			texture  = new Texture2D(slice_w, slice_h, TextureFormat.RGBA32, true);
			texcolor = new Color[slice_w * slice_h];

			float[] values      = element.values;
			float   values_min  = element.min;
			float   values_diff = element.max - values_min;

			for (int j = 0; j < slice_h; j++)
			{
				for (int i = 0; i < slice_w; i++)
				{
					int idx0, idx1;
					sliceHelper.GetIndices(i, j, slice_w, slice_h, slice_d, idx, out idx0, out idx1);

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
							if (values[idx0] == element.undef || values[idx1] == element.undef)
							{
								ansf     = element.undef;
								is_undef = true;
							}
						}

						if (!is_undef)
						{
							if (element.fieldType == FieldType.RECTILINEAR)
							{
								ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
							}
							else if (element.fieldType == FieldType.UNIFORM || element.fieldType == FieldType.IRREGULAR)
							{
								if (sliceHelper.value == sliceHelper.value_min)
								{
									ansf = values[idx0];
								}
								else if (sliceHelper.value == sliceHelper.value_max)
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

					if (element.useUndef && ansf == element.undef)
					{
						texcolor[slice_w * j + i] = new Color(1f, 1f, 1f, 0);
					}
					else
					{
						var color = Mathf.Clamp((ansf - values_min) / values_diff, 0, 1f);
						texcolor[slice_w * j + i] = GetColor(color);
					}

					float[] coord3 = element.coords[3];
					float   v0, v1, v2;

					if (slice_d == 1)
					{
						v0 = coord3[idx0 * 3 + 0];
						v1 = coord3[idx0 * 3 + 1];
						v2 = coord3[idx0 * 3 + 2];
					}
					else
					{
						v0 = coord3[idx0 * 3 + 0] + (coord3[idx1 * 3 + 0] - coord3[idx0 * 3 + 0]) * ratio;
						v1 = coord3[idx0 * 3 + 1] + (coord3[idx1 * 3 + 1] - coord3[idx0 * 3 + 1]) * ratio;
						v2 = coord3[idx0 * 3 + 2] + (coord3[idx1 * 3 + 2] - coord3[idx0 * 3 + 2]) * ratio;
					}

					vertices.Add(new Vector3(v0, v1, v2));
				}
			}

			texture.SetPixels(texcolor, 0);
			texture.Apply();

			if (filterMode == FILTER_MODE.POINT) texture.filterMode = FilterMode.Point;
			else if (filterMode == FILTER_MODE.BILINEAR) texture.filterMode = FilterMode.Bilinear;
			else texture.filterMode = FilterMode.Trilinear;

			tri_idx = 0;

			for (int j = 0; j < slice_h - 1; j++)
			{
				for (int i = 0; i < slice_w - 1; i++)
				{
					int v0 = slice_w * j + i;
					int v1 = slice_w * (j + 1) + i;
					int v2 = slice_w * j + (i + 1);
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

			float du = 1f / (float)(slice_w - 1);
			float dv = 1f / (float)(slice_h - 1);

			for (int j = 0; j < slice_h; j++)
			{
				float v = dv * (float)j;
				for (int i = 0; i < slice_w; i++) texture_uv.Add(new Vector2(du * (float)i, v));
			}

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null) meshFilter.sharedMesh = CreatePlane();

			AssignTextureToMaterial(); // ★ URP対応のテクスチャ割り当てメソッドを使用
		}

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
	}
}