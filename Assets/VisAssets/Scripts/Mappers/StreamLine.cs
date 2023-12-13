using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisAssets.SciVis.Structured.StreamLines
{
	public class StreamLine : MonoBehaviour
	{
		GameObject parent;
		public DataField pdf;
		public DataField df;
		bool dataLoaded;

		bool calcState;

		#region variables

		Mesh mesh;
		List<Vector3> vertices;
		List<Color>   colors;
		List<Color>   magColors; // colors based on magnitude
		List<int>     indices;
		public Color  color;
		Material      material;

		public Vector3 seed = new Vector3();

public int vertCount = 0;
public int colCount = 0;
public int idxCount = 0;
		public bool   IsAnimation;
		public bool   IsRepeat;
		public bool   UseMagnitude;
		public bool   IsCalc;
		public int    calculatedStep;
		public int    step;
		GameObject    sphere;
//		public float  sphereScale;
		public int    maxStep;

		[SerializeField, ReadOnly]
		DataElement[] elements;
		List<int> activeElements;
		int[] dims;
		float[][] coords;
		float min;
		float max;
		float undef;
		bool  useUndef;

		Vector3 boundMin;
		Vector3 boundMax;

//		float h = 2e-3f;
		float h = 5e-3f;
//		float h = 0.005f;
		float magMin;
		float magMax;
		float magnitude;

		Vector3 position;

		#endregion // variables

		// Start is called before the first frame update
		void Start()
		{
			dataLoaded = false;
			calcState = false;
			parent = transform.parent.gameObject;
			df = GetComponent<DataField>();

			vertices  = new List<Vector3>();
			colors    = new List<Color>();
			magColors = new List<Color>();
			indices   = new List<int>();
			color     = new Color(1f, 1f, 1f);
			material  = new Material(Shader.Find("Sprites/Default"));

			IsAnimation = false;
			IsRepeat    = false;
			step = 0;
			calculatedStep = 0;
			maxStep = 5000;

			elements = new DataElement[3];
			dims = new int[3] { -1, -1, -1 };
			useUndef = false;

			sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
			sphere.transform.SetParent(transform, false);
			sphere.hideFlags = HideFlags.HideInHierarchy;
			sphere.SetActive(false);

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

		// Update is called once per frame
		void FixedUpdate()
		{
			if (!IsCalc) return;

			if (dataLoaded)
			{
				RungeKutta();
/*
				if (!calcState)
				{
					RungeKutta();
					calcState = true;
				}
*/				
/*
				if (vertices.Count != 0)
				{
					sphere.SetActive(true);
					sphere.transform.localPosition = vertices[vertices.Count - 1];
				}
*/
				if (indices.Count > 0)
				{
					sphere.SetActive(true);
//					sphere.transform.localPosition = vertices[vertices.Count - 1];
					sphere.transform.localPosition = vertices[indices[indices.Count - 1]];

					mesh.SetVertices(vertices);
					if (UseMagnitude)
					{
						mesh.SetColors(magColors);
					}
					else
					{
						mesh.SetColors(colors);
					}
					mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
					mesh.RecalculateBounds();
					var filter = GetComponent<MeshFilter>();
					filter.mesh = mesh;
				}

				return;
			}

			pdf = parent.GetComponent<StreamLines>().pdf;

			if (pdf.dataLoaded)
			{
				for (int i = 0; i < pdf.elements.Length; i++)
				{
					elements[i] = pdf.elements[i];
				}

				CheckActiveElements();

				if (activeElements.Count > 0)
				{
					position = seed;

					dataLoaded = true;
				}
			}

/*
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
*/
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
			var activeElement = pdf.elements[activeElements[0]];

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
			if (calculatedStep >= maxStep) return;

			if (!JudgeInsideOrOutside(position)) return;

			var k1 = h * GetVector(position).normalized;
			var k2 = h * GetVector(position + k1 / 2f).normalized;
			var k3 = h * GetVector(position + k2 / 2f).normalized;
			var k4 = h * GetVector(position + k3).normalized;
			var deltaPosition = (k1 + 2f * k2 + 2f * k3 + k4) / 6f;

			if (deltaPosition.magnitude == 0)
			{
				IsCalc = false; // stop calculation
				return;
			}

			position += deltaPosition;

//			if (UseMagnitude)
			{
//				var vector = GetVector(position);
//				magnitude = vector.magnitude;
//				var vector = GetVector(position);
				var magnitude = GetVector(position).magnitude;
				var level = Mathf.Clamp((magnitude - magMin) / (magMax - magMin), 0, 1f);
//				var magColor = GetColor(level);
				magColors.Add(GetColor(level));
			}

			vertices.Add(position);
			colors.Add(color);
vertCount++;
colCount++;

			if (calculatedStep > 0)
			{
idxCount += 2;
				indices.Add(calculatedStep - 1);
				indices.Add(calculatedStep - 1 + 1);
/*
				mesh.SetVertices(vertices);
				if (UseMagnitude)
				{
					mesh.SetColors(magColors);
				}
				else
				{
					mesh.SetColors(colors);
				}
				mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
				mesh.RecalculateBounds();
*/
			}
			calculatedStep++;
		}

		Color GetColor(float level)
		{
			Color c = Color.HSVToRGB(level, 1f, 1f);
			return new Color(c.r, c.g, c.b, 1f);
		}

		public void SetColor(Color _color)
		{
			color = _color;
		}

		private bool JudgeInsideOrOutside(Vector3 position)
		{
			// for uniform and rectilinear
			var activeElement = pdf.elements[activeElements[0]];

			var judge = new bool[3];
//			float[] coord;

var min = activeElement.boundMin;
var max = activeElement.boundMax;

			for (int i = 0; i < 3; i++)
			{
				judge[i] = false;
				if ((min[i] <= position[i]) && (max[i] >= position[i]))
				{
					judge[i] = true;
				}
/*
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
*/
			}

			bool final_judge = judge[0] & judge[1] & judge[2];

			return final_judge;
		}

		public void SetSeed(Vector3 _seed)
		{
			IsCalc = true;
			seed = _seed;
		}
	}
}