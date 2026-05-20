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
		SerializedProperty slider, threshold, min, max, shadingMode, useGPU, vramOptimization, triCount, shader;
		SerializedProperty builtinMaterial, urpMaterial;

		private void OnEnable()
		{
			slider      = serializedObject.FindProperty("slider");
			threshold   = serializedObject.FindProperty("threshold");
			min         = serializedObject.FindProperty("min");
			max         = serializedObject.FindProperty("max");
			shadingMode = serializedObject.FindProperty("shadingMode");
			triCount    = serializedObject.FindProperty("triCount");
			useGPU      = serializedObject.FindProperty("useGPU");
			vramOptimization = serializedObject.FindProperty("vramOptimization");
			shader      = serializedObject.FindProperty("shader");
			builtinMaterial = serializedObject.FindProperty("builtinMaterial");
			urpMaterial     = serializedObject.FindProperty("urpMaterial");
		}

		public override void OnInspectorGUI()
		{
			var isosurface = target as Isosurface;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);

			float _threshold;
			if (EditorApplication.isPlaying && max.floatValue > min.floatValue)
			{
				_threshold = EditorGUILayout.Slider("Threshold: ", threshold.floatValue, min.floatValue, max.floatValue);
			}
			else
			{
				_threshold = EditorGUILayout.FloatField("Threshold: ", threshold.floatValue);
			}

			GUILayout.Space(5f);

			EditorGUILayout.LabelField("Triangles : " + (triCount.intValue).ToString());

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

			useGPU.boolValue = EditorGUILayout.ToggleLeft("Enable GPU Acceleration", useGPU.boolValue);

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(!useGPU.boolValue);
			EditorGUI.indentLevel++;
			vramOptimization.boolValue = EditorGUILayout.ToggleLeft("Enable VRAM Optimization (4-float mode)", vramOptimization.boolValue);
			EditorGUI.indentLevel--;
			EditorGUI.EndDisabledGroup();

			EditorGUI.EndDisabledGroup();

			GUILayout.Space(10f);

			EditorGUILayout.PropertyField(builtinMaterial, new GUIContent("Built-in Material"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(urpMaterial, new GUIContent("URP Material"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(shader, new GUIContent("Compute Shader"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

/*
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Isosurface");
				if (isosurface != null)
				{
					var prop = serializedObject.FindProperty("isThresholdInitialized");
					if (prop != null) prop.boolValue = true;

					if (_threshold != threshold.floatValue) isosurface.SetValue(_threshold);
					isosurface.UpdateMaterialShader();
				}
				EditorUtility.SetDirty(target);
			}
*/
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Isosurface");

				if (isosurface != null)
				{
					var prop = serializedObject.FindProperty("isThresholdInitialized");
					if (prop != null) prop.boolValue = true;

					if (_threshold != threshold.floatValue)
					{
						threshold.floatValue = _threshold;

						var sliderProp = serializedObject.FindProperty("slider");
						if (sliderProp != null && max.floatValue > min.floatValue)
						{
							sliderProp.floatValue = (_threshold - min.floatValue) / (max.floatValue - min.floatValue);
						}

						if (Application.isPlaying)
						{
							isosurface.SetValue(_threshold);
						}
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
		public enum SHADING_MODE
		{
			FLAT,
			SMOOTH
		};
/*
		[StructLayout(LayoutKind.Sequential)]
		public struct VertexData
		{
			public Vector3 pos;
			public Vector3 norm;
			public Vector4 col;
		}
*/
		DataElement element;
		int []  dims;
		float[] coords;
		float[] values;

		[Range(0f, 1f)]
		public float slider;
		[SerializeField, ReadOnly]
		public float threshold;
		[SerializeField, ReadOnly]
		public float min;
		[SerializeField, ReadOnly]
		public float max = 1f;
		[SerializeField] public Color color;
		[SerializeField] public SHADING_MODE shadingMode;
		[SerializeField] public int triCount;

		[SerializeField] private Material builtinMaterial;
		[SerializeField] private Material urpMaterial;

		int cell_i, cell_j, cell_k;

		Mesh mesh;
		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		int[] indices;

		public ComputeShader shader = null;
		ComputeBuffer  tablesBuffer;

		ComputeBuffer  cvmBuffer;
		ComputeBuffer  cvBufferSingle;

		ComputeBuffer  dummyFloatBuffer;
		ComputeBuffer  dummyFloat4Buffer;

		GraphicsBuffer vertexBuffer;
		ComputeBuffer  counterBuffer;
		ComputeBuffer  counterCheckBuffer;

#if UNITY_ANDROID
		int maximumVertexNum = 65536 * 15;
#else
		int maximumVertexNum = 65536 * 63;
#endif

		int[]   packedTables;
		int     currentVolumeSize = -1;
		private bool isCalculating = false;
		private bool needsRecalculation = false;

		[SerializeField, HideInInspector]
		private bool isThresholdInitialized = false;

		[SerializeField] public bool useGPU;
		[SerializeField] public bool vramOptimization = true;

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
		}
#endif

		public override void InitModule()
		{
			if (tablesBuffer != null) return;

			dims     = new int[3];
			mesh     = new Mesh();
			triCount = 0;

			if (dummyFloatBuffer == null)
			{
				dummyFloatBuffer = new ComputeBuffer(1, sizeof(float));
			}

			if (dummyFloat4Buffer == null)
			{
				dummyFloat4Buffer = new ComputeBuffer(1, sizeof(float) * 4);
			}

			var isosurfaceTables = new IsosurfaceV5Tables();
			packedTables = isosurfaceTables.PackingTables();

			if (!useGPU)
			{
				vertices = new List<Vector3>();
				normals  = new List<Vector3>();
				colors   = new List<Color>();
				mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			}
			else
			{
				tablesBuffer  = new ComputeBuffer(packedTables.Length, sizeof(int));
				counterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
				counterCheckBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
				counterBuffer.SetCounterValue(0);
				shader.SetInt("maximumVertexNum", maximumVertexNum);

				mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
				var vp = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
				var vn = new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3);
				var vc = new VertexAttributeDescriptor(VertexAttribute.Color,    VertexAttributeFormat.Float32, 4);

				mesh.SetVertexBufferParams(maximumVertexNum, vp, vn, vc);
				mesh.SetIndexBufferParams(maximumVertexNum, IndexFormat.UInt32);
				mesh.SetSubMesh(0, new SubMeshDescriptor(0, maximumVertexNum), MeshUpdateFlags.DontRecalculateBounds);

				vertexBuffer = mesh.GetVertexBuffer(0);
				indices = new int[maximumVertexNum];

				for (int i = 0; i < maximumVertexNum; i++)
				{
					indices[i] = i;
				}

				mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
				mesh.SetIndices(indices, MeshTopology.Triangles, 0);
			}

			UpdateMaterialShader();
		}

		public override int BodyFunc()
		{
			if (pdf.dataLoaded)
			{
				Draw();
			}

			return 1;
		}

		public override void SetParameters()
		{
			if (pdf.dataLoaded)
			{
				Calc();
			}
		}

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

			PrepareDataBuffer();
			InitLevel();
			UpdateMaterialShader();
			Calc();
		}

		private void OnDestroy()
		{
			StopAllCoroutines();

			if (TryGetComponent<MeshFilter>(out var filter))
			{
				filter.sharedMesh = null;
			}

			if (TryGetComponent<MeshRenderer>(out var renderer))
			{
				renderer.sharedMaterial = null;
			}

			DisposeBuffers();
		}

		public void UpdateMaterialShader()
		{
			var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ?? UnityEngine.QualitySettings.renderPipeline;
			bool isURP = pipeline != null;

			Material targetMaterial = isURP ? urpMaterial : builtinMaterial;

			if (TryGetComponent<MeshRenderer>(out var renderer))
			{
				renderer.sharedMaterial = targetMaterial;
			}
		}

		private void DisposeBuffers()
		{
			if (tablesBuffer != null) tablesBuffer.Dispose();
			if (counterBuffer != null) counterBuffer.Dispose();
			if (counterCheckBuffer != null) counterCheckBuffer.Dispose();
			if (vertexBuffer != null) vertexBuffer.Dispose();
			if (cvmBuffer != null) cvmBuffer.Dispose();
			if (cvBufferSingle != null) cvBufferSingle.Dispose();
			if (dummyFloatBuffer != null) dummyFloatBuffer.Dispose();
			if (dummyFloat4Buffer != null) dummyFloat4Buffer.Dispose();
		}

		private void PrepareDataBuffer()
		{
			if (!useGPU) return;

			int volumeSize = dims[0] * dims[1] * dims[2];

			if (volumeSize <= 0) return;

			if (vramOptimization)
			{
				shader.EnableKeyword("VRAM_OPTIMIZATION_ON");

				if (cvBufferSingle == null || currentVolumeSize != volumeSize)
				{
					if (cvBufferSingle != null)
					{
						cvBufferSingle.Dispose();
					}

					cvBufferSingle = new ComputeBuffer(volumeSize, sizeof(float) * 4);
					currentVolumeSize = volumeSize;
				}

				Vector4[] cv = new Vector4[volumeSize];

				Parallel.For(0, volumeSize, i => {
					cv[i] = new Vector4(coords[i * 3 + 0], coords[i * 3 + 1], coords[i * 3 + 2], values[i]);
				});

				cvBufferSingle.SetData(cv);
			}
			else
			{
				shader.DisableKeyword("VRAM_OPTIMIZATION_ON");

				if (cvmBuffer == null || currentVolumeSize != volumeSize)
				{
					if (cvmBuffer != null)
					{
						cvmBuffer.Dispose();
					}

					cvmBuffer = new ComputeBuffer(volumeSize * 13, sizeof(float));
					currentVolumeSize = volumeSize;
				}

				float[] cvmData = new float[volumeSize * 13];

				Parallel.For(0, volumeSize, i => {
					cvmData[i * 13 + 0] = coords[i * 3 + 0];
					cvmData[i * 13 + 1] = coords[i * 3 + 1];
					cvmData[i * 13 + 2] = coords[i * 3 + 2];
					cvmData[i * 13 + 3] = values[i];
					for (int n = 4; n < 13; n++) cvmData[i * 13 + n] = 0;
				});

				cvmBuffer.SetData(cvmData);

				int kernel = shader.FindKernel("GenCoordPrep");
				shader.SetInts("dims", dims);

				shader.SetBuffer(kernel, "cvm", cvmBuffer);
				shader.SetBuffer(kernel, "cvBuffer", dummyFloat4Buffer);

				uint sx, sy, sz;
				shader.GetKernelThreadGroupSizes(kernel, out sx, out sy, out sz);
				shader.Dispatch(kernel, (dims[0] + (int)sx - 1) / (int)sx, (dims[1] + (int)sy - 1) / (int)sy, (dims[2] + (int)sz - 1) / (int)sz);
			}
		}

		public void Calc()
		{
			if (element == null) return;
			if (useGPU && (tablesBuffer == null || vertexBuffer == null)) return;

			if (!useGPU)
			{
				RunCPUCalc();

				return;
			}

			if (isCalculating)
			{
				needsRecalculation = true;

				return;
			}

			StartCoroutine(CalcRoutine());
		}

		private IEnumerator CalcRoutine()
		{
			isCalculating = true;
			needsRecalculation = false;

			int kernel = shader.FindKernel("Calc");
			shader.SetFloat("threshold", threshold);
			shader.SetFloat("_min", element.min);
			shader.SetFloat("_max", element.max);
			shader.SetBool("useLegacyGPU", !vramOptimization);
			shader.SetInts("dims", dims);
			shader.SetInt("maximumVertexNum", maximumVertexNum);

			counterBuffer.SetCounterValue(0);
			shader.SetBuffer(kernel, "counter", counterBuffer);
			tablesBuffer.SetData(packedTables);
			shader.SetBuffer(kernel, "tables", tablesBuffer);
			shader.SetBuffer(kernel, "vertices", vertexBuffer);

			if (vramOptimization)
			{
				shader.EnableKeyword("VRAM_OPTIMIZATION_ON");
				shader.SetBuffer(kernel, "cvBuffer", cvBufferSingle);
				shader.SetBuffer(kernel, "cvm", dummyFloatBuffer);
			}
			else
			{
				shader.DisableKeyword("VRAM_OPTIMIZATION_ON");
				shader.SetBuffer(kernel, "cvBuffer", dummyFloat4Buffer);
				shader.SetBuffer(kernel, "cvm", cvmBuffer);
			}

			uint sx, sy, sz;
			shader.GetKernelThreadGroupSizes(kernel, out sx, out sy, out sz);
			shader.Dispatch(kernel, (dims[0] + (int)sx - 1) / (int)sx, (dims[1] + (int)sy - 1) / (int)sy, (dims[2] + (int)sz - 1) / (int)sz);

			ComputeBuffer.CopyCount(counterBuffer, counterCheckBuffer, 0);
			var reqCount = AsyncGPUReadback.Request(counterCheckBuffer);
			yield return new WaitUntil(() => reqCount.done);

			if (!reqCount.hasError)
			{
				triCount = (int)reqCount.GetData<uint>()[0];

				int validVertexCount = Mathf.Clamp(triCount * 3, 3, maximumVertexNum);

				if (mesh != null)
				{
					mesh.SetSubMesh(0, new SubMeshDescriptor(0, validVertexCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);
				}
			}

			isCalculating = false;
			if (needsRecalculation) Calc();
		}

		private int GetTriangleNum(int i) { return packedTables[i]; }
		private int GetEdgeEndVert(int j, int i) { return packedTables[256 + j * 2 + i]; }
		private int GetTriangle(int k, int j, int i) { return packedTables[256 + (12 * 2) + 3 * 4 * k + 3 * j + i]; }
//		private int GetIndex(int i, int j, int k) { return (dims[1] * k + j) * dims[0] + i; }
		private float GetCoord(int i, int j, int k, int axis) { return coords[GetIndex(i, j, k) * 3 + axis]; }
//		private float GetValue(int i, int j, int k) { return values[GetIndex(i, j, k)]; }

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		private int GetIndex(int i, int j, int k) { return (dims[1] * k + j) * dims[0] + i; }

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		private float GetValue(int i, int j, int k) { return values[GetIndex(i, j, k)]; }

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		private Vector3 GetCoordVec(int i, int j, int k)
		{
			int idx = GetIndex(i, j, k) * 3;
			return new Vector3(coords[idx], coords[idx + 1], coords[idx + 2]);
		}

		private void VertIntPosition(int vert, ref int i, ref int j, ref int k)
		{
			i = cell_i + (vert & 1);
			j = cell_j + ((vert >> 1) & 1);
			k = cell_k + ((vert >> 2) & 1);
		}

		private void RunCPUCalc()
		{
			vertices.Clear();
			normals.Clear();
			colors.Clear();
			triCount = 0;

			for (cell_k = 0; cell_k < dims[2] - 1; cell_k++)
			{
				for (cell_j = 0; cell_j < dims[1] - 1; cell_j++)
				{
					for (cell_i = 0; cell_i < dims[0] - 1; cell_i++)
					{
						UnitCube();
					}
				}
			}

			indices = new int[triCount * 3];

			for (int i = 0; i < triCount * 3; i++)
			{
				indices[i] = i;
			}

			Draw();
		}

		private Vector3 Grad(int i, int j, int k)
		{
			int iPrev = i == 0 ? 0 : i - 1;
			int iNext = i == dims[0] - 1 ? i : i + 1;
			int jPrev = j == 0 ? 0 : j - 1;
			int jNext = j == dims[1] - 1 ? j : j + 1;
			int kPrev = k == 0 ? 0 : k - 1;
			int kNext = k == dims[2] - 1 ? k : k + 1;

			float divI = (i == 0 || i == dims[0] - 1) ? 1f : 2f;
			float divJ = (j == 0 || j == dims[1] - 1) ? 1f : 2f;
			float divK = (k == 0 || k == dims[2] - 1) ? 1f : 2f;

			float dfd1 = (GetValue(iNext, j, k) - GetValue(iPrev, j, k)) / divI;
			float dfd2 = (GetValue(i, jNext, k) - GetValue(i, jPrev, k)) / divJ;
			float dfd3 = (GetValue(i, j, kNext) - GetValue(i, j, kPrev)) / divK;

			Vector3 vPrevX = GetCoordVec(iPrev, j, k); Vector3 vNextX = GetCoordVec(iNext, j, k);
			Vector3 vPrevY = GetCoordVec(i, jPrev, k); Vector3 vNextY = GetCoordVec(i, jNext, k);
			Vector3 vPrevZ = GetCoordVec(i, j, kPrev); Vector3 vNextZ = GetCoordVec(i, j, kNext);

			Vector3 dx = (vNextX - vPrevX) / divI;
			Vector3 dy = (vNextY - vPrevY) / divJ;
			Vector3 dz = (vNextZ - vPrevZ) / divK;

			float jac = dx.x * (dy.y * dz.z - dy.z * dz.y) - dy.x * (dx.y * dz.z - dx.z * dz.y) + dz.x * (dx.y * dy.z - dx.z * dy.y);

			if (jac == 0f) return Vector3.zero;

			float gx = ((dy.y * dz.z - dy.z * dz.y) * dfd1 + (dy.z * dx.z - dx.y * dz.z) * dfd2 + (dx.y * dy.z - dy.y * dx.z) * dfd3) / jac;
			float gy = ((dz.y * dy.x - dy.y * dz.x) * dfd1 + (dx.x * dz.z - dz.x * dx.z) * dfd2 + (dy.x * dx.z - dx.x * dy.z) * dfd3) / jac;
			float gz = ((dy.x * dz.y - dz.x * dy.y) * dfd1 + (dz.x * dx.y - dx.x * dz.y) * dfd2 + (dx.x * dy.y - dy.x * dx.y) * dfd3) / jac;

			Vector3 g = new Vector3(gx, gy, gz);
			float gg = g.magnitude;

			return gg > 0.0f ? -g / gg : Vector3.zero;
		}

		private void CrossPoint(int edge, out Vector3 vert, out Vector3 norm)
		{
			int i0 = 0, j0 = 0, k0 = 0, i1 = 0, j1 = 0, k1 = 0;
			VertIntPosition(GetEdgeEndVert(edge, 0), ref i0, ref j0, ref k0);
			VertIntPosition(GetEdgeEndVert(edge, 1), ref i1, ref j1, ref k1);

			float val0 = GetValue(i0, j0, k0);
			float val1 = GetValue(i1, j1, k1);

			float weight0 = (val1 != val0) ? (val1 - threshold) / (val1 - val0) : 0f;
			float weight1 = 1f - weight0;

			Vector3 p0 = GetCoordVec(i0, j0, k0);
			Vector3 p1 = GetCoordVec(i1, j1, k1);
			vert = p0 * weight0 + p1 * weight1;

			Vector3 n0 = Grad(i0, j0, k0);
			Vector3 n1 = Grad(i1, j1, k1);
			norm = n0 * weight0 + n1 * weight1;
		}

		private void AddTriangleGeom(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 n0, Vector3 n1, Vector3 n2)
		{
			vertices.Add(v0);
			vertices.Add(v1);
			vertices.Add(v2);
			normals.Add(n0);
			normals.Add(n1);
			normals.Add(n2);

			Color c = GetColor();
			colors.Add(c);
			colors.Add(c);
			colors.Add(c);
		}

		private Color GetColor()
		{
			float level = (element.max - threshold) / (element.max - element.min);
			float r=0f, g=0f, b=0f, a=1f;

			if (level < 0.5f)
			{
				r = 0f;
			}
			else if (level >= 0.5f && level < 5f / 6f)
			{
				r = 6f * (level - 0.5f);
			}
			else if (level >= 5f / 6f)
			{
				r = 1f;
			}

			if (level < 1f / 3f)
			{
				g = 3f * level;
			}
			else if (level >= 1f / 3f && level < 2f / 3f)
			{
				g = 1f;
			}
			else if (level >= 2f / 3f)
			{
				g = 1f - 3f * (level - 2f / 3f);
			}

			if (level < 1f/3f)
			{
				b = 1f;
			}
			else if (level >= 1f / 3f && level < 1f / 2f)
			{
				b = 1f - 6f * (level - 1f / 3f);
			}
			else if (level >= 1f / 2f)
			{
				b = 0f;
			}

			return new Color(r, g, b, a);
		}

		private int CellCode(int i0, int j0, int k0)
		{
			int sum = 0, code = 0;

			for (int k = k0 + 1; k >= k0; k--)
			{
				for (int j = j0 + 1; j >= j0; j--)
				{
					for (int i = i0 + 1; i >= i0; i--)
					{
						int bit = (GetValue(i, j, k) > threshold ? 1 : 0);

						code |= bit;

						if (i != i0 || j != j0 || k != k0) code <<= 1;

						sum += bit;
					}
				}
			}

			if (sum > 4)
			{
				code = (byte)~code;
			}

			return code;
		}

		private void UnitCube()
		{
			int code = CellCode(cell_i, cell_j, cell_k);
			int p = GetTriangleNum(code);

			while (p-- > 0)
			{
				int edge0 = GetTriangle(code, p, 0);
				int edge1 = GetTriangle(code, p, 1);
				int edge2 = GetTriangle(code, p, 2);

				CrossPoint(edge0, out Vector3 p0, out Vector3 n0);
				CrossPoint(edge1, out Vector3 p1, out Vector3 n1);
				CrossPoint(edge2, out Vector3 p2, out Vector3 n2);

				AddTriangleGeom(p0, p1, p2, n0, n1, n2);
				triCount++;
			}
		}

		private void InitLevel()
		{
			min = element.min;
			max = element.max;

			if (!isThresholdInitialized)
			{
				threshold = element.average + element.variance * 3f;
				isThresholdInitialized = true;
			}

			threshold = Mathf.Clamp(threshold, min, max);
			slider = (max > min) ? (threshold - min) / (max - min) : 0f;
		}

		public override void ResetUI()
		{
			if (element == null) return;

			var sliderObj = UIPanel.transform.Find("Threshold/Slider");

			if (sliderObj != null)
			{
				var sliderComp = sliderObj.GetComponent<Slider>();
				if (sliderComp != null)
				{
					sliderComp.minValue = element.min;
					sliderComp.maxValue = element.max;
					sliderComp.value    = threshold;
				}
			}
		}

		public void SetValue(float value)
		{
			threshold = Mathf.Clamp(value, min, max);
			slider = (threshold - min) / (max - min);

			if (Application.isPlaying && activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		public void Draw()
		{
			var meshFilter = GetComponent<MeshFilter>();
			if (!useGPU)
			{
				mesh.Clear();
				mesh.SetVertices(vertices);
				mesh.SetNormals(normals);
				mesh.SetColors(colors);
				mesh.SetIndices(indices, MeshTopology.Triangles, 0);
				mesh.RecalculateBounds();
				meshFilter.mesh = mesh;
			}
			else
			{
				var scale = transform.localScale;
				var v0 = Vector3.Scale(element.boundMin, scale);
				var v1 = Vector3.Scale(element.boundMax, scale);

				mesh.bounds = new UnityEngine.Bounds(v0 + (v1 - v0) / 2, (v1 - v0) * 2);
				meshFilter.sharedMesh = mesh;
			}
		}
	}
}