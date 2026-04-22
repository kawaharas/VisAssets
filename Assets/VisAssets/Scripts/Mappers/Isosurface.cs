//
// The part of Marching Cubes in this code was ported from VFIVE (isosurf.cpp).
// https://www.jamstec.go.jp/ceist/aeird/avcrg/vfive.ja.html
// The original code was written by Akira Kageyama (Kobe University) and Nobuaki Ohno (University of Hyogo).
//
// An implementation of graphics buffers was referenced from the code written by Keijiro Takahashi (Unity Technologies Japan).
// https://github.com/keijiro/ComputeMarchingCubes
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Isosurface
{
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Isosurface))]
	public class IsosurfaceEditor : Editor
	{
		SerializedProperty slider;
		SerializedProperty threshold;
		SerializedProperty min;
		SerializedProperty max;
		SerializedProperty triCount;
		SerializedProperty shader;
		SerializedProperty builtinCustomshader;
		SerializedProperty rendershader;

		/// <summary>
		/// Initializes serialized properties when the object is selected in the Inspector.
		/// </summary>
		private void OnEnable()
		{
			slider      = serializedObject.FindProperty("slider");
			threshold   = serializedObject.FindProperty("threshold");
			min         = serializedObject.FindProperty("min");
			max         = serializedObject.FindProperty("max");
			triCount    = serializedObject.FindProperty("triCount");
			shader      = serializedObject.FindProperty("shader");
			builtinCustomshader = serializedObject.FindProperty("builtinCustomShader");
			rendershader = serializedObject.FindProperty("renderShader");
		}

		/// <summary>
		/// Renders the custom Inspector GUI for the Isosurface module.
		/// </summary>
		public override void OnInspectorGUI()
		{
			var isosurface = target as Isosurface;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			var _threshold = EditorGUILayout.Slider("Threshold: ", threshold.floatValue, min.floatValue, max.floatValue);

			GUILayout.Space(5f);

			EditorGUILayout.LabelField("Total Triangles : " + (triCount.intValue).ToString());

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(builtinCustomshader, new GUIContent("Built-in Custom Shader"));

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(rendershader, new GUIContent("Current Render Shader"));
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

			EditorGUILayout.PropertyField(shader, new GUIContent("Compute Shader"));

			EditorGUI.EndDisabledGroup();

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Isosurface");

				if (isosurface != null)
				{
					if (_threshold != threshold.floatValue)
					{
						isosurface.SetValue(_threshold);
					}

					isosurface.UpdateMaterialShader();
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
	public class Isosurface : MapperModuleTemplate
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct VertexData
		{
			public Vector3 pos; public Vector3 norm; public Vector4 col;
		}

		DataElement element;
		int []  dims;
		float[] coords;
		float[] values;

		[Range(0f, 1f)] public float slider;
		[SerializeField, ReadOnly] public float threshold;
		[SerializeField, ReadOnly] public float min;
		[SerializeField, ReadOnly] public float max = 1f;
		[SerializeField] public int triCount;

		[SerializeField] public Shader builtinCustomShader;
		public Shader renderShader;

		private Material material;

		public ComputeShader shader = null;
		ComputeBuffer tablesBuffer;
		int[] packedTables;

		private int maximumVertexNum;
		private int currentVolumeSize = -1;

		ComputeBuffer  cvBufferSingle;
		GraphicsBuffer vertexBufferSingle;
		ComputeBuffer  counterBufferSingle;
		ComputeBuffer  counterCheckBufferSingle;
		Mesh singleMesh;

		// ==========================================================
		// Component Caching (Lazy Evaluation)
		// ==========================================================
		private MeshFilter _cachedMeshFilter;
		private MeshFilter CachedMeshFilter
		{
			get
			{
				if (_cachedMeshFilter == null) _cachedMeshFilter = GetComponent<MeshFilter>();
				return _cachedMeshFilter;
			}
		}

		private MeshRenderer _cachedMeshRenderer;
		private MeshRenderer CachedMeshRenderer
		{
			get
			{
				if (_cachedMeshRenderer == null) _cachedMeshRenderer = GetComponent<MeshRenderer>();
				return _cachedMeshRenderer;
			}
		}

		// ==========================================================
		// Core Module Logic
		// ==========================================================

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
			EnsureCorrectShader();
		}

		/// <summary>
		/// Automatically determines the current render pipeline (Built-in or URP) and assigns the appropriate shader.
		/// Prevents compilation errors and missing shaders by using Unity's standard shader for URP,
		/// and a custom pre-assigned shader for the Built-in Render Pipeline to avoid build stripping.
		/// </summary>
		private void EnsureCorrectShader()
		{
			bool isURP = GraphicsSettings.renderPipelineAsset != null;

			if (isURP)
			{
				string expectedShaderName = "Universal Render Pipeline/Particles/Lit";

				if (renderShader == null || renderShader.name != expectedShaderName)
				{
					renderShader = Shader.Find(expectedShaderName);
				}
			}
			else
			{
				if (renderShader == null || renderShader != builtinCustomShader)
				{
					renderShader = builtinCustomShader;
				}
			}
		}
#endif

		public override void InitModule()
		{
			if (tablesBuffer != null) return;

			int vertexStride = 40;
			long maxBufferBytes = SystemInfo.maxGraphicsBufferSize;

			// Safe fallback for devices that return 0
			if (maxBufferBytes <= 0) maxBufferBytes = 128 * 1024 * 1024;

			long maxTheoreticalVerts = (long)(maxBufferBytes * 0.8f) / vertexStride;

			int practicalCap = 15000000;
#if UNITY_ANDROID && !UNITY_EDITOR
			practicalCap = 3000000;
#endif
			int calculatedVerts = (int)Math.Min(maxTheoreticalVerts, practicalCap);
			maximumVertexNum = (calculatedVerts / 3) * 3;

			if (maximumVertexNum <= 0) maximumVertexNum = 65536 * 3;

			dims = new int[3];
			triCount = 0;

			var isosurfaceTables = new IsosurfaceV5Tables();
			packedTables = isosurfaceTables.PackingTables();
			tablesBuffer = new ComputeBuffer(packedTables.Length, sizeof(int));
			tablesBuffer.SetData(packedTables);

			singleMesh = new Mesh();
			singleMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			singleMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

			var vp = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
			var vn = new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3);
			var vc = new VertexAttributeDescriptor(VertexAttribute.Color,    VertexAttributeFormat.Float32, 4);

			singleMesh.SetVertexBufferParams(maximumVertexNum, vp, vn, vc);
			singleMesh.SetIndexBufferParams(maximumVertexNum, IndexFormat.UInt32);

			int[] inds = new int[maximumVertexNum];
			for (int i = 0; i < maximumVertexNum; i++)
			{
				inds[i] = i;
			}
			singleMesh.SetIndexBufferData(inds, 0, 0, maximumVertexNum);
			singleMesh.SetSubMesh(0, new SubMeshDescriptor(0, maximumVertexNum), MeshUpdateFlags.DontRecalculateBounds);

			vertexBufferSingle = singleMesh.GetVertexBuffer(0);
			counterBufferSingle = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
			counterCheckBufferSingle = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);

			if (CachedMeshFilter != null)
			{
				CachedMeshFilter.sharedMesh = singleMesh;
			}

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
			if (!pdf.dataLoaded) return;

			if (dims != null && pdf.elements != null && pdf.elements.Length > 0)
			{
				if (dims[0] != pdf.elements[0].dims[0] ||
					dims[1] != pdf.elements[0].dims[1] ||
					dims[2] != pdf.elements[0].dims[2])
				{
					SoftRebuild();
					return;
				}
			}

			Calc();
		}

		/// <summary>
		/// Performs a partial rebuild of the volume dimensions without completely resetting module parameters.
		/// </summary>
		private void SoftRebuild()
		{
			element = pdf.elements[0];
			dims    = element.dims;
			coords  = element.coords[3];
			values  = element.values;
			min     = element.min;
			max     = element.max;

			threshold = Mathf.Clamp(threshold, min, max);

			if (max > min)
			{
				slider = (threshold - min) / (max - min);
			}
			else
			{
				slider = 0.5f;
			}

			GenCoordPrepSinglePass();
			Calc();
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (!pdf.dataLoaded) return;

			element = pdf.elements[0];
			dims    = element.dims;
			coords  = element.coords[3];
			values  = element.values;

			GenCoordPrepSinglePass();
			InitLevel();
			UpdateMaterialShader();
			Calc();
		}

		public override void ResetUI()
		{
			var sliderObj = UIPanel.transform.Find("Threshold/Slider").GetComponent<Slider>();

			if (sliderObj != null && element != null)
			{
				sliderObj.minValue = element.min;
				sliderObj.maxValue = element.max;
				sliderObj.value = threshold;
			}
		}

		private void OnDestroy()
		{
			if (tablesBuffer != null) tablesBuffer.Dispose();
			if (cvBufferSingle != null) cvBufferSingle.Dispose();
			if (vertexBufferSingle != null) vertexBufferSingle.Dispose();
			if (counterBufferSingle != null) counterBufferSingle.Dispose();
			if (counterCheckBufferSingle != null) counterCheckBufferSingle.Dispose();

			if (material != null) Destroy(material);
			if (singleMesh != null) Destroy(singleMesh);
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent() || values == null) return;

			threshold = min + (max - min) * slider;

			if(activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Updates or creates the shared material used for rendering the isosurface mesh.
		/// Dynamically configures material properties (like culling and shadows) if running under URP.
		/// </summary>
		public void UpdateMaterialShader()
		{
			if (CachedMeshRenderer == null) return;

#if UNITY_EDITOR
			EnsureCorrectShader();
#endif

			if (material == null || (renderShader != null && material.shader != renderShader))
			{
				if (material != null)
				{
					if (Application.isPlaying) Destroy(material);
					else DestroyImmediate(material);
				}

				if (renderShader != null) material = new Material(renderShader);
				else material = new Material(Shader.Find("Standard"));

				bool isURP = GraphicsSettings.renderPipelineAsset != null;

				if (isURP && material.HasProperty("_Cull"))
				{
					material.SetFloat("_Cull", 0);           // 0 = Cull Off
					material.SetFloat("_ReceiveShadows", 1);
					material.SetFloat("_Surface", 0);
				}

				CachedMeshRenderer.sharedMaterial = material;
			}
		}

		/// <summary>
		/// Updates the isosurface extraction threshold from an external UI source.
		/// </summary>
		public void SetValue(float value)
		{
			if (values == null) return;

			threshold = Mathf.Clamp(value, min, max);

			if (max > min)
			{
				slider = (threshold - min) / (max - min);
			}

			if(activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Initializes the threshold level to the average value of the dataset.
		/// </summary>
		private void InitLevel()
		{
			min = element.min;
			max = element.max;
			threshold = element.average;
			threshold = Mathf.Clamp(threshold, min, max);

			if (max > min) slider = (threshold - min) / (max - min);
			else slider = 0.5f;
		}

		/// <summary>
		/// Main calculation router. Executes the Single Pass Marching Cubes.
		/// </summary>
		public void Calc()
		{
			if (values == null || coords == null) return;
			if (cvBufferSingle == null) return;

			SinglePassCalc();
		}

		/// <summary>
		/// Prepares coordinate and value data in a unified ComputeBuffer for high-performance single-pass GPU execution.
		/// Safely reuses buffers if the spatial volume dimensions remain constant, preventing mobile driver memory crashes.
		/// </summary>
		private void GenCoordPrepSinglePass()
		{
			int volumeSize = dims[0] * dims[1] * dims[2];
			if (volumeSize <= 0) return;

			if (cvBufferSingle == null || currentVolumeSize != volumeSize)
			{
				if (cvBufferSingle != null) cvBufferSingle.Dispose();
				cvBufferSingle = new ComputeBuffer(volumeSize, sizeof(float) * 4);
				currentVolumeSize = volumeSize;
			}

			Vector4[] cv = new Vector4[volumeSize];
			Parallel.For(0, volumeSize, i => {
				cv[i] = new Vector4(coords[i * 3 + 0], coords[i * 3 + 1], coords[i * 3 + 2], values[i]);
			});
			cvBufferSingle.SetData(cv);

			if (CachedMeshRenderer != null)
			{
				CachedMeshRenderer.enabled = true;
			}
		}

		/// <summary>
		/// Executes the Marching Cubes algorithm utilizing the entire volume in a single Compute Shader dispatch.
		/// </summary>
		private void SinglePassCalc()
		{
			int maxVerts = maximumVertexNum;
			int kernel = shader.FindKernel("Calc");

			shader.SetInts("dims", dims);
			shader.SetFloat("_min", element.min);
			shader.SetFloat("_max", element.max);
			shader.SetInt("maximumVertexNum", maxVerts);
			shader.SetFloat("threshold", threshold);

			counterBufferSingle.SetCounterValue(0);
			shader.SetBuffer(kernel, "counter", counterBufferSingle);
			shader.SetBuffer(kernel, "tables", tablesBuffer);
			shader.SetBuffer(kernel, "cvBuffer", cvBufferSingle);
			shader.SetBuffer(kernel, "vertices", vertexBufferSingle);

			uint sx, sy, sz;
			shader.GetKernelThreadGroupSizes(kernel, out sx, out sy, out sz);
			int _x = (dims[0] + (int)sx - 1) / (int)sx;
			int _y = (dims[1] + (int)sy - 1) / (int)sy;
			int _z = (dims[2] + (int)sz - 1) / (int)sz;

			shader.Dispatch(kernel, _x, _y, _z);

			ComputeBuffer.CopyCount(counterBufferSingle, counterCheckBufferSingle, 0);
			uint[] countData = new uint[1];
			counterCheckBufferSingle.GetData(countData); // Blocks CPU until GPU is done
			triCount = (int)countData[0];

			int validVertexCount = Mathf.Min(triCount * 3, maxVerts);

			if (validVertexCount == 0)
			{
				validVertexCount = 3;
			}

			singleMesh.SetSubMesh(0, new SubMeshDescriptor(0, validVertexCount), MeshUpdateFlags.DontRecalculateBounds);
			var scale = transform.localScale;
			var v0 = Vector3.Scale(element.boundMin, scale);
			var v1 = Vector3.Scale(element.boundMax, scale);
			singleMesh.bounds = new UnityEngine.Bounds(v0 + (v1 - v0) / 2, (v1 - v0) * 2);
		}
	}
}