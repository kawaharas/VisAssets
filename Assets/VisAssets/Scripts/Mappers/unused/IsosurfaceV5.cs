// ported from VFIVE

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(IsosurfaceV5))]
	public class IsosurfaceV5Editor : Editor
	{
		SerializedProperty slider;
		SerializedProperty threshold;
		SerializedProperty min;
		SerializedProperty max;
		SerializedProperty shading;

		private void OnEnable()
		{
			slider = serializedObject.FindProperty("slider");
			threshold = serializedObject.FindProperty("threshold");
			min = serializedObject.FindProperty("min");
			max = serializedObject.FindProperty("max");
			shading = serializedObject.FindProperty("shading");
		}

		public override void OnInspectorGUI()
		{
//			base.DrawDefaultInspector();

			var isosurface = target as IsosurfaceV5;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			var label = new GUIContent("Shading: ");
			EditorGUILayout.PropertyField(shading, label, true);
			GUILayout.Space(6f);
			var _threshold = EditorGUILayout.Slider("Threshold: ", threshold.floatValue, min.floatValue, max.floatValue);
			GUILayout.Space(6f);
			var _color = EditorGUILayout.ColorField("Color: ", isosurface.color);
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Isosurface");
				isosurface.SetValue(_threshold);
				isosurface.SetColor(_color);
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

//	[System.Serializable]
//	public class IntParam : Parameter<int> { }

	public class IsosurfaceV5 : MapperModuleTemplate
	{
		public enum SHADING_MODE
		{
			FLAT,
			SMOOTH
		};

		DataElement element;

		int I1, I2, I3;
		float[] v, x, y, z;

		float[] c1x, c1y, c1z;
		float[] c2x, c2y, c2z;
		float[] c3x, c3y, c3z;

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
		public SHADING_MODE shading = SHADING_MODE.FLAT;

//		[SerializeField]
//		public IntParam intParam;
//		public Parameter<int> intParam = new Parameter<int>();

		int triCount = 0;
		int cell_i, cell_j, cell_k;  // the current position of marching cell 
		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		int[]         indicies;
		Material      material;

		public override void InitModule()
		{
			vertices = new List<Vector3>();
			normals  = new List<Vector3>();
			colors   = new List<Color>();
			color    = new Color(0, 1f, 0, 1f);
//			material = new Material(Shader.Find("Custom/SurfaceShader"));
			material = new Material(Shader.Find("Custom/SimplePhong"));

			threshold = 0;
			slider = 0;
			min = 0;
			max = 1f;

			var transform = GetComponent<Transform>();
			transform.hideFlags = HideFlags.HideInInspector;
			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				meshRenderer.material = material;
				meshRenderer.hideFlags = HideFlags.HideInInspector;
			}
		}

		public override int BodyFunc()
		{
			Debug.Log(" Exec : IsosurfaceV5 module");
			Calc();

			return 1;
		}

		public override void ReSetParameters()
		{
			element = pdf.elements[0];

			shading = SHADING_MODE.FLAT;
			I1 = element.dims[0];
			I2 = element.dims[1];
			I3 = element.dims[2];
			x = element.coords[0];
			y = element.coords[1];
			z = element.coords[2];
			v = element.values;
			c1x = new float[I1];
			c1y = new float[I2];
			c1z = new float[I3];
			c2x = new float[I1];
			c2y = new float[I2];
			c2z = new float[I3];
			c3x = new float[I1];
			c3y = new float[I2];
			c3z = new float[I3];
			GenCoordPrep(ref c1x, ref c1y, ref c1z, ref c2x, ref c2y, ref c2z, ref c3x, ref c3y, ref c3z);

			InitLevel();
		}

		public override void SetParameters()
		{
		}

		public override void GetParameters()
		{
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

			threshold = min + (max - min) * slider;
			Calc();

			activation.SetParameterChanged(1);
		}

		public void SetValue(float value)
		{
			threshold = Mathf.Clamp(value, element.min, element.max);
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
			slider.minValue = element.min;
			slider.maxValue = element.max;
			slider.value = element.average + element.variance * 3f;
//			slider.value = element.min + (element.max - element.min) / 2.0f;
		}

		private void VertIntPosition(int vert, ref int i, ref int j, ref int k)
		{
			int shift_i = vert % 2 == 0 ? 0 : 1;    // (0,2,4,6) --> 0, (1,3,5,7) --> 1
			int shift_j = vert % 4 < 2 ? 0 : 1;     // (0,1,4,5) --> 0, (2,3,6,7) --> 1
			int shift_k = vert < 4 ? 0 : 1;         // (0,1,2,3) --> 0, (4,5,6,7) --> 1
													//			int[] ret = new int[3];
			i = cell_i + shift_i;
			j = cell_j + shift_j;
			k = cell_k + shift_k;
		}

		private void GenCoordPrep(
			ref float[] _c1x,  // the output (metrics)
			ref float[] _c1y,  // c1x={d(coord 1)/dx} 
			ref float[] _c1z,  //            /Jacobian
			ref float[] _c2x,
			ref float[] _c2y,
			ref float[] _c2z,
			ref float[] _c3x,
			ref float[] _c3y,
			ref float[] _c3z)
		{
			float x1, x2, x3;   // dx/d(coord 1), etc.
			float y1, y2, y3;   // dy/d(coord 1), etc.
			float z1, z2, z3;   // dz/d(coord 1), etc.
			float jac;          // jacobian

			for (int k = 0; k < I3; k++)
			{
				for (int j = 0; j < I2; j++)
				{
					for (int i = 0; i < I1; i++)
					{
//						if ((i > I1 - 3) && (j > I2 - 3) && (k > I3 - 3))
//						{
//							Debug.Log("(i, j, k) = " + i + ", " + j + ", " + k);
//						}

						y1 = z1 = x2 = z2 = x3 = y3 = 0f;
						if (i == 0)
						{
							x1 = x[i + 1] - x[i];
//							y1 = y[j] - y[j];
//							z1 = z[k] - z[k];
						}
						else if (i == I1 - 1)
						{
							x1 = x[i] - x[i - 1];
//							y1 = y[j] - y[j];
//							z1 = z[k] - z[k];
						}
						else
						{
//							x1 = x[i + 1] - x[i - 1] / 2f;
//							y1 = y[j] - y[j] / 2f;
//							z1 = z[k] - z[k] / 2f;
							x1 = x[i + 1] - x[i - 1] / 2f;
							y1 = y[j] / 2f;
							z1 = z[k] / 2f;
						}

						if (j == 0)
						{
//							x2 = x[i] - x[i];
							y2 = y[j + 1] - y[j];
//							z2 = z[k] - z[k];
						}
						else if (j == I2 - 1)
						{
//							x2 = x[i] - x[i];
							y2 = y[j] - y[j - 1];
//							z2 = z[k] - z[k];
						}
						else
						{
//							x2 = x[i] - x[i] / 2f;
//							y2 = y[j + 1] - y[j - 1] / 2f;
//							z2 = z[k] - z[k] / 2f;
							x2 = x[i] / 2f;
							y2 = y[j + 1] - y[j - 1] / 2f;
							z2 = z[k] / 2f;
						}

						if (k == 0)
						{
//							x3 = x[i] - x[i];
//							y3 = y[j] - y[j];
							z3 = z[k + 1] - z[k];
						}
						else if (k == I3 - 1)
						{
//							x3 = x[i] - x[i];
//							y3 = y[j] - y[j];
							z3 = z[k] - z[k - 1];
						}
						else
						{
//							x3 = x[i] - x[i] / 2f;
//							y3 = y[j] - y[j] / 2f;
//							z3 = z[k + 1] - z[k - 1] / 2f;
							x3 = x[i] / 2f;
							y3 = y[j] / 2f;
							z3 = z[k + 1] - z[k - 1] / 2f;
						}

						jac = x1 * (y2 * z3 - y3 * z2)
							- x2 * (y1 * z3 - y3 * z1)
							+ x3 * (y1 * z2 - y2 * z1);

						_c1x[i] = (y2 * z3 - y3 * z2) / jac;
						_c2x[i] = (y3 * z1 - y1 * z3) / jac;
						_c3x[i] = (y1 * z2 - y2 * z1) / jac;
						_c1y[j] = (x3 * z2 - x2 * z3) / jac;
						_c2y[j] = (x1 * z3 - x3 * z1) / jac;
						_c3y[j] = (x2 * z1 - x1 * z2) / jac;
						_c1z[k] = (x2 * y3 - x3 * y2) / jac;
						_c2z[k] = (x3 * y1 - x1 * y3) / jac;
						_c3z[k] = (x1 * y2 - x2 * y1) / jac;
					}
				}
			}
		}

		private byte CellCode(int i0, int j0, int k0)
		{
			int sum = 0;
			byte code = 0;

			for (int k = k0 + 1; k >= k0; k--)
			{
				for (int j = j0 + 1; j >= j0; j--)
				{
					for (int i = i0 + 1; i >= i0; i--)
					{
						int bit = (v[k * I2 * I1 + j * I1 + i] > threshold ? 1 : 0);
						code |= Convert.ToByte(bit);
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
				// Should'v counted the number of vertices with negative data.
				code = (byte)~code;
			}

			return code;
		}

		private void Grad(
			int i, int j, int k,
			ref float vx, ref float vy, ref float vz)
		{
			float dfd1, dfd2, dfd3, gx, gy, gz, gg;

			if (i == 0)
			{
				dfd1 = v[k * I2 * I1 + j * I1 + i + 1] - v[k * I2 * I1 + j * I1 + i];
			}
			else if (i == I1 - 1)
			{
				dfd1 = v[k * I2 * I1 + j * I1 + i] - v[k * I2 * I1 + j * I1 + i - 1];
			}
			else
			{
				dfd1 = v[k * I2 * I1 + j * I1 + i + 1] - v[k * I2 * I1 + j * I1 + i - i] / 2f;
			}

			if (j == 0)
			{
				dfd2 = v[k * I2 * I1 + (j + 1) * I1 + i] - v[k * I2 * I1 + j * I1 + i];
			}
			else if (j == I2 - 1)
			{
				dfd2 = v[k * I2 * I1 + j * I1 + i] - v[k * I2 * I1 + (j - 1) * I1 + i];
			}
			else
			{
				dfd2 = v[k * I2 * I1 + (j + 1) * I1 + i] - v[k * I2 * I1 + (j - 1) * I1 + i] / 2f;
			}

			if (k == 0)
			{
				dfd3 = v[(k + 1) * I2 * I1 + j * I1 + i] - v[k * I2 * I1 + j * I1 + i];
			}
			else if (k == I3 - 1)
			{
				dfd3 = v[k * I2 * I1 + j * I1 + i] - v[(k - 1) * I2 * I1 + j * I1 + i];
			}
			else
			{
				dfd3 = v[(k + 1) * I2 * I1 + j * I1 + i] - v[(k - 1) * I2 * I1 + j * I1 + i] / 2f;
			}

			gx = c1x[i] * dfd1 + c2x[i] * dfd2 + c3x[i] * dfd3;
			gy = c1y[j] * dfd1 + c2y[j] * dfd2 + c3y[j] * dfd3;
			gz = c1z[k] * dfd1 + c2z[k] * dfd2 + c3z[k] * dfd3;
			gg = Mathf.Sqrt(gx * gx + gy * gy + gz * gz); // gg cannot be 0 since
			vx = -gx / gg;      // 0-isosurface is near here
			vy = -gy / gg;
			vz = -gz / gg;
		}

		private void CrossPoint(int edge, ref float[] vert, ref float[] norm)
		{
			int i0, j0, k0, i1, j1, k1;
			i0 = j0 = k0 = i1 = j1 = k1 = 0;
			float weight0, weight1;
			float x0, y0, z0, x1, y1, z1;
			float nx0, ny0, nz0, nx1, ny1, nz1;
			nx0 = ny0 = nz0 = nx1 = ny1 = nz1 = 0f;

			VertIntPosition(EdgeEndVert[edge, 0], ref i0, ref j0, ref k0);
			VertIntPosition(EdgeEndVert[edge, 1], ref i1, ref j1, ref k1);
			weight0 = (v[k1 * I2 * I1 + j1 * I1 + i1] - threshold)
				/ (v[k1 * I2 * I1 + j1 * I1 + i1] - v[k0 * I2 * I1 + j0 * I1 + i0]);
			weight1 = 1f - weight0;

			x0 = x[i0];
			y0 = y[j0];
			z0 = z[k0];
			x1 = x[i1];
			y1 = y[j1];
			z1 = z[k1];

			Grad(i0, j0, k0, ref nx0, ref ny0, ref nz0);    // you get negative gradient
			Grad(i1, j1, k1, ref nx1, ref ny1, ref nz1);    // vector of the fisos field

			vert[0] = weight0 * x0 + weight1 * x1;  // linear interpolation
			vert[1] = weight0 * y0 + weight1 * y1;
			vert[2] = weight0 * z0 + weight1 * z1;
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
			normals.Add(new Vector3(-norm0[0], -norm0[1], -norm0[2]));
			normals.Add(new Vector3(-norm1[0], -norm1[1], -norm1[2]));
			normals.Add(new Vector3(-norm2[0], -norm2[1], -norm2[2]));
//			var color = GetColor();
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

		private void UnitCube()
		{
			int edge0, edge1, edge2;
			int code;
			float[] posit0 = new float[3];
			float[] posit1 = new float[3];
			float[] posit2 = new float[3];
			float[] norml0 = new float[3];
			float[] norml1 = new float[3];
			float[] norml2 = new float[3];

			code = Convert.ToInt16(CellCode(cell_i, cell_j, cell_k));
			int p = TriangleNum[code];

			while (p-- > 0)
			{
				edge0 = Triangle[code, p, 0];
				edge1 = Triangle[code, p, 1];
				edge2 = Triangle[code, p, 2];
				CrossPoint(edge0, ref posit0, ref norml0);
				CrossPoint(edge1, ref posit1, ref norml1);
				CrossPoint(edge2, ref posit2, ref norml2);
				AddTriangleGeom(posit0, posit1, posit2, norml0, norml1, norml2);
				triCount++;
			}
		}

		private void InitLevel()
		{
//			threshold = element.average + element.variance * 3f;
//			threshold = (element.max + element.min) / 2.0; // for time series data
			min = element.min;
			max = element.max;
			threshold = min;
			slider = (threshold - min) / (max - min);
		}

		public void Calc()
		{
			vertices.Clear();
			normals.Clear();
			colors.Clear();
			triCount = 0;
			for (cell_k = 0; cell_k < I3 - 1; cell_k++)
			{
				for (cell_j = 0; cell_j < I2 - 1; cell_j++)
				{
					for (cell_i = 0; cell_i < I1 - 1; cell_i++)
					{
						UnitCube();
					}
				}
			}

			indicies = new int[triCount * 3];
			for (int i = 0; i < triCount * 3; i++)
			{
				indicies[i] = i;
			}

			var mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetColors(colors);
			mesh.triangles = indicies;
//			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();

			var meshFilter = GetComponent<MeshFilter>();
			meshFilter.mesh = mesh;
		}

		int[] TriangleNum = new int[256] {
			0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 2,
			1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4,-8,
			1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4,-8,
			2, 3, 3, 2, 3, 4, 4,-8, 3, 4, 4,-8, 4,-8,-8,-8,
			1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4,-8,
			2, 3, 3, 4, 3, 2, 4,-8, 3, 4, 4,-8, 4,-8,-8,-8,
			2, 3, 3, 4, 3, 4, 4,-8, 3, 4, 4,-8, 4,-8,-8,-8,
			3, 4, 4,-8, 4,-8,-8,-8, 4,-8,-8,-8,-8,-8,-8,-8,
			1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4,-8,
			2, 3, 3, 4, 3, 4, 4,-8, 3, 4, 4,-8, 4,-8,-8,-8,
			2, 3, 3, 4, 3, 4, 4,-8, 3, 4, 2,-8, 4,-8,-8,-8,
			3, 4, 4,-8, 4,-8,-8,-8, 4,-8,-8,-8,-8,-8,-8,-8,
			2, 3, 3, 4, 3, 4, 4,-8, 3, 4, 4,-8, 2,-8,-8,-8,
			3, 4, 4,-8, 4,-8,-8,-8, 4,-8,-8,-8,-8,-8,-8,-8,
			3, 4, 4,-8, 4,-8,-8,-8, 4,-8,-8,-8,-8,-8,-8,-8,
			2,-8,-8,-8,-8,-8,-8,-8,-8,-8,-8,-8,-8,-8,-8,-8,
		};

		int[,] EdgeEndVert = new int[12, 2] {
			{0, 1}, /*  0 */
			{0, 2}, /*  1 */
			{1, 3}, /*  2 */
			{2, 3}, /*  3 */
			{0, 4}, /*  4 */
			{1, 5}, /*  5 */
			{2, 6}, /*  6 */
			{3, 7}, /*  7 */
			{4, 5}, /*  8 */
			{4, 6}, /*  9 */
			{5, 7}, /* 10 */
			{6, 7}  /* 11 */
		};

		static int[,,] Triangle = new int[256, 4, 3] {
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  1,  4 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  5,  2 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  5,  1 }, {  5,  1,  2 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  3,  6 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  3,  4 }, {  3,  4,  6 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  3,  6 }, {  0,  2,  5 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  6,  2,  5 }, {  6,  3,  2 }, {  6,  5,  4 }, { 15, 15, 15 } },
			{ {  3,  2,  7 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  1,  4 }, {  2,  3,  7 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  3,  0,  7 }, {  0,  7,  5 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  3,  7 }, {  4,  1,  3 }, {  4,  7,  5 }, { 15, 15, 15 } },
			{ {  1,  2,  6 }, {  2,  6,  7 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7,  0,  4 }, {  7,  2,  0 }, {  7,  4,  6 }, { 15, 15, 15 } },
			{ {  5,  1,  6 }, {  5,  0,  1 }, {  5,  6,  7 }, { 15, 15, 15 } },
			{ {  4,  6,  7 }, {  4,  5,  7 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8,  4,  9 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8,  0,  9 }, {  0,  9,  1 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8,  4,  9 }, {  5,  0,  2 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  2,  8,  9 }, {  2,  5,  8 }, {  2,  9,  1 }, { 15, 15, 15 } },
			{ {  6,  1,  3 }, {  9,  4,  8 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8,  6,  3 }, {  8,  9,  6 }, {  8,  3,  0 }, { 15, 15, 15 } },
			{ {  4,  9,  8 }, {  0,  5,  2 }, {  1,  6,  3 }, { 15, 15, 15 } },
			{ {  3,  2,  5 }, {  3,  6,  5 }, {  6,  5,  8 }, {  6,  9,  8 } },
			{ {  2,  7,  3 }, {  8,  4,  9 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  2,  7,  3 }, {  8,  0,  9 }, {  0,  9,  1 }, { 15, 15, 15 } },
			{ {  4,  9,  8 }, {  3,  0,  7 }, {  0,  7,  5 }, { 15, 15, 15 } },
			{ {  7,  3,  5 }, {  9,  8,  5 }, {  3,  9,  5 }, {  3,  9,  1 } },
			{ {  9,  8,  4 }, {  7,  6,  2 }, {  6,  2,  1 }, { 15, 15, 15 } },
			{ {  9,  7,  6 }, {  9,  7,  0 }, {  7,  2,  0 }, {  8,  9,  0 } },
			{ {  8,  4,  9 }, {  5,  1,  6 }, {  5,  6,  7 }, {  5,  0,  1 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8, 10,  5 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  0,  1 }, {  8,  5, 10 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  8,  2 }, {  8,  2, 10 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10,  4,  1 }, { 10,  8,  4 }, { 10,  1,  2 }, { 15, 15, 15 } },
			{ {  1,  3,  6 }, {  5,  8, 10 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8, 10,  5 }, {  6,  4,  3 }, {  4,  3,  0 }, { 15, 15, 15 } },
			{ {  3,  6,  1 }, { 10,  2,  8 }, {  2,  8,  0 }, { 15, 15, 15 } },
			{ {  3, 10,  2 }, {  3, 10,  4 }, { 10,  8,  4 }, {  6,  3,  4 } },
			{ {  2,  7,  3 }, {  5, 10,  8 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  1,  4 }, {  5,  8, 10 }, {  2,  3,  7 }, { 15, 15, 15 } },
			{ {  3, 10,  8 }, {  3,  7, 10 }, {  3,  8,  0 }, { 15, 15, 15 } },
			{ {  7, 10,  8 }, {  7,  3,  8 }, {  3,  8,  4 }, {  3,  1,  4 } },
			{ {  5,  8, 10 }, {  1,  2,  6 }, {  2,  6,  7 }, { 15, 15, 15 } },
			{ { 10,  5,  8 }, {  7,  0,  4 }, {  7,  4,  6 }, {  7,  2,  0 } },
			{ {  8, 10,  0 }, {  6,  1,  0 }, { 10,  6,  0 }, { 10,  6,  7 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  9, 10,  4 }, { 10,  4,  5 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  5, 10 }, {  1,  0,  5 }, {  1, 10,  9 }, { 15, 15, 15 } },
			{ {  9,  0,  2 }, {  9,  4,  0 }, {  9,  2, 10 }, { 15, 15, 15 } },
			{ {  9,  1,  2 }, {  9, 10,  2 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  3,  6 }, {  5,  4, 10 }, {  4, 10,  9 }, { 15, 15, 15 } },
			{ {  3,  6,  0 }, { 10,  5,  0 }, {  6, 10,  0 }, {  6, 10,  9 } },
			{ {  6,  1,  3 }, {  9,  0,  2 }, {  9,  2, 10 }, {  9,  4,  0 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7,  3,  2 }, {  9, 10,  4 }, { 10,  4,  5 }, { 15, 15, 15 } },
			{ {  3,  2,  7 }, {  1,  5, 10 }, {  1, 10,  9 }, {  1,  0,  5 } },
			{ {  7,  9, 10 }, {  7,  9,  0 }, {  9,  4,  0 }, {  3,  7,  0 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7,  2,  1 }, {  7,  6,  1 }, { 10,  5,  4 }, { 10,  9,  4 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  9,  6, 11 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  9,  6, 11 }, {  4,  1,  0 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  9,  6, 11 }, {  0,  5,  2 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  6, 11,  9 }, {  2,  1,  5 }, {  1,  5,  4 }, { 15, 15, 15 } },
			{ {  9,  1, 11 }, {  1, 11,  3 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  9, 11 }, {  0,  4,  9 }, {  0, 11,  3 }, { 15, 15, 15 } },
			{ {  0,  5,  2 }, {  9,  1, 11 }, {  1, 11,  3 }, { 15, 15, 15 } },
			{ { 11,  9,  3 }, {  5,  2,  3 }, {  9,  5,  3 }, {  9,  5,  4 } },
			{ {  6, 11,  9 }, {  3,  7,  2 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  6, 11,  9 }, {  1,  4,  0 }, {  3,  7,  2 }, { 15, 15, 15 } },
			{ { 11,  9,  6 }, {  5,  7,  0 }, {  7,  0,  3 }, { 15, 15, 15 } },
			{ {  9,  6, 11 }, {  4,  3,  7 }, {  4,  7,  5 }, {  4,  1,  3 } },
			{ {  9,  7,  2 }, {  9, 11,  7 }, {  9,  2,  1 }, { 15, 15, 15 } },
			{ {  2,  0,  4 }, {  2,  7,  4 }, {  7,  4,  9 }, {  7, 11,  9 } },
			{ { 11,  5,  7 }, { 11,  5,  1 }, {  5,  0,  1 }, {  9, 11,  1 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  6,  8 }, {  6,  8, 11 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 11,  1,  0 }, { 11,  6,  1 }, { 11,  0,  8 }, { 15, 15, 15 } },
			{ {  5,  2,  0 }, { 11,  8,  6 }, {  8,  6,  4 }, { 15, 15, 15 } },
			{ {  6,  2,  1 }, {  6,  2,  8 }, {  2,  5,  8 }, { 11,  6,  8 } },
			{ {  3,  4,  8 }, {  3,  1,  4 }, {  3,  8, 11 }, { 15, 15, 15 } },
			{ {  8, 11,  3 }, {  8,  0,  3 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  2,  0,  5 }, {  3,  4,  8 }, {  3,  8, 11 }, {  3,  1,  4 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  3,  2,  7 }, {  4,  6,  8 }, {  6,  8, 11 }, { 15, 15, 15 } },
			{ {  7,  3,  2 }, { 11,  1,  0 }, { 11,  0,  8 }, { 11,  6,  1 } },
			{ {  4,  8, 11 }, {  4,  6, 11 }, {  0,  5,  7 }, {  0,  3,  7 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  2,  7,  1 }, {  8,  4,  1 }, {  7,  8,  1 }, {  7,  8, 11 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8, 10,  5 }, {  9, 11,  6 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  0,  1 }, {  9,  6, 11 }, {  8,  5, 10 }, { 15, 15, 15 } },
			{ {  9,  6, 11 }, {  0,  8,  2 }, {  8,  2, 10 }, { 15, 15, 15 } },
			{ { 11,  9,  6 }, { 10,  4,  1 }, { 10,  1,  2 }, { 10,  8,  4 } },
			{ { 10,  5,  8 }, {  3, 11,  1 }, { 11,  1,  9 }, { 15, 15, 15 } },
			{ {  5,  8, 10 }, {  0,  9, 11 }, {  0, 11,  3 }, {  0,  4,  9 } },
			{ {  3,  1,  9 }, {  3, 11,  9 }, {  2,  0,  8 }, {  2, 10,  8 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7,  3,  2 }, { 10,  5,  8 }, { 11,  6,  9 }, { 15, 15, 15 } },
			{ {  8,  5, 10 }, {  9,  6, 11 }, {  4,  1,  0 }, {  3,  2,  7 } },
			{ {  6, 11,  9 }, {  3, 10,  8 }, {  3,  8,  0 }, {  3,  7, 10 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8, 10,  5 }, {  9,  7,  2 }, {  9,  2,  1 }, {  9, 11,  7 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  5, 11,  6 }, {  5, 10, 11 }, {  5,  6,  4 }, { 15, 15, 15 } },
			{ {  6,  1,  0 }, {  6, 11,  0 }, { 11,  0,  5 }, { 11, 10,  5 } },
			{ {  6, 11,  4 }, {  2,  0,  4 }, { 11,  2,  4 }, { 11,  2, 10 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10,  3, 11 }, { 10,  3,  4 }, {  3,  1,  4 }, {  5, 10,  4 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  2,  7,  3 }, {  5, 11,  6 }, {  5,  6,  4 }, {  5, 10, 11 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10, 11,  7 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10, 11,  7 }, {  4,  0,  1 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  5,  2,  0 }, { 10,  7, 11 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10, 11,  7 }, {  4,  5,  1 }, {  5,  1,  2 }, { 15, 15, 15 } },
			{ {  3,  6,  1 }, {  7, 11, 10 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7, 10, 11 }, {  0,  3,  4 }, {  3,  4,  6 }, { 15, 15, 15 } },
			{ {  3,  6,  1 }, {  2,  0,  5 }, {  7, 11, 10 }, { 15, 15, 15 } },
			{ { 11,  7, 10 }, {  6,  2,  5 }, {  6,  5,  4 }, {  6,  3,  2 } },
			{ {  2, 10,  3 }, { 10,  3, 11 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  4,  0 }, { 11,  3, 10 }, {  3, 10,  2 }, { 15, 15, 15 } },
			{ { 11,  5,  0 }, { 11, 10,  5 }, { 11,  0,  3 }, { 15, 15, 15 } },
			{ {  1, 11,  3 }, {  1, 11,  5 }, { 11, 10,  5 }, {  4,  1,  5 } },
			{ {  1, 11, 10 }, {  1,  6, 11 }, {  1, 10,  2 }, { 15, 15, 15 } },
			{ { 10, 11,  2 }, {  4,  0,  2 }, { 11,  4,  2 }, { 11,  4,  6 } },
			{ { 10,  5,  0 }, { 10, 11,  0 }, { 11,  0,  1 }, { 11,  6,  1 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10, 11,  7 }, {  8,  9,  4 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 11,  7, 10 }, {  1,  9,  0 }, {  9,  0,  8 }, { 15, 15, 15 } },
			{ {  5,  2,  0 }, {  8,  4,  9 }, { 10,  7, 11 }, { 15, 15, 15 } },
			{ {  7, 10, 11 }, {  2,  8,  9 }, {  2,  9,  1 }, {  2,  5,  8 } },
			{ { 11,  7, 10 }, {  9,  8,  4 }, {  6,  3,  1 }, { 15, 15, 15 } },
			{ { 10, 11,  7 }, {  8,  6,  3 }, {  8,  3,  0 }, {  8,  9,  6 } },
			{ {  4,  8,  9 }, {  1,  3,  6 }, {  0,  2,  5 }, {  7, 10, 11 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8,  4,  9 }, {  2, 10,  3 }, { 10,  3, 11 }, { 15, 15, 15 } },
			{ { 11, 10,  2 }, { 11,  3,  2 }, {  9,  8,  0 }, {  9,  1,  0 } },
			{ {  9,  8,  4 }, { 11,  5,  0 }, { 11,  0,  3 }, { 11, 10,  5 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  9,  8 }, {  1, 11, 10 }, {  1, 10,  2 }, {  1,  6, 11 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7,  5, 11 }, {  5, 11,  8 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  0,  1,  4 }, {  7,  5, 11 }, {  5, 11,  8 }, { 15, 15, 15 } },
			{ {  0,  7, 11 }, {  0,  2,  7 }, {  0, 11,  8 }, { 15, 15, 15 } },
			{ { 11,  7,  8 }, {  1,  4,  8 }, {  7,  1,  8 }, {  7,  1,  2 } },
			{ {  6,  1,  3 }, {  8, 11,  5 }, { 11,  5,  7 }, { 15, 15, 15 } },
			{ {  0,  4,  6 }, {  0,  3,  6 }, {  5,  8, 11 }, {  5,  7, 11 } },
			{ {  1,  3,  6 }, {  0,  7, 11 }, {  0, 11,  8 }, {  0,  2,  7 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  8,  2,  3 }, {  8,  5,  2 }, {  8,  3, 11 }, { 15, 15, 15 } },
			{ {  4,  0,  1 }, {  8,  2,  3 }, {  8,  3, 11 }, {  8,  5,  2 } },
			{ {  8,  0,  3 }, {  8, 11,  3 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  6,  8, 11 }, {  6,  8,  2 }, {  8,  5,  2 }, {  1,  6,  2 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  7,  9,  4 }, {  7, 11,  9 }, {  7,  4,  5 }, { 15, 15, 15 } },
			{ { 11,  1,  9 }, { 11,  1,  5 }, {  1,  0,  5 }, {  7, 11,  5 } },
			{ { 11,  9,  4 }, { 11,  7,  4 }, {  7,  4,  0 }, {  7,  2,  0 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  3,  6,  1 }, {  7,  9,  4 }, {  7,  4,  5 }, {  7, 11,  9 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  3,  2, 11 }, {  4,  9, 11 }, {  2,  4, 11 }, {  2,  4,  5 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 10,  9,  7 }, {  9,  7,  6 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4,  0,  1 }, { 10,  9,  7 }, {  9,  7,  6 }, { 15, 15, 15 } },
			{ {  2,  0,  5 }, {  6,  7,  9 }, {  7,  9, 10 }, { 15, 15, 15 } },
			{ {  6,  9, 10 }, {  6,  7, 10 }, {  1,  4,  5 }, {  1,  2,  5 } },
			{ { 10,  3,  1 }, { 10,  7,  3 }, { 10,  1,  9 }, { 15, 15, 15 } },
			{ {  7,  0,  3 }, {  7,  0,  9 }, {  0,  4,  9 }, { 10,  7,  9 } },
			{ {  5,  2,  0 }, { 10,  3,  1 }, { 10,  1,  9 }, { 10,  7,  3 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  2,  6,  9 }, {  2,  3,  6 }, {  2,  9, 10 }, { 15, 15, 15 } },
			{ {  0,  1,  4 }, {  2,  6,  9 }, {  2,  9, 10 }, {  2,  3,  6 } },
			{ {  9,  6, 10 }, {  0,  5, 10 }, {  6,  0, 10 }, {  6,  0,  3 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  9, 10 }, {  1,  2, 10 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  4, 10,  7 }, {  4,  8, 10 }, {  4,  7,  6 }, { 15, 15, 15 } },
			{ {  7, 10,  6 }, {  0,  1,  6 }, { 10,  0,  6 }, { 10,  0,  8 } },
			{ {  0,  5,  2 }, {  4, 10,  7 }, {  4,  7,  6 }, {  4,  8, 10 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  4,  8 }, {  1,  3,  8 }, {  3,  8, 10 }, {  3,  7, 10 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  3,  4,  6 }, {  3,  4, 10 }, {  4,  8, 10 }, {  2,  3, 10 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  6,  8,  5 }, {  6,  9,  8 }, {  6,  5,  7 }, { 15, 15, 15 } },
			{ {  1,  4,  0 }, {  6,  8,  5 }, {  6,  5,  7 }, {  6,  9,  8 } },
			{ {  9,  0,  8 }, {  9,  0,  7 }, {  0,  2,  7 }, {  6,  9,  7 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  1,  3,  9 }, {  5,  8,  9 }, {  3,  5,  9 }, {  3,  5,  7 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  9,  8,  5 }, {  9,  6,  5 }, {  6,  5,  2 }, {  6,  3,  2 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ {  6,  4,  5 }, {  6,  7,  5 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } },
			{ { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 }, { 15, 15, 15 } }
		};
	}
}
