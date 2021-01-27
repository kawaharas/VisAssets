using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(StreamLines))]
	public class StreamLinesEditor : Editor
	{
		SerializedProperty p0;
		SerializedProperty p1;
		float[] _p0 = new float[3];
		float[] _p1 = new float[3];
		SerializedProperty p0x, p0y, p0z;
		SerializedProperty p1x, p1y, p1z;

		private void OnEnable()
		{
			serializedObject.FindProperty("__dummy__"); // for null value
			p0x = serializedObject.FindProperty("p0x");
			p0y = serializedObject.FindProperty("p0y");
			p0z = serializedObject.FindProperty("p0z");
			p1x = serializedObject.FindProperty("p1x");
			p1y = serializedObject.FindProperty("p1y");
			p1z = serializedObject.FindProperty("p1z");
			p0 = serializedObject.FindProperty("p0");
			p1 = serializedObject.FindProperty("p1");
			for (int i = 0; i < p0.arraySize; ++i)
			{
				_p0[i] = p0.GetArrayElementAtIndex(i).floatValue;
				_p1[i] = p1.GetArrayElementAtIndex(i).floatValue;
			}
		}

		public override void OnInspectorGUI()
		{
			var streamlines = target as StreamLines;

			base.DrawDefaultInspector();

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			var color = EditorGUILayout.ColorField("Color:", streamlines.color);
			GUILayout.Space(10f);
			GUILayout.BeginHorizontal();
			GUILayout.Label("Point 0:");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.BeginVertical(GUI.skin.box);
			GUILayout.Space(3f);
			EditorGUIUtility.labelWidth = 35;
			EditorGUIUtility.fieldWidth = 50;
			var _p0x_tmp = EditorGUILayout.Slider("p0x:", _p0[0], 0, 1f);
			GUILayout.Space(3f);
			var _p0y_tmp = EditorGUILayout.Slider("p0y:", _p0[1], 0, 1f);
			GUILayout.Space(3f);
			var _p0z_tmp = EditorGUILayout.Slider("p0z:", _p0[2], 0, 1f);
			EditorGUIUtility.labelWidth = 0;
			EditorGUIUtility.fieldWidth = 0;
			GUILayout.Space(3f);
			GUILayout.EndVertical();

			GUILayout.Space(5f);

			GUILayout.BeginHorizontal();
			GUILayout.Label("Point 1:");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.BeginVertical(GUI.skin.box);
			GUILayout.Space(3f);
			EditorGUIUtility.labelWidth = 35;
			EditorGUIUtility.fieldWidth = 50;
			var _p1x_tmp = EditorGUILayout.Slider("p1x:", _p1[0], 0, 1f);
			GUILayout.Space(3f);
			var _p1y_tmp = EditorGUILayout.Slider("p1y:", _p1[1], 0, 1f);
			GUILayout.Space(3f);
			var _p1z_tmp = EditorGUILayout.Slider("p1z:", _p1[2], 0, 1f);
			EditorGUIUtility.labelWidth = 0;
			EditorGUIUtility.fieldWidth = 0;
			GUILayout.Space(3f);
			GUILayout.EndVertical();

//			EditorGUIUtility.labelWidth = 0;
//			EditorGUIUtility.fieldWidth = 0;
			var _p0x = EditorGUILayout.Slider("p0x:", p0x.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p0y = EditorGUILayout.Slider("p0y:", p0y.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p0z = EditorGUILayout.Slider("p0z:", p0z.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p1x = EditorGUILayout.Slider("p1x:", p1x.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p1y = EditorGUILayout.Slider("p1y:", p1y.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p1z = EditorGUILayout.Slider("p1z:", p1z.floatValue, 0, 1f);
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "StreamLines");

				streamlines.SetColor(color);

				p0x.floatValue = _p0x;
				p0y.floatValue = _p0y;
				p0z.floatValue = _p0z;
				p1x.floatValue = _p1x;
				p1y.floatValue = _p1y;
				p1z.floatValue = _p1z;
/*
				p0x.floatValue = _p0x_tmp;
				p0y.floatValue = _p0y_tmp;
				p0z.floatValue = _p0z_tmp;
				p1x.floatValue = _p1x_tmp;
				p1y.floatValue = _p1y_tmp;
				p1z.floatValue = _p1z_tmp;
*/
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class StreamLines : MapperModuleTemplate
	{
		List<Vector3> vertices;
		List<Color>   colors;
		List<int>     indices;
		public bool   IsAnimation;
		public bool   IsRepeat;
		public bool   UseMagnitude = false;
		public int    step;
		GameObject    sphere;
		public float  sphereScale;
		public int    maxStep;

		[SerializeField]
		public Color color;
		Material material;
		Mesh mesh;

		public float[] p0;
		public float[] p1;
		public float p0x, p0y, p0z;
		public float p1x, p1y, p1z;

		[SerializeField, ReadOnly]
		public DataElement[] elements;
		public List<int> activeElements;
		public int[] dims;
		public float[][] coords;
		public float min;
		public float max;
		public float undef;
		public bool  useUndef;
//		float h = 5e-3f;
		float h = 0.05f;
		public float magMin;
		public float magMax;
		public float magnitude;

		public override void InitModule()
		{
			vertices = new List<Vector3>();
			indices  = new List<int>();
			colors   = new List<Color>();
			color    = new Color(1f, 1f, 1f);
			material = new Material(Shader.Find("Sprites/Default"));

			IsAnimation = false;
			IsRepeat    = false;
			step = 0;
			maxStep = 5000;
			sphereScale = 0.02f;
			sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.transform.parent = this.gameObject.transform;
			sphere.transform.localScale = new Vector3(sphereScale, sphereScale, sphereScale);
			sphere.hideFlags = HideFlags.HideInHierarchy;
			sphere.SetActive(false);
			magMin = float.MaxValue;
			magMax = float.MinValue;

			elements = new DataElement[3];
			dims = new int[3] { -1, -1, -1 };
			useUndef = false;

			p0 = new float[3];
			p1 = new float[3];

			var transform = GetComponent<Transform>();
//			transform.hideFlags = HideFlags.HideInInspector;

			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.mesh = mesh;
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				meshRenderer.material  = material;
				meshRenderer.hideFlags = HideFlags.HideInInspector;
			}
		}

		public override int BodyFunc()
		{
			if (activeElements.Count > 0)
			{
				RungeKutta();
			}

			return 1;
		}

		public override void IdleFunc()
		{
			if (IsAnimation)
			{
				step++;
				if (IsRepeat)
				{
					if (step * 2 > indices.Count)
					{
						step = 0;
					}
				}
				else
				{
					Mathf.Clamp(step, 0, indices.Count / 2);
				}
				int[] subIndices = new int[step];
				System.Array.Copy(indices.ToArray(), subIndices, step);
				mesh.SetIndices(subIndices, MeshTopology.Lines, 0);

				if (vertices.Count != 0)
				{
					sphere.SetActive(true);
				}
				else
				{
					sphere.SetActive(false);
				}
				sphere.transform.localPosition = vertices[subIndices.Last()];
			}
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (pdf.elements.Length != 3)
			{
				// error
			}

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				elements[i] = pdf.elements[i];
			}

			CheckActiveElements();

			if (activeElements.Count > 0)
			{
				int index = activeElements[0];
				for (int n = 0; n < 3; n++)
				{
					p0[n] = elements[index].coords[n][(int)((float)dims[n] / 2f)];
				}
				p0[0] = elements[index].coords[0][(int)((float)dims[0] / 4f)];
				p0[1] = elements[index].coords[1][(int)((float)dims[1] / 2f)];
				p0[2] = elements[index].coords[2][(int)((float)dims[2] / 2f)];
			}

			step = 0;
		}

		public override void SetParameters()
		{
			sphere.transform.localScale = new Vector3(sphereScale, sphereScale, sphereScale);
		}

		public override void ResetUI()
		{
		}

		private void CheckActiveElements()
		{
			// create a list of active elements
			activeElements = new List<int>();
			for (int i = 0; i < 3; i++)
			{
				if (elements[i].isActive)
				{
					activeElements.Add(i);
				}
			}

			// get variables in the first active element
			if (activeElements.Count > 0)
			{
				int ae0 = activeElements[0];
				for (int n = 0; n < 3; n++)
				{
					dims[n] = elements[ae0].dims[n];
				}
				min      = elements[ae0].min;
				max      = elements[ae0].max;
				undef    = elements[ae0].undef;
				useUndef = elements[ae0].useUndef;

				CheckRange();
			}
		}

		private void CheckRange()
		{
			magMin = float.MaxValue;
			magMax = float.MinValue;
			Vector3 vec3 = new Vector3();
			int size = dims[0] * dims[1] * dims[2];
			for (int i = 0; i < size; i++)
			{
				bool IsUndef = false;
				for (int n = 0; n < 3; n++)
				{
					vec3[n] = 0; // initialize by zero

					if (elements[n].isActive)
					{
						float value = elements[n].values[i];
						if (useUndef && (value == undef))
						{
							IsUndef = true;
						}
						else
						{
							vec3[n] = elements[n].values[i];
						}
					}
				}
				if (!IsUndef)
				{
					magMin = Math.Min(magMin, vec3.magnitude);
					magMax = Math.Max(magMax, vec3.magnitude);
				}
			}
		}

		public void SetAnimationState(bool state)
		{
			IsAnimation = state;
		}

		private Vector3 GetVector(Vector3 position)
		{
			// for uniform and rectilinear
			DataElement activeElement = pdf.elements[activeElements[0]];

			// find indices
			int[] idx = new int[3];
			for (int n = 0; n < 3; n++)
			{

				float[] coord = activeElement.coords[n];
				int size = activeElement.dims[n];
				if (coord.First() < coord.Last())
				{
					for (int i = 0; i < size - 1; i++)
					{
						if (coord[i + 1] >= position[n])
						{
							idx[n] = i;
							break;
						}
					}
				}
				else
				{
					for (int i = 0; i < size - 1; i++)
					{
						if (coord[i + 1] <= position[n])
						{
							idx[n] = i;
							break;
						}
					}
				}
			}

			int   xa, xb, ya, yb, za, zb;
			float x0, x1, y0, y1, z0, z1;
			float v0, v1, v2, v3, v4, v5, v6, v7;
			float p, q, r;

			xa = idx[0];
			if (xa == dims[0] - 1)
			{
				xb = xa;
			}
			else
			{
				xb = xa + 1;
			}
			ya = idx[1];
			if (ya == dims[1] - 1)
			{
				yb = ya;
			}
			else
			{
				yb = ya + 1;
			}
			if (dims[2] == 1)
			{
				za = idx[2];
				zb = za;
			}
			else
			{
				za = idx[2];
				if (za == dims[2] - 1)
				{
					zb = za;
				}
				else
				{
					zb = za + 1;
				}
			}

			x0 = activeElement.coords[0][xa];
			x1 = activeElement.coords[0][xb];
			y0 = activeElement.coords[1][ya];
			y1 = activeElement.coords[1][yb];
			z0 = activeElement.coords[2][za];
			z1 = activeElement.coords[2][zb];

			float[] vec = new float[3];
			for (int n = 0; n < 3; n++)
			{
				vec[n] = 0; // initialization by zero

				if (elements[n].isActive)
				{
					int sx = dims[0];
					int sy = dims[1];
					int sz = dims[2];

					var values = elements[n].values;
					v0 = values[(za * sx * sy) + (ya * sx) + xa];
					v1 = values[(za * sx * sy) + (ya * sx) + xb];
					v2 = values[(zb * sx * sy) + (ya * sx) + xa];
					v3 = values[(zb * sx * sy) + (ya * sx) + xb];
					v4 = values[(za * sx * sy) + (yb * sx) + xa];
					v5 = values[(za * sx * sy) + (yb * sx) + xb];
					v6 = values[(zb * sx * sy) + (yb * sx) + xa];
					v7 = values[(zb * sx * sy) + (yb * sx) + xb];

					float ansf = 0f;

					bool isUndef = false;
					if (useUndef)
					{
						if ((v0 == undef) || (v1 == undef) || (v2 == undef) || (v3 == undef) ||
							(v4 == undef) || (v5 == undef) || (v6 == undef) || (v7 == undef))
						{
							isUndef = true;
						}
					}

					if (isUndef)
					{
						ansf = undef;
					}
					else
					{
						p = q = r = 0f;
						if (x0 != x1)
						{
							p = (position[0] - x0) / (x1 - x0);
						}
						if (y0 != y1)
						{
							q = (position[1] - y0) / (y1 - y0);
						}
						if (z0 != z1)
						{
							r = (position[2] - z0) / (z1 - z0);
						}
						double ans = 0.0;
						ans += v0 * (1 - p) * (1 - q) * (1 - r);
						ans += v1 * p * (1 - q) * (1 - r);
						ans += v2 * (1 - p) * (1 - q) * r;
						ans += v3 * p * (1 - q) * r;
						ans += v4 * (1 - p) * q * (1 - r);
						ans += v5 * p * q * (1 - r);
						ans += v6 * (1 - p) * q * r;
						ans += v7 * p * q * r;
						ansf = (float)ans;
					}

					if (ansf == undef)
					{
						vec[n] = undef;
					}
					else
					{
						vec[n] = Mathf.Clamp(ansf, min, max);
					}
				}
			}

			return new Vector3(vec[0], vec[1], vec[2]);
		}

		private void RungeKutta()
		{
			vertices.Clear();
			colors.Clear();
			indices.Clear();

			Vector3 k1, k2, k3, k4;
			Vector3 position, deltaPosition;
			Vector3 prev_position;

			position = new Vector3(p0[0], p0[1], p0[2]);
			prev_position = new Vector3(p0[0], p0[1], p0[2]);

			for (int i = 0; i < maxStep; i++)
			{
				if (!JudgeInsideOrOutside(position))
				{
					Debug.Log("breaked at " + i);
					break;
				}

				k1 = h * GetVector(position).normalized;
				k2 = h * GetVector(position + k1 / 2f).normalized;
				k3 = h * GetVector(position + k2 / 2f).normalized;
				k4 = h * GetVector(position + k3).normalized;

				deltaPosition = (k1 + 2f * k2 + 2f * k3 + k4) / 6f;
				if (deltaPosition.magnitude == 0)
				{
					break;
				}

				position += deltaPosition;

				if (UseMagnitude)
				{
					var vector = GetVector(position);
					magnitude = vector.magnitude;
					var level = Mathf.Clamp((magnitude - magMin) / (magMax - magMin), 0, 1f);
					color = GetColor(level);
				}

				vertices.Add(position);
				colors.Add(color);
			}

			for (int i = 0; i < vertices.Count - 1; i++)
			{
				indices.Add(i);
				indices.Add(i + 1);
			}

			Debug.Log("vertices.Count = " + vertices.Count);
			Debug.Log("colors.Count = " + colors.Count);
			Debug.Log("indices.Count = " + indices.Count);

			var lines = indices.ToArray();
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetIndices(lines, MeshTopology.Lines, 0);
			mesh.RecalculateBounds();
		}

		Color GetColor(float level)
		{
//			level += shift;
//			if (level > 1f) level -= 1f;
			Color c = Color.HSVToRGB(level, 1f, 1f);
			return new Color(c.r, c.g, c.b, 1f);
		}

		public void SetColor(Color _color)
		{
			color = _color;

			ParameterChanged();
		}

		private bool JudgeInsideOrOutside(Vector3 position)
		{
			// for uniform and rectilinear
			DataElement activeElement = pdf.elements[activeElements[0]];

			bool[] judge = new bool[3];
//			float[] coord;
			for (int i = 0; i < 3; i++)
			{
				judge[i] = false;
//				coord = activeElement.coords[i];

				float min = activeElement.coords[i][0];
				float max = activeElement.coords[i][dims[i] - 1];
				if (min > max)
				{
					float _max = min;
					min = max;
					max = _max;
				}
				if ((min <= position[i]) && (max >= position[i]))
				{
					judge[i] = true;
				}
			}

			bool final_judge = judge[0] & judge[1] & judge[2];

			return final_judge;
		}
	}
}