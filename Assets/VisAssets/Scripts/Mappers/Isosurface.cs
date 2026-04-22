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

#if UNITY_EDITOR
	[CustomEditor(typeof(Isosurface))]
	public class IsosurfaceEditor : Editor
	{
		SerializedProperty slider, threshold, min, max, triCount, shader, builtinCustomshader, rendershader;

		private void OnEnable()
		{
			slider = serializedObject.FindProperty("slider");
			threshold = serializedObject.FindProperty("threshold");
			min = serializedObject.FindProperty("min");
			max = serializedObject.FindProperty("max");
			triCount = serializedObject.FindProperty("triCount");
			shader = serializedObject.FindProperty("shader");
			builtinCustomshader = serializedObject.FindProperty("builtinCustomShader");
			rendershader = serializedObject.FindProperty("renderShader");
		}

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
					if (_threshold != threshold.floatValue) isosurface.SetValue(_threshold);
					isosurface.UpdateMaterialShader();
				}
				EditorUtility.SetDirty(target);
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class Isosurface : MapperModuleTemplate
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct VertexData
		{
			public Vector3 pos; public Vector3 norm; public Vector4 col;
		}

		DataElement element;
		int[] dims;
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
		private bool isCalculating = false;
		private bool needsRecalculation = false;

		ComputeBuffer cvBufferSingle;
		ComputeBuffer vertexBufferSingle;
		ComputeBuffer counterBufferSingle;
		ComputeBuffer counterCheckBufferSingle;
		Mesh singleMesh;

		private MeshFilter _cachedMeshFilter;
		private MeshFilter CachedMeshFilter => _cachedMeshFilter ??= GetComponent<MeshFilter>();
		private MeshRenderer _cachedMeshRenderer;
		private MeshRenderer CachedMeshRenderer => _cachedMeshRenderer ??= GetComponent<MeshRenderer>();

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
			EnsureCorrectShader();
		}

		private void EnsureCorrectShader()
		{
			bool isURP = GraphicsSettings.renderPipelineAsset != null;
			if (isURP)
			{
				string expectedShaderName = "Universal Render Pipeline/Particles/Lit";
				if (renderShader == null || renderShader.name != expectedShaderName) renderShader = Shader.Find(expectedShaderName);
			}
			else
			{
				if (renderShader == null || renderShader != builtinCustomShader) renderShader = builtinCustomShader;
			}
		}
