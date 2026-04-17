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
		SerializedProperty chunkSize;
		SerializedProperty forceChunkModeOnPC;
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
			chunkSize   = serializedObject.FindProperty("chunkSize");
			forceChunkModeOnPC = serializedObject.FindProperty("forceChunkModeOnPC");
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

			EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

			forceChunkModeOnPC.boolValue = EditorGUILayout.ToggleLeft("Force Chunk Mode on PC (Test for Mobile)", forceChunkModeOnPC.boolValue);

			GUILayout.Space(5f);

			if (forceChunkModeOnPC.boolValue)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(chunkSize, new GUIContent("Chunk Size (Mobile safe: 64-96)"));

				EditorGUI.indentLevel--;
			}

			EditorGUI.EndDisabledGroup();

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

		[SerializeField] public bool forceChunkModeOnPC = false;
		[Range(32, 128)] public int chunkSize = 96;

		public ComputeShader shader = null;
		ComputeBuffer tablesBuffer;
		int[] packedTables;

		private bool isCalculating = false;
		private bool needsRecalculation = false;
		private bool abortChunking = false;

		private int maximumVertexNum;

		ComputeBuffer  cvBufferSingle;
		GraphicsBuffer vertexBufferSingle;
		ComputeBuffer  counterBufferSingle;
		ComputeBuffer  counterCheckBufferSingle;
		Mesh singleMesh;

		private Coroutine  chunkCoroutine;
		private GameObject chunkParent;

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

		protected override void Reset()
		{
#if UNITY_EDITOR
			base.Reset();

			EnsureCorrectShader();
#endif
		}

#if UNITY_EDITOR
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
				// URP Environment: Safely use Shader.Find since it's a Unity standard built-in shader.
				string expectedShaderName = "Universal Render Pipeline/Particles/Lit";

				if (renderShader == null || renderShader.name != expectedShaderName)
				{
					renderShader = Shader.Find(expectedShaderName);
				}
			}
			else
			{
				// Built-in Environment: Avoid Shader.Find to prevent shader stripping during build.
				// Restore directly from the member variable registered in the Prefab.
				if (renderShader == null || renderShader != builtinCustomShader)
				{
					renderShader = builtinCustomShader;
				}
			}
		}
#endif

		public override void InitModule()
		{
			int vertexStride = 40;
			long maxBufferBytes = SystemInfo.maxGraphicsBufferSize;
			long maxTheoreticalVerts = (long)(maxBufferBytes * 0.8f) / vertexStride;
			int practicalCap = 15000000;
#if UNITY_ANDROID && !UNITY_EDITOR
			practicalCap = 3000000;
#endif
			int calculatedVerts = (int)Math.Min(maxTheoreticalVerts, practicalCap);
			maximumVertexNum = (calculatedVerts / 3) * 3;

			dims = new int[3];
			triCount = 0;

			var isosurfaceTables = new IsosurfaceV5Tables();
			packedTables = isosurfaceTables.PackingTables();
			tablesBuffer = new ComputeBuffer(packedTables.Length, sizeof(int));
			tablesBuffer.SetData(packedTables);

			singleMesh = new Mesh();
			singleMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

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

			if (!IsChunkMode())
			{
				GenCoordPrepSinglePass();
			}

			Calc();
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (!pdf.dataLoaded) return;

			if (IsChunkMode() && isCalculating)
			{
				needsRecalculation = true;
				abortChunking = true;

				return;
			}

			element = pdf.elements[0];
			dims    = element.dims;
			coords  = element.coords[3];
			values  = element.values;

			if (!IsChunkMode())
			{
				GenCoordPrepSinglePass();
			}

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
			if (tablesBuffer != null)
			{
				tablesBuffer.Dispose();
			}

			DisposeSinglePassBuffers();

			if (material != null)
			{
				Destroy(material);
			}
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent() || values == null) return;

			threshold = min + (max - min) * slider;

			UpdateMaterialShader();

			Calc();

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
					if (Application.isPlaying)
					{
						Destroy(material);
					}
					else
					{
						DestroyImmediate(material);
					}
				}

				if (renderShader != null)
				{
					material = new Material(renderShader);
				}
				else
				{
					material = new Material(Shader.Find("Standard"));
				}

				// Apply specific properties programmatically only for URP to match the custom built-in shader's behavior.
				bool isURP = GraphicsSettings.renderPipelineAsset != null;

				if (isURP && material.HasProperty("_Cull"))
				{
					material.SetFloat("_Cull", 0);           // 0 = Cull Off (Double-sided rendering)
					material.SetFloat("_ReceiveShadows", 1); // Enable receiving shadows
					material.SetFloat("_Surface", 0);        // 0 = Opaque surface type
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

			if (max > min)
			{
				slider = (threshold - min) / (max - min);
			}
			else
			{
				slider = 0.5f;
			}
		}

		/// <summary>
		/// Determines whether the chunking method is required based on platform or editor settings.
		/// </summary>
		private bool IsChunkMode()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			return true;
