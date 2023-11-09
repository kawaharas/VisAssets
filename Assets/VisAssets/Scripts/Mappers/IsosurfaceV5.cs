//
// The part of Marching Cubes in this code was ported from VFIVE (isosurf.cpp).
// https://www.jamstec.go.jp/ceist/aeird/avcrg/vfive.ja.html
// The original code was written by Akira Kageyama (Kobe University) and Nobuaki Ohno (University of Hyogo).
//
// I refered an implementation of graphics buffers from the code written by Keijiro Takahashi (Unity Technologies Japan).
// https://github.com/keijiro/ComputeMarchingCubes
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets.SciVis.Structured.Isosurface
{
#if UNITY_EDITOR
	[CustomEditor(typeof(IsosurfaceV5))]
	public class IsosurfaceV5Editor : Editor
	{
		SerializedProperty slider;
		SerializedProperty threshold;
		SerializedProperty min;
		SerializedProperty max;
		SerializedProperty shadingMode;
		SerializedProperty useGPU;
		SerializedProperty triCount;

		private void OnEnable()
		{
			slider      = serializedObject.FindProperty("slider");
			threshold   = serializedObject.FindProperty("threshold");
			min         = serializedObject.FindProperty("min");
			max         = serializedObject.FindProperty("max");
			shadingMode = serializedObject.FindProperty("shadingMode");
			triCount    = serializedObject.FindProperty("triCount");
			useGPU      = serializedObject.FindProperty("useGPU");
		}

		public override void OnInspectorGUI()
		{
			var isosurface = target as IsosurfaceV5;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
//			var label = new GUIContent("Shading Mode: ");
//			EditorGUILayout.PropertyField(shadingMode, label, true);
//			GUILayout.Space(6f);
			var _threshold = EditorGUILayout.Slider("Threshold: ", threshold.floatValue, min.floatValue, max.floatValue);
			GUILayout.Space(6f);
//			var _color = EditorGUILayout.ColorField("Color: ", isosurface.color);
//			GUILayout.Space(6f);
			EditorGUILayout.LabelField("Triangles : " + (triCount.intValue).ToString());
			GUILayout.Space(6f);
			EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
			useGPU.boolValue = EditorGUILayout.ToggleLeft("Enable GPU acceleration", useGPU.boolValue);
			EditorGUI.EndDisabledGroup();
			GUILayout.Space(6f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Isosurface");
				if (isosurface != null)
				{
					if (_threshold != threshold.floatValue)
					{
						isosurface.SetValue(_threshold);
					}
//					isosurface.SetColor(_color);
				}
				EditorUtility.SetDirty(target);
			}
			serializedObject.ApplyModifiedProperties();

//			base.OnInspectorGUI();
		}
	}
#endif

	public class IsosurfaceV5 : MapperModuleTemplate
	{
		public enum SHADING_MODE
		{
			FLAT,
			SMOOTH
		};

		DataElement element;
		int []  dims;
//		float[] x, y, z;
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
		[SerializeField]
		public Color color;
		[SerializeField]
		public SHADING_MODE shadingMode;
		[SerializeField]
		public int triCount;
		int cell_i, cell_j, cell_k;  // the current position of marching cell 

		Mesh mesh;

		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		int[] indices;

		[Range(0f, 1f)]
		public float alpha = 1.0f;

		public ComputeShader shader = null;
		ComputeBuffer  tablesBuffer;  // buffer packed three tables
		ComputeBuffer  cvmBuffer;     // buffer packed coord, value and metrics
		GraphicsBuffer vertexBuffer;  // buffer for vertices (position, normal and color)
		ComputeBuffer  counterBuffer;
		ComputeBuffer  counterCheckBuffer;

#if UNITY_ANDROID
		// this variable needs to be a multiple of three.
		int maximumVertexNum = 65536 * 15; // UInt32.MaxValue = 65535;
#else
		int maximumVertexNum = 65536 * 63; // UInt32.MaxValue = 65535;
#endif

		int[]   packedTables;
		float[] cvm;
		float[] metrics;
		uint[]  counter;

		[SerializeField]
		public bool useGPU;

		public override void InitModule()
		{
			dims     = new int[3];
			mesh     = new Mesh();
			triCount = 0;

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

				var vp = new VertexAttributeDescriptor
					(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);

				var vn = new VertexAttributeDescriptor
					(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3);

				var vc = new VertexAttributeDescriptor
					(VertexAttribute.Color,    VertexAttributeFormat.Float32, 4);

				mesh.SetVertexBufferParams(maximumVertexNum, vp, vn, vc);
				mesh.SetIndexBufferParams(maximumVertexNum, IndexFormat.UInt32);

				mesh.SetSubMesh(0, new SubMeshDescriptor(0, maximumVertexNum),
					MeshUpdateFlags.DontRecalculateBounds);

				vertexBuffer = mesh.GetVertexBuffer(0);
				indices = new int[maximumVertexNum];
				for (int i = 0; i < maximumVertexNum; i++)
				{
					indices[i] = i;
				}
				mesh.SetIndices(indices, MeshTopology.Triangles, 0);
			}
		}

		public override int BodyFunc()
		{
//			Debug.Log(" Exec : Isosurface module");
			if (pdf.dataLoaded)
			{
				Draw();
			}
			else
			{
				Debug.Log(" Error: Isosurface should be located under extract scalar");
			}
			return 1;
		}

		public override void SetParameters()
		{
			if (!pdf.dataLoaded) return;

			Calc();
		}

		public override void ReSetParameters()
		{
			if (!pdf.dataLoaded) return;

			element = pdf.elements[0];
			dims = element.dims;
/*
			if ((element.fieldType == DataElement.FieldType.UNIFORM) ||
				(element.fieldType == DataElement.FieldType.RECTILINEAR))
			{
				x = element.coords[0];
				y = element.coords[1];
				z = element.coords[2];
			}
			else if (element.fieldType == DataElement.FieldType.IRREGULAR)
			{
				coords = element.coords[3];
			}
			else
			{
				// not implemented yet
			}
*/
			coords = element.coords[3]; // temporary, all fieldtype are using 3-d coords data
			values = element.values;
			metrics = new float[dims[0] * dims[1] * dims[2] * 9];

			if (!useGPU)
			{
				GenCoordPrep();
			}
			else
			{
				GenCoordPrepGPU();
			}

			InitLevel();

			Calc();
		}

		private void OnDestroy()
		{
			DisposeBuffers();
		}

		private void DisposeBuffers()
		{
			if (tablesBuffer != null)
			{
				tablesBuffer.Dispose();
			}
			if (counterBuffer != null)
			{
				counterBuffer.Dispose();
			}
			if (counterCheckBuffer != null)
			{
				counterCheckBuffer.Dispose();
			}
			if (vertexBuffer != null)
			{
				vertexBuffer.Dispose();
			}
			if (cvmBuffer != null)
			{
				cvmBuffer.Dispose();
			}
		}
		private int GetTriangleNum(int i)
		{
			return packedTables[i];
		}

		private int GetEdgeEndVert(int j, int i)
		{
			return packedTables[256 + j * 2 + i];
		}

		private int GetTriangle(int k, int j, int i)
		{
			return packedTables[256 + (12 * 2) + 3 * 4 * k + 3 * j + i];
		}

		private int GetIndex(int i, int j, int k)
		{
			return (dims[1] * k + j) * dims[0] + i;
		}

		private float GetCoord(int i, int j, int k, int axis)
		{
			// axis : X = 0, Y = 1, Z = 2
			return coords[GetIndex(i, j, k) * 3 + axis];
		}

		private float GetValue(int i, int j, int k)
		{
			return values[GetIndex(i, j, k)];
		}

		void OnValidate()
		{
//			if (pdf == null) return;

//			if (!pdf.dataLoaded) return;

			if (!IsDataLoadedToParent()) return;

			threshold = min + (max - min) * slider;
			Calc();

			activation.SetParameterChanged(1);
		}

		public void SetValue(float value)
		{
			threshold = Mathf.Clamp(value, min, max);
			slider = (threshold - min) / (max - min);

			activation.SetParameterChanged(1);
		}

		public void SetColor(Color _color)
		{
			color = _color;

			activation.SetParameterChanged(1);
		}

		public override void ResetUI()
		{
			var slider = UIPanel.transform.Find("Threshold/Slider").GetComponent<Slider>();
//			slider.value    = element.average + element.variance * 3f;
			slider.value    = element.min;
			slider.minValue = element.min;
			slider.maxValue = element.max;
		}

		private void GenCoordPrep()
		{
			float x1, x2, x3;   // dx/d(coord 1), etc.
			float y1, y2, y3;   // dy/d(coord 1), etc.
			float z1, z2, z3;   // dz/d(coord 1), etc.
			float jac;          // jacobian

			for (int k = 0; k < dims[2]; k++)
			{
				for (int j = 0; j < dims[1]; j++)
				{
					for (int i = 0; i < dims[0]; i++)
					{
						if (i == 0)
						{
							x1 =  GetCoord(i + 1, j, k, 0) - GetCoord(i,     j, k, 0);
							y1 =  GetCoord(i + 1, j, k, 1) - GetCoord(i,     j, k, 1);
							z1 =  GetCoord(i + 1, j, k, 2) - GetCoord(i,     j, k, 2);
						}
						else if (i == dims[0] - 1)
						{
							x1 =  GetCoord(i,     j, k, 0) - GetCoord(i - 1, j, k, 0);
							y1 =  GetCoord(i,     j, k, 1) - GetCoord(i - 1, j, k, 1);
							z1 =  GetCoord(i,     j, k, 2) - GetCoord(i - 1, j, k, 2);
						}
						else
						{
							x1 = (GetCoord(i + 1, j, k, 0) - GetCoord(i - 1, j, k, 0)) / 2f;
							y1 = (GetCoord(i + 1, j, k, 1) - GetCoord(i - 1, j, k, 1)) / 2f;
							z1 = (GetCoord(i + 1, j, k, 2) - GetCoord(i - 1, j, k, 2)) / 2f;
						}

						if (j == 0)
						{
							x2 =  GetCoord(i, j + 1, k, 0) - GetCoord(i, j,     k, 0);
							y2 =  GetCoord(i, j + 1, k, 1) - GetCoord(i, j,     k, 1);
							z2 =  GetCoord(i, j + 1, k, 2) - GetCoord(i, j,     k, 2);
						}
						else if (j == dims[1] - 1)
						{
							x2 =  GetCoord(i, j,     k, 0) - GetCoord(i, j - 1, k, 0);
							y2 =  GetCoord(i, j,     k, 1) - GetCoord(i, j - 1, k, 1);
							z2 =  GetCoord(i, j,     k, 2) - GetCoord(i, j - 1, k, 2);
						}
						else
						{
							x2 = (GetCoord(i, j + 1, k, 0) - GetCoord(i, j - 1, k, 0)) / 2f;
							y2 = (GetCoord(i, j + 1, k, 1) - GetCoord(i, j - 1, k, 1)) / 2f;
							z2 = (GetCoord(i, j + 1, k, 2) - GetCoord(i, j - 1, k, 2)) / 2f;
						}

						if (k == 0)
						{
							x3 =  GetCoord(i, j, k + 1, 0) - GetCoord(i, j, k,     0);
							y3 =  GetCoord(i, j, k + 1, 1) - GetCoord(i, j, k,     1);
							z3 =  GetCoord(i, j, k + 1, 2) - GetCoord(i, j, k,     2);
						}
						else if (k == dims[2] - 1)
						{
							x3 =  GetCoord(i, j, k,     0) - GetCoord(i, j, k - 1, 0);
							y3 =  GetCoord(i, j, k,     1) - GetCoord(i, j, k - 1, 1);
							z3 =  GetCoord(i, j, k,     2) - GetCoord(i, j, k - 1, 2);
						}
						else
						{
							x3 = (GetCoord(i, j, k + 1, 0) - GetCoord(i, j, k - 1, 0)) / 2f;
							y3 = (GetCoord(i, j, k + 1, 1) - GetCoord(i, j, k - 1, 1)) / 2f;
							z3 = (GetCoord(i, j, k + 1, 2) - GetCoord(i, j, k - 1, 2)) / 2f;
						}

						jac = x1 * (y2 * z3 - y3 * z2)
							- x2 * (y1 * z3 - y3 * z1)
							+ x3 * (y1 * z2 - y2 * z1);

						int idx = GetIndex(i, j, k) * 9;
						if (jac == 0)
						{
							for (int n = 0; n < 9; n++)
							{
								metrics[idx + n] = 0;
							}
						}
						else
						{
							metrics[idx + 0] = (y2 * z3 - y3 * z2) / jac;
							metrics[idx + 1] = (y3 * z1 - y1 * z3) / jac;
							metrics[idx + 2] = (y1 * z2 - y2 * z1) / jac;
							metrics[idx + 3] = (x3 * z2 - x2 * z3) / jac;
							metrics[idx + 4] = (x1 * z3 - x3 * z1) / jac;
							metrics[idx + 5] = (x2 * z1 - x1 * z2) / jac;
							metrics[idx + 6] = (x2 * y3 - x3 * y2) / jac;
							metrics[idx + 7] = (x3 * y1 - x1 * y3) / jac;
							metrics[idx + 8] = (x1 * y2 - x2 * y1) / jac;
						}
					}
				}
			}
		}

		private void GenCoordPrepGPU()
		{
			if (cvmBuffer != null)
			{
				cvmBuffer.Dispose();
			}

			// create a buffer which was packed coords, values and metrics
			cvmBuffer = new ComputeBuffer(dims[0] * dims[1] * dims[2] * 13, sizeof(float));
			cvm = new float[dims[0] * dims[1] * dims[2] * 13];
			for (int i = 0; i < dims[0] * dims[1] * dims[2]; i++)
			{
				cvm[i * 13 + 0] = coords[i * 3 + 0];
				cvm[i * 13 + 1] = coords[i * 3 + 1];
				cvm[i * 13 + 2] = coords[i * 3 + 2];
				cvm[i * 13 + 3] = values[i];
				for (int n = 4; n < 13; n++)
				{
					cvm[i * 13 + n] = 0;
				}
			}

			int kernel = shader.FindKernel("GenCoordPrep");

			shader.SetInts("dims", dims);
			shader.SetFloat("_min", element.min);
			shader.SetFloat("_max", element.max);
			shader.SetInt("maximumVertexNum", maximumVertexNum);

			cvmBuffer.SetData(cvm);
			shader.SetBuffer(kernel, "cvm",     cvmBuffer);

			uint sx, sy, sz;
			shader.GetKernelThreadGroupSizes(kernel, out sx, out sy, out sz);
			int _x = (dims[0] + (int)sx - 1) / (int)sx;
			int _y = (dims[1] + (int)sy - 1) / (int)sy;
			int _z = (dims[2] + (int)sz - 1) / (int)sz;
			shader.Dispatch(kernel, _x, _y, _z);
		}

		private void VertIntPosition(int vert, ref int i, ref int j, ref int k)
		{
			int shift_i = vert % 2 == 0 ? 0 : 1; // (0,2,4,6) --> 0, (1,3,5,7) --> 1
			int shift_j = vert % 4 < 2 ? 0 : 1;  // (0,1,4,5) --> 0, (2,3,6,7) --> 1
			int shift_k = vert < 4 ? 0 : 1;      // (0,1,2,3) --> 0, (4,5,6,7) --> 1

			i = cell_i + shift_i;
			j = cell_j + shift_j;
			k = cell_k + shift_k;
		}

		private void Grad(int i, int j, int k, ref float vx, ref float vy, ref float vz)
		{
			float dfd1, dfd2, dfd3, gx, gy, gz, gg;

			if (i == 0)
			{
				dfd1 =  GetValue(i + 1, j, k) - GetValue(i,     j, k);
			}
			else if (i == dims[0] - 1)
			{
				dfd1 =  GetValue(i,     j, k) - GetValue(i - 1, j, k);
			}
			else
			{
				dfd1 = (GetValue(i + 1, j, k) - GetValue(i - 1, j, k)) / 2f;
			}

			if (j == 0)
			{
				dfd2 =  GetValue(i, j + 1, k) - GetValue(i, j,     k);
			}
			else if (j == dims[1] - 1)
			{
				dfd2 =  GetValue(i, j,     k) - GetValue(i, j - 1, k);
			}
			else
			{
				dfd2 = (GetValue(i, j + 1, k) - GetValue(i, j - 1, k)) / 2f;
			}

			if (k == 0)
			{
				dfd3 =  GetValue(i, j, k + 1) - GetValue(i, j, k    );
			}
			else if (k == dims[2] - 1)
			{
				dfd3 =  GetValue(i, j, k    ) - GetValue(i, j, k - 1);
			}
			else
			{
				dfd3 = (GetValue(i, j, k + 1) - GetValue(i, j, k - 1)) / 2f;
			}

			int idx = GetIndex(i, j, k) * 9;
			gx = metrics[idx + 0] * dfd1 + metrics[idx + 1] * dfd2 + metrics[idx + 2] * dfd3;
			gy = metrics[idx + 3] * dfd1 + metrics[idx + 4] * dfd2 + metrics[idx + 5] * dfd3;
			gz = metrics[idx + 6] * dfd1 + metrics[idx + 7] * dfd2 + metrics[idx + 8] * dfd3;
			gg = Mathf.Sqrt(gx * gx + gy * gy + gz * gz); // gg cannot be 0 since

			vx = -gx / gg;      // 0-isosurface is near here
			vy = -gy / gg;
			vz = -gz / gg;
		}

		private void CrossPoint(int edge, ref float[] vert, ref float[] norm)
		{
			int i0, j0, k0, i1, j1, k1;
			float weight0, weight1;
			float x0, y0, z0, x1, y1, z1;
			float nx0, ny0, nz0, nx1, ny1, nz1;

			i0 = j0 = k0 = i1 = j1 = k1 = 0;
			nx0 = ny0 = nz0 = nx1 = ny1 = nz1 = 0f;

			VertIntPosition(GetEdgeEndVert(edge, 0), ref i0, ref j0, ref k0);
			VertIntPosition(GetEdgeEndVert(edge, 1), ref i1, ref j1, ref k1);

			if (GetValue(i1, j1, k1) != GetValue(i0, j0, k0))
			{
				weight0 = (GetValue(i1, j1, k1) - threshold)
					/ (GetValue(i1, j1, k1) - GetValue(i0, j0, k0));
			}
			else
			{
				weight0 = 0;
			}
			weight1 = 1f - weight0;

			x0 = GetCoord(i0, j0, k0, 0);
			y0 = GetCoord(i0, j0, k0, 1);
			z0 = GetCoord(i0, j0, k0, 2);
			x1 = GetCoord(i1, j1, k1, 0);
			y1 = GetCoord(i1, j1, k1, 1);
			z1 = GetCoord(i1, j1, k1, 2);

			Grad(i0, j0, k0, ref nx0, ref ny0, ref nz0);    // you get negative gradient
			Grad(i1, j1, k1, ref nx1, ref ny1, ref nz1);    // vector of the fisos field

			vert[0] = weight0 *  x0 + weight1 *  x1;  // linear interpolation
			vert[1] = weight0 *  y0 + weight1 *  y1;
			vert[2] = weight0 *  z0 + weight1 *  z1;
			norm[0] = weight0 * nx0 + weight1 * nx1;
			norm[1] = weight0 * ny0 + weight1 * ny1;
			norm[2] = weight0 * nz0 + weight1 * nz1;
		}

		private void AddTriangleGeom(
			float[] vert0, float[] vert1, float[] vert2,
			float[] norm0, float[] norm1, float[] norm2)
		{
			vertices.Add(new Vector3(vert0[0], vert0[1], vert0[2]));
			vertices.Add(new Vector3(vert1[0], vert1[1], vert1[2]));
			vertices.Add(new Vector3(vert2[0], vert2[1], vert2[2]));
			 normals.Add(new Vector3(norm0[0], norm0[1], norm0[2]));
			 normals.Add(new Vector3(norm1[0], norm1[1], norm1[2]));
			 normals.Add(new Vector3(norm2[0], norm2[1], norm2[2]));

			var color = GetColor();
			for (int i = 0; i < 3; i++)
			{
				colors.Add(color);
			}
		}

		private Color GetColor()
		{
			float level = (element.max - threshold) / (element.max - element.min);
			float r, g, b, a;
			r = g = b = 0f;
			a = 1f;

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
				g = 3f * (level);
			}
			else if (level >= 1f / 3f && level < 2f / 3f)
			{
				g = 1f;
			}
			else if (level >= 2f / 3f)
			{
				g = 1f - 3f * (level - 2f / 3f);
			}

			if (level < 1f / 3f)
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

//		private byte CellCode(int i0, int j0, int k0)
		private int CellCode(int i0, int j0, int k0)
		{
			int sum = 0;
//			byte code = 0;
			int code = 0;

			for (int k = k0 + 1; k >= k0; k--)
			{
				for (int j = j0 + 1; j >= j0; j--)
				{
					for (int i = i0 + 1; i >= i0; i--)
					{
						int bit = (GetValue(i, j, k) > threshold ? 1 : 0);
//						code |= Convert.ToByte(bit);
						code |= bit;
						if (i != i0 || j != j0 || k != k0)
						{
							code <<= 1;
						}
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
			int edge0, edge1, edge2;
			float[] posit0 = new float[3];
			float[] posit1 = new float[3];
			float[] posit2 = new float[3];
			float[] norml0 = new float[3];
			float[] norml1 = new float[3];
			float[] norml2 = new float[3];

//			int code = Convert.ToInt16(CellCode(cell_i, cell_j, cell_k));
			int code = CellCode(cell_i, cell_j, cell_k);
			int p = GetTriangleNum(code);

			while (p-- > 0)
			{
				edge0 = GetTriangle(code, p, 0);
				edge1 = GetTriangle(code, p, 1);
				edge2 = GetTriangle(code, p, 2);
				CrossPoint(edge0, ref posit0, ref norml0);
				CrossPoint(edge1, ref posit1, ref norml1);
				CrossPoint(edge2, ref posit2, ref norml2);
				AddTriangleGeom(posit0, posit1, posit2, norml0, norml1, norml2);
				triCount++;
			}
		}

		private void InitLevel()
		{
			threshold = element.average + element.variance * 3f;
//			threshold = (element.max + element.min) / 2.0; // for time series data
			min = element.min;
			max = element.max;
			slider = (max - threshold) / (max - min);
		}

		public void Calc()
		{
			if (!useGPU)
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
			}
			else
			{
				int kernel = shader.FindKernel("Calc");

				shader.SetFloat("threshold", threshold);

				counterBuffer.SetCounterValue(0);
				shader.SetBuffer(kernel, "counter",  counterBuffer);

				tablesBuffer.SetData(packedTables);
				shader.SetBuffer(kernel, "tables",   tablesBuffer);
				shader.SetBuffer(kernel, "cvm",      cvmBuffer);
				shader.SetBuffer(kernel, "vertices", vertexBuffer);

				uint sx, sy, sz;
				shader.GetKernelThreadGroupSizes(kernel, out sx, out sy, out sz);
				int _x = (dims[0] + (int)sx - 1) / (int)sx;
				int _y = (dims[1] + (int)sy - 1) / (int)sy;
				int _z = (dims[2] + (int)sz - 1) / (int)sz;
				shader.Dispatch(kernel, _x, _y, _z);

				counter = new uint[1] { 0 };
				counterCheckBuffer.SetData(counter);
				shader.SetBuffer(kernel, "counterCheck",  counterCheckBuffer);
				ComputeBuffer.CopyCount(counterBuffer, counterCheckBuffer, 0);
				counterCheckBuffer.GetData(counter);
				triCount = (int)counter[0];

				ClearGPUBuffers();
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
				// Set Bounding Box for Rendering
				var scale = transform.localScale;
				var v0 = new Vector3(
					element.boundMin[0] * scale.x,
					element.boundMin[1] * scale.y,
					element.boundMin[2] * scale.z);
				var v1 = new Vector3(
					element.boundMax[0] * scale.x,
					element.boundMax[1] * scale.y,
					element.boundMax[2] * scale.z);
				mesh.bounds = new UnityEngine.Bounds(v0, v1);

				meshFilter.sharedMesh = mesh;
			}
		}

		public void ClearGPUBuffers()
		{
			int kernel = shader.FindKernel("ClearBuffers");

			shader.SetBuffer(kernel, "counter",  counterBuffer);
			shader.SetBuffer(kernel, "vertices", vertexBuffer);
			shader.Dispatch(kernel, 1, 1, 1);
		}
	}
}
