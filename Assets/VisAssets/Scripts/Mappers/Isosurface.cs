using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Isosurface))]
	public class IsosurfaceEditor : Editor
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

			var isosurface = target as Isosurface;

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

	public class Isosurface : MapperModuleTemplate
	{
		public enum SHADING_MODE
		{
			FLAT,
			SMOOTH
		};

		DataElement element;

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

		int[] coord_idx = new int[8];
		float[] vlocal  = new float[8];
		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		List<int>     triangles;
		Material      material;

		public override void InitModule()
		{
			vertices  = new List<Vector3>();
			normals   = new List<Vector3>();
			colors    = new List<Color>();
			triangles = new List<int>();
			color     = new Color(0, 1f, 0, 1f);
//			material  = new Material(Shader.Find("Custom/SurfaceShader"));
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
			Debug.Log(" Exec : Isosurface module");
			Calc();

			return 1;
		}

		public override void ReSetParameters()
		{
			element = pdf.elements[0];

			min = element.min;
			max = element.max;
//			threshold = element.average + element.variance * 3f;
//			threshold = min;
//			slider = (threshold - min) / (max - min);
			threshold = min + (max - min) * slider;
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

		void Calc()
		{
			vertices.Clear();
			colors.Clear();
			normals.Clear();
			triangles.Clear();

			int mx = element.dims[0];
			int my = element.dims[1];
			int mz = element.dims[2];

			int count = 0;
			for (int k = 0; k < mz - 1; k++)
			{
				for (int j = 0; j < my - 1; j++)
				{
					for (int i = 0; i < mx - 1; i++)
					{
						coord_idx[0] = i + j * mx + k * mx * my;
						coord_idx[1] = (i + 1) + j * mx + k * mx * my;
						coord_idx[2] = (i + 1) + (j + 1) * mx + k * mx * my;
						coord_idx[3] = i + (j + 1) * mx + k * mx * my;
						coord_idx[4] = i + j * mx + (k + 1) * mx * my;
						coord_idx[5] = (i + 1) + j * mx + (k + 1) * mx * my;
						coord_idx[6] = (i + 1) + (j + 1) * mx + (k + 1) * mx * my;
						coord_idx[7] = i + (j + 1) * mx + (k + 1) * mx * my;

						int vtype = 0;
						for (int n = 0; n < 8; n++)
						{
							vlocal[n] = element.values[coord_idx[n]];
							vtype |= ((vlocal[n] > threshold) ? 1 : 0) << n;
						}

						int tri = 0;
						if (trinum[vtype] > 0)
						{
							tri = triindex[vtype * 14 + 1];

							for (int n = 0; n < tri; n++)
							{
								List<int> edges = new List<int>();
								int index = vtype * 14 + 2 + n * 3;
								for (int v = 0; v < 3; v++)
								{
									int edge = triindex[index + v];
									if (edge < 8)
									{
										if ((edge == 3) || (edge == 7))
										{
											edges.Add(edge);
											edges.Add(edge - 3);
										}
										else
										{
											edges.Add(edge);
											edges.Add(edge + 1);
										}
									}
									else
									{
										edges.Add(edge % 8);
										edges.Add(edge % 8 + 4);
									}
									triangles.Add(count * 3 + n * 3 + v);
								}
								AddGeometry(edges);
							}
							count += tri;
						}
					}
				}
			}

			Vector3[] finalNormals = null;
			if (shading == SHADING_MODE.SMOOTH)
			{
				finalNormals = new Vector3[normals.Count()];
				for (int n = 0; n < normals.Count(); n++)
				{
					if (finalNormals[n] == Vector3.zero)
					{
						List<int> index = new List<int>();
						index.Add(n);
						finalNormals[n] = normals[n];
						for (int c = n + 1; c < normals.Count(); c++)
						{
							if (vertices[c] == vertices[n])
							{
								finalNormals[n] += normals[c];
								finalNormals[n] = finalNormals[n].normalized;
								index.Add(c);
							}
						}
						for (int c = 1; c < index.Count(); c++)
						{
							finalNormals[index[c]] = finalNormals[n];
						}
					}
				}
			}

			var mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			if (shading == SHADING_MODE.SMOOTH)
			{
				mesh.normals = finalNormals;
			}
			else
			{
				mesh.SetNormals(normals);
			}
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();

			var meshFilter = GetComponent<MeshFilter>();
			meshFilter.mesh = mesh;
		}

		private void AddGeometry(List<int> edge)
		{
			float x0, y0, z0, x1, y1, z1, a, b;
			float[] x = new float[3];
			float[] y = new float[3];
			float[] z = new float[3];
			int p0, p1, idx0, idx1;

			for (int i = 0; i < 3; i++)
			{
				p0 = edge[i * 2];
				p1 = edge[i * 2 + 1];
				idx0 = coord_idx[p0] * 3;
				idx1 = coord_idx[p1] * 3;
				x0 = element.coords[3][idx0];
				y0 = element.coords[3][idx0 + 1];
				z0 = element.coords[3][idx0 + 2];
				x1 = element.coords[3][idx1];
				y1 = element.coords[3][idx1 + 1];
				z1 = element.coords[3][idx1 + 2];
				a = Math.Abs(threshold - vlocal[p0]);
				b = Math.Abs(threshold - vlocal[p1]);
				x[i] = (a * x1 + b * x0) / (a + b);
				y[i] = (a * y1 + b * y0) / (a + b);
				z[i] = (a * z1 + b * z0) / (a + b);
			}
			var v0 = new Vector3(x[0], y[0], z[0]);
			var v1 = new Vector3(x[1], y[1], z[1]);
			var v2 = new Vector3(x[2], y[2], z[2]);
			vertices.Add(v0);
			vertices.Add(v1);
			vertices.Add(v2);
			var norm = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
			normals.Add(new Vector3(norm.x, norm.y, norm.z));
			normals.Add(new Vector3(norm.x, norm.y, norm.z));
			normals.Add(new Vector3(norm.x, norm.y, norm.z));
			for (int i = 0; i < 3; i++)
			{
				colors.Add(color);
			}
		}

		private void AddGeometry(int v0, int v1)
		{
			float x0, y0, z0, x1, y1, z1;
			float x, y, z, a, b;

			int idx0 = coord_idx[v0] * 3;
			int idx1 = coord_idx[v1] * 3;

			x0 = element.coords[3][idx0];
			y0 = element.coords[3][idx0 + 1];
			z0 = element.coords[3][idx0 + 2];
			x1 = element.coords[3][idx1];
			y1 = element.coords[3][idx1 + 1];
			z1 = element.coords[3][idx1 + 2];

			a = Math.Abs(threshold - vlocal[v0]);
			b = Math.Abs(threshold - vlocal[v1]);
			x = (a * x1 + b * x0) / (a + b);
			y = (a * y1 + b * y0) / (a + b);
			z = (a * z1 + b * z0) / (a + b);
			vertices.Add(new Vector3(x, y, z));
			colors.Add(color);
		}

		int[] trinum = {
			0,1,1,2,1,2,2,3,1,2,2,3,2,3,3,2,1,2,2,3,2,3,3,4,2,3,3,4,3,4,4,3,1,
			2,2,3,2,3,3,4,2,3,3,4,3,4,4,3,2,3,3,2,3,4,4,3,3,4,4,3,4,3,3,2,1,2,
			2,3,2,3,3,4,2,3,3,4,3,4,4,3,2,3,3,4,3,4,4,3,3,4,4,3,4,3,3,2,2,3,3,
			4,3,4,2,3,3,4,4,3,4,3,3,2,3,4,4,3,4,3,3,2,4,3,3,2,3,2,2,1,1,2,2,3,
			2,3,3,4,2,3,3,4,3,4,4,3,2,3,3,4,3,4,4,3,3,2,4,3,4,3,3,2,2,3,3,4,3,
			4,4,3,3,4,4,3,4,3,3,2,3,4,4,3,4,3,3,2,4,3,3,2,3,2,2,1,2,3,3,4,3,4,
			4,3,3,4,4,3,2,3,3,2,3,4,4,3,4,3,3,2,4,3,3,2,3,2,2,1,3,4,4,3,4,3,3,
			2,4,3,3,2,3,2,2,1,2,3,3,2,3,2,2,1,3,2,2,1,2,1,1,0
		};

		int[] triindex = {
	0,0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	1,1,3,0,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	2,1,0,1,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	3,2,3,1,8,8,1,9,-1,-1,-1,-1,-1,-1,
	4,1,1,2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	5,2,3,0,8,1,2,10,-1,-1,-1,-1,-1,-1,
	6,2,0,2,9,9,2,10,-1,-1,-1,-1,-1,-1,
	7,3,9,8,10,10,8,2,3,2,8,-1,-1,-1,
	8,1,2,3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	9,2,2,0,11,11,0,8,-1,-1,-1,-1,-1,-1,
	10,2,0,1,9,2,3,11,-1,-1,-1,-1,-1,-1,
	11,3,9,8,11,2,9,11,2,1,9,-1,-1,-1,
	12,2,1,3,11,1,11,10,-1,-1,-1,-1,-1,-1,
	13,3,8,11,10,1,8,10,1,0,8,-1,-1,-1,
	14,3,10,9,11,9,3,11,3,9,0,-1,-1,-1,
	15,2,9,8,10,8,11,10,-1,-1,-1,-1,-1,-1,
	16,1,4,7,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	17,2,0,4,3,3,4,7,-1,-1,-1,-1,-1,-1,
	18,2,0,1,9,4,7,8,-1,-1,-1,-1,-1,-1,
	19,3,3,1,7,7,1,4,4,1,9,-1,-1,-1,
	20,2,1,2,10,4,7,8,-1,-1,-1,-1,-1,-1,
			21,3,2,1,10,0,4,7,0,7,3,-1,-1,-1,
			22,3,4,7,8,0,2,10,0,10,9,-1,-1,-1,
			23,4,2,0,7,2,3,0,0,4,7,7,10,2,
	24,2,2,3,11,4,7,8,-1,-1,-1,-1,-1,-1,
	25,3,2,0,4,2,4,7,2,7,11,-1,-1,-1,
	26,3,0,1,9,2,3,11,4,7, 8,-1,-1,-1,
	27,4,2,1,11,1,7,11,1,4,7,4,1,9,
			28,3,4,7,8,1,3,11,1,11,10,-1,-1,-1,
			29,4,0,10,7,0,1,10,10,11,7,7,4,0,
			30,4,4,7,8,9,10,11,11,9,3,3,9,0,
	31,3,10,9,11,9,7,11,4,7,9,-1,-1,-1,
	32,1,5,4,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	33,2,3,0,8,5,4,9,-1,-1,-1,-1,-1,-1,
	34,2,1,5,0,0,5,4,-1,-1,-1,-1,-1,-1,
	35,3,3,1,5,3,5,4,3,4,8,-1,-1,-1,
	36,2,1,2,10,5,4,9,-1,-1,-1,-1,-1,-1,
	37,3,3,0,8,1,2,10,5,4, 9,-1,-1,-1,
	38,3,0,2,4,4,2,5,5,2,10,-1,-1,-1,
	39,4,3,2,8,2,4,8,4,2,5,5,2,10,
	40,2,2,3,11,5,4,9,-1,-1,-1,-1,-1,-1,
			41,3,5,4,9,0,2,11,0,11,8,-1,-1,-1,
			42,3,3,2,11,0,4,5,0,5,1,-1,-1,-1,
			43,4,1,11,4,1,2,11,11,8,4,4,5,1,
			44,3,5,4,9,1,3,11,1,11,10,-1,-1,-1,
			45,4,5,4,9,8,11,10,10,8,1,1,8,0,
			46,4,0,11,5,0,3,11,11,10,5,5,4,0,
	47,3,10,8,11,8,10,5,8,5,4,-1,-1,-1,
	48,2,8,9,7,7,9,5,-1,-1,-1,-1,-1,-1,
	49,3,3,5,7,5,3,9,9,3,0,-1,-1,-1,
	50,3,1,5,7,1,7,8,0,1,8,-1,-1,-1,
	51,2,3,1,7,1,5,7,-1,-1,-1,-1,-1,-1,
			52,3,2,1,10,5,7,8,5,8,9,-1,-1,-1,
			53,4,2,1,10,3,7,5,5,3,9,9,3,0,
			54,4,0,10,7,0,2,10,10,5,7,7,8,0,
	55,3,3,5,7,5,3,10,3,2,10,-1,-1,-1,
			56,3,3,2,11,5,7,8,5,8,9,-1,-1,-1,
			57,4,0,11,5,0,2,11,11,7,5,5,9,0,
			58,4,3,2,11,1,5,7,7,1,8,8,1,0,
	59,3,7,1,5,1,7,11,2,1,11,-1,-1,-1,
	60,4,1,3,11,1,11,10,5,7,9,7,8,9,
			61,3,1,9,0,5,7,11,5,11,10,-1,-1,-1,
			62,3,0,3,8,5,7,11,5,11,10,-1,-1,-1,
	63,2,11,10,7,7,10,5,-1,-1,-1,-1,-1,-1,
	64,1,6,5,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	65,2,3,0,8,6,5,10,-1,-1,-1,-1,-1,-1,
	66,2,0,1,9,6,5,10,-1,-1,-1,-1,-1,-1,
			67,3,6,5,10,1,3,8,1,8,9,-1,-1,-1,
	68,2,2,6,1,1,6,5,-1,-1,-1,-1,-1,-1,
			69,3,0,3,8,1,5,6,1,6,2,-1,-1,-1,
	70,3,0,2,6,6,5,0,0,5,9,-1,-1,-1,
			71,4,2,8,5,2,3,8,8,9,5,5,6,2,
	72,2,2,3,11,6,5,10,-1,-1,-1,-1,-1,-1,
			73,3,6,5,10,0,2,11,0,11,8,-1,-1,-1,
	74,3,0,1,9,2,3,11,6,5,10,-1,-1,-1,
			75,4,6,5,10,9,8,11,11,9,2,2,9,1,
	76,3,1,3,5,5,3,6,6,3,11,-1,-1,-1,
			77,4,0,10,6,0,1,10,10,5,6,6,11,0,
			78,4,0,3,11,0,11,6,0,6,5,0,5,9,
	79,3,9,8,11,6,9,11,6,5,9,-1,-1,-1,
	80,2,4,7,8,6,5,10,-1,-1,-1,-1,-1,-1,
			81,3,6,5,10,0,4,7,0,7,3,-1,-1,-1,
	82,3,0,1,9,4,7,8,6,5,10,-1,-1,-1,
			83,4,6,5,10,1,3,7,7,1,4,4,1,9,
			84,3,4,7,8,1,5,6,1,6,2,-1,-1,-1,
			85,4,0,2,1,0,1,3,4,6,5,4,5,7,
			86,4,4,7,8,0,2,6,6,0,5,5,0,9,
			87,3,5,4,9,2,6,7,2,7,3,-1,-1,-1,
	88,3,2,3,11,4,7,8,6,5,10,-1,-1,-1,
			89,4,6,5,10,2,0,4,4,2,7,7,2,11,
			90,4,1,9,0,3,2,11,4,7,8,6,5,10,
	91,3,2,1,10,4,5,9,6,7,11,-1,-1,-1,
			92,4,4,7,8,3,1,5,5,3,6,6,3,11,
			93,3,7,6,11,0,4,5,0,5,1,-1,-1,-1,
	94,3,0,3,8,4,5,9,6,7,11,-1,-1,-1,
	95,2,4,5,9,6,7,11,-1,-1,-1,-1,-1,-1,
	96,2,9,10,4,4,10,6,-1,-1,-1,-1,-1,-1,
			97,3,0,3,8,4,6,10,4,10,9,-1,-1,-1,
	98,3,0,6,4,6,0,10,0,1,10,-1,-1,-1,
			99,4,1,8,6,1,3,8,8,4,6,6,10,1,
	100,3,4,2,6,2,4,9,1,2,9,-1,-1,-1,
			101,4,0,3,8,2,6,4,4,2,9,9,2,1,
	102,2,0,2,4,2,6,4,-1,-1,-1,-1,-1,-1,
	103,3,4,2,6,2,4,8,3,2,8,-1,-1,-1,
			104,3,3,2,11,4,6,10,4,10,9,-1,-1,-1,
	105,4,2,8,11,2,0,8,4,9,10,6,4,10,
			106,4,3,2,11,0,4,6,6,0,10,10,0,1,
			107,3,2,1,10,4,6,11,4,11,8,-1,-1,-1,
			108,4,1,6,9,1,10,6,6,4,9,9,11,1,
			109,3,1,9,0,4,6,11,4,11,8,-1,-1,-1,
	110,3,4,0,6,6,0,11,0,3,11,-1,-1,-1,
	111,2,6,4,11,11,4,8,-1,-1,-1,-1,-1,-1,
	112,3,8,9,10,6,8,10,6,7,8,-1,-1,-1,
			113,4,0,7,10,0,3,7,7,6,10,10,9,0,
			114,4,0,1,10,0,10,6,0,6,7,0,7,8,
	115,3,3,1,7,7,1,6,6,1,10,-1,-1,-1,
			116,4,1,10,7,1,2,10,10,6,7,7,9,1,
			117,3,1,9,0,2,6,7,2,7,3,-1,-1,-1,
	118,3,0,2,6,0,6,7,0,7,8,-1,-1,-1,
	119,2,7,3,6,6,3,2,-1,-1,-1,-1,-1,-1,
			120,4,3,2,11,8,9,10,10,8,6,6,8,7,
			121,3,7,6,11,0,2,10,0,10,9,-1,-1,-1,
	122,3,0,3,8,2,1,10,6,7,11,-1,-1,-1,
	123,2,2,1,10,6,7,11,-1,-1,-1,-1,-1,-1,
			124,3,7,6,11,1,3,8,1,8,9,-1,-1,-1,
	125,2,1,0,9,6,7,11,-1,-1,-1,-1,-1,-1,
	126,2,0,3,8,6,7,11,-1,-1,-1,-1,-1,-1,
	127,1,6,7,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	128,1,7,6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	129,2,3,0,8,7,6,11,-1,-1,-1,-1,-1,-1,
	130,2,0,1,0,7,6,11,-1,-1,-1,-1,-1,-1,
			131,3,7,6,11,1,3,8,1,8,9,-1,-1,-1,
	132,2,1,2,10,7,6,11,-1,-1,-1,-1,-1,-1,
	133,3,3,0,8,1,2,10,7,6,11,-1,-1,-1,
			134,3,7,6,11,0,2,10,0,10,9,-1,-1,-1,
			135,4,7,6,11,8,9,10,10,8,2,2,8,3,
	136,2,3,7,2,2,7,6,-1,-1,-1,-1,-1,-1,
	137,3,2,0,6,6,0,7,7,0,8,-1,-1,-1,
			138,3,1,9,0,2,6,7,2,7,3,-1,-1,-1,
			139,4,1,10,7,1,2,10,10,6,7,7,9,1,
	140,3,1,3,7,1,7,6,1,6,10,-1,-1,-1,
	141,4,1,0,10,10,0,6,6,0,7,7,0,8,
			142,4,0,7,10,0,3,7,7,6,10,10,9,0,
	143,3,9,8,10,8,6,10,7,6,8,-1,-1,-1,
	144,2,4,6,8,8,6,11,-1,-1,-1,-1,-1,-1,
	145,3,0,4,6,0,6,11,3,0,11,-1,-1,-1,
			146,3,1,9,0,4,6,11,4,11,8,-1,-1,-1,
			147,4,1,6,9,1,10,6,6,4,9,9,11,1,
			148,3,2,1,10,4,6,11,4,11,8,-1,-1,-1,
			149,4,2,1,10,0,4,6,6,0,11,11,0,3,
	150,4,8,2,11,0,2,8,9,4,10,4,6,10,
			151,3,3,2,11,4,6,10,4,10,9,-1,-1,-1,
	152,3,2,4,6,4,2,8,2,3,8,-1,-1,-1,
	153,2,2,0,4,6,2,4,-1,-1,-1,-1,-1,-1,
			154,4,1,9,0,2,6,4,4,2,8,8,2,3,
	155,3,2,4,6,4,2,9,2,1,9,-1,-1,-1,
			156,4,1,6,8,1,10,6,6,4,8,8,3,1,
	157,3,6,0,4,0,6,10,1,0,10,-1,-1,-1,
			158,3,0,3,8,4,6,10,4,10,9,-1,-1,-1,
	159,2,10,9,6,6,9,4,-1,-1,-1,-1,-1,-1,
	160,2,5,4,9,7,6,11,-1,-1,-1,-1,-1,-1,
	161,3,3,0,8,5,4,9,7,6,11,-1,-1,-1,
			162,3,7,6,11,0,4,5,0,5,1,-1,-1,-1,
			163,4,7,6,11,3,1,5,5,3,4,4,3,8,
	164,3,1,2,10,5,4,9,7,6,11,-1,-1,-1,
			165,4,0,3,8,2,1,10,5,4,9,7,6,11,
			166,4,7,6,11,2,0,4,4,2,5,5,2,10,
	167,3,3,2,11,7,4,8,5,6,10,-1,-1,-1,
			168,3,5,4,9,2,6,7,2,7,3,-1,-1,-1,
			169,4,5,4,9,0,2,6,6,0,7,7,0,8,
			170,4,0,2,1,0,1,3,4,6,5,4,5,7,
			171,3,4,7,8,1,5,6,1,6,2,-1,-1,-1,
			172,4,5,4,9,1,3,7,7,1,6,6,1,10,
	173,3,1,0,9,7,4,8,5,6,10,-1,-1,-1,
			174,3,6,5,10,0,4,7,0,7,3,-1,-1,-1,
	175,2,7,4,8,5,6,10,-1,-1,-1,-1,-1,-1,
	176,3,8,9,11,9,6,11,5,6,9,-1,-1,-1,
	177,4,3,0,11,0,6,11,6,0,5,5,0,9,
			178,4,0,5,11,0,1,5,5,6,11,11,8,0,
	179,3,3,1,5,3,5,6,3,6,11,-1,-1,-1,
			180,4,2,1,10,9,8,11,11,9,6,6,9,5,
	181,3,1,0,9,3,2,11,5,6,10,-1,-1,-1,
			182,3,6,5,10,0,2,11,0,11,8,-1,-1,-1,
	183,2,3,2,11,5,6,10,-1,-1,-1,-1,-1,-1,
			184,4,2,8,5,2,3,8,8,9,5,5,6,2,
	185,3,2,0,6,5,6,0,5,0,9,-1,-1,-1,
			186,3,0,3,8,1,5,6,1,6,2,-1,-1,-1,
	187,2,6,2,5,5,2,1,-1,-1,-1,-1,-1,-1,
			188,3,6,5,10,1,3,8,1,8,9,-1,-1,-1,
	189,2,1,0,9,5,6,10,-1,-1,-1,-1,-1,-1,
	190,2,0,3,8,5,6,10,-1,-1,-1,-1,-1,-1,
	191,1,5,6,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	192,2,10,11,5,5,11,7,-1,-1,-1,-1,-1,-1,
			193,3,0,3,8,5,7,11,5,11,10,-1,-1,-1,
			194,3,1,9,0,5,7,11,5,11,10,-1,-1,-1,
	195,4,3,1,11,11,1,10,7,5,9,8,7,9,
	196,3,1,7,5,7,1,11,1,2,11,-1,-1,-1,
			197,4,0,3,8,1,5,7,7,1,11,11,1,2,
			198,4,0,11,5,0,2,11,11,7,5,5,9,0,
			199,3,3,2,11,5,7,8,5,8,9,-1,-1,-1,
	200,3,5,3,7,3,5,10,2,3,10,-1,-1,-1,
			201,4,0,10,7,0,2,10,10,5,7,7,8,0,
			202,4,1,9,0,3,7,5,5,3,10,10,3,2,
			203,3,2,1,10,5,7,8,5,8,9,-1,-1,-1,
	204,2,1,3,7,5,1,7,-1,-1,-1,-1,-1,-1,
	205,3,5,1,7,7,1,8,1,0,8,-1,-1,-1,
	206,3,5,3,7,3,5,9,3,9,0,-1,-1,-1,
	207,2,9,8,5,5,8,7, -1,-1,-1,-1,-1,-1,
	208,3,8,10,11,10,8,5,5,8,4,-1,-1,-1,
			209,4,0,11,5,0,3,11,11,10,5,5,4,0,
			210,4,1,9,0,8,11,10,10,8,5,5,8,4,
			211,3,5,4,9,1,3,11,1,11,10,-1,-1,-1,
			212,4,1,11,4,1,2,11,11,8,4,4,5,1,
			213,3,3,2,11,0,4,5,0,5,1,-1,-1,-1,
			214,3,5,4,9,0,2,11,0,11,8,-1,-1,-1,
	215,2,3,2,11,4,5,9,-1,-1,-1,-1,-1,-1,
	216,4,2,3,8,4,2,8,2,4,5,2,5,10,
	217,3,2,0,4,2,4,5,2,5,10,-1,-1,-1,
	218,3,0,3,8,2,1,10,4,5, 9,-1,-1,-1,
	219,2,2,1,10,4,5,9,-1,-1,-1,-1,-1,-1,
	220,3,1,3,5,5,3,4,4,3,8,-1,-1,-1,
	221,2,5,1,4,4,1,0,-1,-1,-1,-1,-1,-1,
	222,2,0,3,8,4,5,9,-1,-1,-1,-1,-1,-1,
	223,1,4,5,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	224,3,9,10,11,7,9,11,7,4,9,-1,-1,-1,
			225,4,0,3,8,9,10,11,11,9,7,7,9,4,
			226,4,0,10,7,0,1,10,10,11,7,7,4,0,
			227,3,4,7,8,1,3,11,1,11,10,-1,-1,-1,
	228,4,1,2,11,7,1,11,4,1,7,1,4,9,
	229,3,1,0,9,3,2,11,7,4, 8,-1,-1,-1,
	230,3,0,2,4,4,2,7,7,2,11,-1,-1,-1,
	231,2,3,2,11,7,4,8,-1,-1,-1,-1,-1,-1,
			232,4,2,7,9,2,3,7,7,4,9,9,10,2,
			233,3,4,7,8,0,2,10,0,10,9,-1,-1,-1,
			234,3,2,1,10,0,4,7,0,7,3,-1,-1,-1,
	235,2,2,1,10,7,4,8,-1,-1,-1,-1,-1,-1,
	236,3,1,3,7,1,7,4,1,4,9,-1,-1,-1,
	237,2,1,0,9,7,4,8,-1,-1,-1,-1,-1,-1,
	238,2,4,0,7,7,0,3,-1,-1,-1,-1,-1,-1,
	239,1,7,4,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	240,2,8,9,10,11,8,10,-1,-1,-1,-1,-1,-1,
	241,3,9,10,11,3,9,11,9,3,0,-1,-1,-1,
	242,3,11,8,10,8,1,10,0,1,8,-1,-1,-1,
	243,2,3,1,11,11,1,10,-1,-1,-1,-1,-1,-1,
	244,3,8,9,11,9,2,11,1,2,9,-1,-1,-1,
	245,2,1,0,9,3,2,11,-1,-1,-1,-1,-1,-1,
	246,2,11,8,2,2,8,0,-1,-1,-1,-1,-1,-1,
	247,1,3,2,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	248,3,8,9,10,8,10,2,2,3,8,-1,-1,-1,
	249,2,2,0,9,2,9,10,-1,-1,-1,-1,-1,-1,
	250,2,0,3,8,2,1,10,-1,-1,-1,-1,-1,-1,
	251,1,2,1,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	252,2,1,3,9,9,3,8,-1,-1,-1,-1,-1,-1,
	253,1,1,0,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	254,1,0,3,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,
	255,0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1
		};
	}
}