#endif

		public override void InitModule()
		{
			if (tablesBuffer != null) return;

			int vertexStride = 40;
			long maxBufferBytes = SystemInfo.maxGraphicsBufferSize;
			if (maxBufferBytes <= 0) maxBufferBytes = 128 * 1024 * 1024;

			long maxTheoreticalVerts = (long)(maxBufferBytes * 0.8f) / vertexStride;
			int practicalCap = 1500000;
			maximumVertexNum = (int)Math.Min(maxTheoreticalVerts, practicalCap);
			maximumVertexNum = (maximumVertexNum / 3) * 3;
			if (maximumVertexNum <= 0) maximumVertexNum = 65536 * 3;

			dims = new int[3];
			triCount = 0;

			var isosurfaceTables = new IsosurfaceV5Tables();
			packedTables = isosurfaceTables.PackingTables();
			tablesBuffer = new ComputeBuffer(packedTables.Length, sizeof(int));
			tablesBuffer.SetData(packedTables);

			vertexBufferSingle = new ComputeBuffer(maximumVertexNum * 10, 4, ComputeBufferType.Raw);
			counterBufferSingle = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
			counterCheckBufferSingle = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);

			singleMesh = new Mesh();
			singleMesh.indexFormat = IndexFormat.UInt32;

			if (CachedMeshFilter != null) CachedMeshFilter.sharedMesh = singleMesh;
			UpdateMaterialShader();
		}

		public override int BodyFunc()
		{
			Calc();
			return 1;
		}

		public override void IdleFunc() { }

		public override void SetParameters()
		{
			if (!pdf.dataLoaded) return;
			if (dims != null && pdf.elements != null && pdf.elements.Length > 0)
			{
				if (dims[0] != pdf.elements[0].dims[0] || dims[1] != pdf.elements[0].dims[1] || dims[2] != pdf.elements[0].dims[2])
				{
					SoftRebuild();
					return;
				}
			}
			Calc();
		}

		private void SoftRebuild()
		{
			StopAllCoroutines();
			isCalculating = false;
			needsRecalculation = false;

			element = pdf.elements[0];
			dims = element.dims;
			coords = element.coords[3];
			values = element.values;
			min = element.min;
			max = element.max;

			threshold = Mathf.Clamp(threshold, min, max);
			slider = (max > min) ? (threshold - min) / (max - min) : 0.5f;

			GenCoordPrepSinglePass();
			Calc();
		}

		public override void GetParameters() { }

		public override void ReSetParameters()
		{
			if (!pdf.dataLoaded) return;

			StopAllCoroutines();
			isCalculating = false;
			needsRecalculation = false;

			element = pdf.elements[0];
			dims = element.dims;
			coords = element.coords[3];
			values = element.values;

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
			StopAllCoroutines();

			if (CachedMeshFilter != null) CachedMeshFilter.sharedMesh = null;
			if (CachedMeshRenderer != null) CachedMeshRenderer.sharedMaterial = null;

			tablesBuffer = null;
			cvBufferSingle = null;
			vertexBufferSingle = null;
			counterBufferSingle = null;
			counterCheckBufferSingle = null;

			if (material != null) Destroy(material);
			if (singleMesh != null) Destroy(singleMesh);
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent() || values == null) return;
			threshold = min + (max - min) * slider;
			if (activation != null) activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

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

				material = (renderShader != null) ? new Material(renderShader) : new Material(Shader.Find("Standard"));

				if (GraphicsSettings.renderPipelineAsset != null && material.HasProperty("_Cull"))
				{
					material.SetFloat("_Cull", 0);
					material.SetFloat("_ReceiveShadows", 1);
					material.SetFloat("_Surface", 0);
				}
				CachedMeshRenderer.sharedMaterial = material;
			}
		}

		public void SetValue(float value)
		{
			if (values == null) return;
			threshold = Mathf.Clamp(value, min, max);
			if (max > min) slider = (threshold - min) / (max - min);
			if (activation != null) activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

		private void InitLevel()
		{
			min = element.min;
			max = element.max;
			threshold = Mathf.Clamp(element.average, min, max);
			slider = (max > min) ? (threshold - min) / (max - min) : 0.5f;
		}

		public void Calc()
		{
			if (values == null || coords == null || cvBufferSingle == null) return;
			if (isCalculating)
			{
				needsRecalculation = true;
				return;
			}
			StartCoroutine(SinglePassCalcRoutine());
		}

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

			if (CachedMeshRenderer != null) CachedMeshRenderer.enabled = true;
		}

		/// <summary>
		/// Uses AsyncGPUReadback to safely fetch vertices from a dedicated ComputeBuffer.
		/// Completely isolates Compute Shader from the Rendering Pipeline to prevent driver freezes.
		/// </summary>
		private IEnumerator SinglePassCalcRoutine()
		{
			isCalculating = true;
			needsRecalculation = false;

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

			var reqCount = UnityEngine.Rendering.AsyncGPUReadback.Request(counterCheckBufferSingle);
			yield return new WaitUntil(() => reqCount.done);

			if (reqCount.hasError)
			{
				isCalculating = false;
				yield break;
			}

			uint triCountLocal = reqCount.GetData<uint>()[0];
			uint vertCount = triCountLocal * 3;
			triCount = (int)triCountLocal;

			int validVertexCount = Mathf.Min((int)vertCount, maxVerts);

			if (validVertexCount >= 3)
			{
				int byteSize = validVertexCount * 40;
				var reqVerts = UnityEngine.Rendering.AsyncGPUReadback.Request(vertexBufferSingle, byteSize, 0);

				yield return new WaitUntil(() => reqVerts.done);

				if (!reqVerts.hasError && singleMesh != null)
				{
					var vertData = reqVerts.GetData<VertexData>();

					var layout = new[]
					{
						new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
						new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
						new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4)
					};

					singleMesh.SetVertexBufferParams(validVertexCount, layout);
					singleMesh.SetVertexBufferData(vertData, 0, 0, validVertexCount);

					int[] inds = new int[validVertexCount];
					for (int i = 0; i < validVertexCount; i++) inds[i] = i;

					singleMesh.SetIndexBufferParams(validVertexCount, IndexFormat.UInt32);
					singleMesh.SetIndexBufferData(inds, 0, 0, validVertexCount);

					singleMesh.SetSubMesh(0, new SubMeshDescriptor(0, validVertexCount), MeshUpdateFlags.DontRecalculateBounds);

					var scale = transform.localScale;
					var v0 = Vector3.Scale(element.boundMin, scale);
					var v1 = Vector3.Scale(element.boundMax, scale);
					singleMesh.bounds = new UnityEngine.Bounds(v0 + (v1 - v0) / 2, (v1 - v0) * 2);
				}
			}
			else if (singleMesh != null)
			{
				singleMesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), MeshUpdateFlags.DontRecalculateBounds);
			}

			isCalculating = false;
			if (needsRecalculation) Calc();
		}
	}
}