#else
			return forceChunkModeOnPC;
#endif
		}

		/// <summary>
		/// Main calculation router. Delegates the process to either Single Pass or Chunk Mode based on device constraints.
		/// </summary>
		public void Calc()
		{
			if (values == null || coords == null) return;

			if (!IsChunkMode() && cvBufferSingle == null) return;

			if (IsChunkMode())
			{
				if (isCalculating)
				{
					needsRecalculation = true;
					abortChunking = true;

					return;
				}

				isCalculating = true;
				needsRecalculation = false;
				abortChunking = false;
				chunkCoroutine = StartCoroutine(BuildChunksRoutine());
			}
			else
			{
				SinglePassCalc();
			}
		}

		/// <summary>
		/// Prepares coordinate and value data in a unified ComputeBuffer for high-performance single-pass GPU execution.
		/// </summary>
		private void GenCoordPrepSinglePass()
		{
			DisposeSinglePassBuffers();

			int maxVerts = maximumVertexNum;
			cvBufferSingle = new ComputeBuffer(dims[0] * dims[1] * dims[2], sizeof(float) * 4);
			Vector4[] cv = new Vector4[dims[0] * dims[1] * dims[2]];
			Parallel.For(0, dims[0] * dims[1] * dims[2], i => {
				cv[i] = new Vector4(coords[i * 3 + 0], coords[i * 3 + 1], coords[i * 3 + 2], values[i]);
			});
			cvBufferSingle.SetData(cv);

			counterBufferSingle      = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
			counterCheckBufferSingle = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);

			singleMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
			var vp = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
			var vn = new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3);
			var vc = new VertexAttributeDescriptor(VertexAttribute.Color,    VertexAttributeFormat.Float32, 4);
			singleMesh.SetVertexBufferParams(maxVerts, vp, vn, vc);
			singleMesh.SetIndexBufferParams(maxVerts, IndexFormat.UInt32);
			singleMesh.SetSubMesh(0, new SubMeshDescriptor(0, maxVerts), MeshUpdateFlags.DontRecalculateBounds);
			vertexBufferSingle = singleMesh.GetVertexBuffer(0);

			int[] inds = new int[maxVerts];

			for (int i = 0; i < maxVerts; i++)
			{
				inds[i] = i;
			}

			singleMesh.SetIndices(inds, MeshTopology.Triangles, 0);

			if (CachedMeshRenderer != null)
			{
				CachedMeshRenderer.enabled = true;
			}

			if (chunkParent != null)
			{
				Destroy(chunkParent);
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
			counterCheckBufferSingle.GetData(countData);
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

			if (CachedMeshFilter != null)
			{
				CachedMeshFilter.sharedMesh = singleMesh;
			}
		}

		private void DisposeSinglePassBuffers()
		{
			if (cvBufferSingle != null)
			{
				cvBufferSingle.Dispose();
			}

			if (vertexBufferSingle != null)
			{
				vertexBufferSingle.Dispose();
			}

			if (counterBufferSingle != null)
			{
				counterBufferSingle.Dispose();
			}

			if (counterCheckBufferSingle != null)
			{
				counterCheckBufferSingle.Dispose();
			}
		}

		/// <summary>
		/// Executes the Marching Cubes algorithm in segmented chunks to bypass mobile graphics memory limitations.
		/// Utilizes AsyncGPUReadback to prevent pipeline stalling.
		/// </summary>
		private IEnumerator BuildChunksRoutine()
		{
			if (CachedMeshRenderer != null)
			{
				CachedMeshRenderer.enabled = false;
			}

			if (chunkParent != null)
			{
				Destroy(chunkParent);
			}

			chunkParent = new GameObject("Isosurface_Chunks");
			chunkParent.transform.SetParent(this.transform, false);

			int maxVertsPerChunk = Math.Min(maximumVertexNum, 65536 * 45);
			maxVertsPerChunk = (maxVertsPerChunk / 3) * 3;
			shader.SetInt("maximumVertexNum", maxVertsPerChunk);

			int maxVoxelCount = (chunkSize + 1) * (chunkSize + 1) * (chunkSize + 1);
			var chunkCvBuffer = new ComputeBuffer(maxVoxelCount, sizeof(float) * 4);
			var chunkVertBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, maxVertsPerChunk * 10, 4);
			var chunkCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
			var chunkCounterCheck = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);

			int kernel = shader.FindKernel("Calc");
			shader.SetBuffer(kernel, "tables", tablesBuffer);
			shader.SetFloat("_min", element.min);
			shader.SetFloat("_max", element.max);
			shader.SetFloat("threshold", threshold);

			int numX = Mathf.CeilToInt((float)(dims[0] - 1) / chunkSize);
			int numY = Mathf.CeilToInt((float)(dims[1] - 1) / chunkSize);
			int numZ = Mathf.CeilToInt((float)(dims[2] - 1) / chunkSize);

			int totalTris = 0;

			for (int cz = 0; cz < numZ; cz++)
			{
				for (int cy = 0; cy < numY; cy++)
				{
					for (int cx = 0; cx < numX; cx++)
					{
						if (abortChunking) goto EndCoroutine;

						int startX = cx * chunkSize;
						int endX   = Mathf.Min(startX + chunkSize + 1, dims[0]);
						int sizeX  = endX - startX;
						int startY = cy * chunkSize;
						int endY   = Mathf.Min(startY + chunkSize + 1, dims[1]);
						int sizeY  = endY - startY;
						int startZ = cz * chunkSize;
						int endZ   = Mathf.Min(startZ + chunkSize + 1, dims[2]);
						int sizeZ  = endZ - startZ;

						Vector4[] localData = null;
						var task = Task.Run(() =>
						{
							localData = new Vector4[sizeX * sizeY * sizeZ];
							for (int lz = 0; lz < sizeZ; lz++)
							{
								for (int ly = 0; ly < sizeY; ly++)
								{
									for (int lx = 0; lx < sizeX; lx++)
									{
										int gIdx = (dims[1] * (startZ + lz) + (startY + ly)) * dims[0] + (startX + lx);
										int lIdx = (sizeY * lz + ly) * sizeX + lx;
										localData[lIdx] = new Vector4(coords[gIdx * 3], coords[gIdx * 3 + 1], coords[gIdx * 3 + 2], values[gIdx]);
									}
								}
							}
						});

						yield return new WaitUntil(() => task.IsCompleted);

						if (abortChunking) goto EndCoroutine;

						chunkCvBuffer.SetData(localData, 0, 0, localData.Length);
						shader.SetInts("dims", new int[] { sizeX, sizeY, sizeZ });
						chunkCounter.SetCounterValue(0);
						shader.SetBuffer(kernel, "cvBuffer", chunkCvBuffer);
						shader.SetBuffer(kernel, "vertices", chunkVertBuffer);
						shader.SetBuffer(kernel, "counter", chunkCounter);

						int tx = (sizeX + 7) / 8; int ty = (sizeY + 7) / 8;
						shader.Dispatch(kernel, tx, ty, sizeZ);

						ComputeBuffer.CopyCount(chunkCounter, chunkCounterCheck, 0);
						var reqCount = UnityEngine.Rendering.AsyncGPUReadback.Request(chunkCounterCheck);
						yield return new WaitUntil(() => reqCount.done);
						if (abortChunking) goto EndCoroutine;

						uint triCountLocal = reqCount.GetData<uint>()[0];
						uint vertCount = triCountLocal * 3;

						if (vertCount > 0)
						{
							int byteSize = (int)vertCount * 40;
							var reqVerts = UnityEngine.Rendering.AsyncGPUReadback.Request(chunkVertBuffer, byteSize, 0);
							yield return new WaitUntil(() => reqVerts.done);

							if (abortChunking) goto EndCoroutine;

							var vertData = reqVerts.GetData<VertexData>();

							Mesh m = new Mesh();
							m.indexFormat = IndexFormat.UInt32;
							var layout = new[]
							{
								new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
								new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
								new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4)
							};
							m.SetVertexBufferParams((int)vertCount, layout);
							m.SetVertexBufferData(vertData, 0, 0, (int)vertCount);

							int[] inds = new int[vertCount];

							for (int i = 0; i < vertCount; i++)
							{
								inds[i] = i;
							}

							m.SetIndexBufferParams((int)vertCount, IndexFormat.UInt32);
							m.SetIndexBufferData(inds, 0, 0, (int)vertCount);
							m.SetSubMesh(0, new SubMeshDescriptor(0, (int)vertCount));
							m.RecalculateBounds();

							GameObject chunkGO = new GameObject($"Chunk_{cx}_{cy}_{cz}");
							chunkGO.transform.SetParent(chunkParent.transform, false);
							chunkGO.AddComponent<MeshFilter>().sharedMesh = m;
							var chunkMr = chunkGO.AddComponent<MeshRenderer>();

							chunkMr.sharedMaterial = material;

							totalTris += (int)triCountLocal;
						}
					}
				}
			}

		EndCoroutine:
			chunkCvBuffer.Dispose();
			chunkVertBuffer.Dispose();
			chunkCounter.Dispose();
			chunkCounterCheck.Dispose();

			triCount = totalTris;
			isCalculating = false;

			if (needsRecalculation)
			{
				Calc();
			}
		}
	}
}