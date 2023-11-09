using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisAssets
{
	[System.Serializable]
	public class DataElement
	{
		public int[]     dims;
		public int       ndim;
		public int       size;
		public int       steps;
		[System.NonSerialized]
		public float[][] coords;
		[System.NonSerialized]
		public float[]   values;
		public float     scale;
		public float     min;
		public float     max;
		public float     average;
		public float     variance;
		public string    varName;
		public bool      useUndef;
		public float     undef;
		public FieldType fieldType;
		public bool      isActive;

		public Vector3   boundMin;
		public Vector3   boundMax;

		public enum FieldType
		{
			UNIFORM,
			RECTILINEAR,
			IRREGULAR,
			UNSTRUCTURED,
			UNDEFINED
		}

		public DataElement()
		{
			dims      = new int[3] { -1, -1, -1 };
			steps     = 1;
			scale     = 1f;
			min       = float.MaxValue;
			max       = float.MinValue;
			varName   = string.Empty;
			useUndef  = false;
			fieldType = FieldType.UNDEFINED;
			isActive  = false;
		}

		public DataElement Clone()
		{
			return (DataElement)MemberwiseClone();
		}

		public void SetDims(Vector3Int d)
		{
			ndim = 3;
			size = 1;
			for (int i = 0; i < ndim; i++)
			{
				dims[i] = d[i];
				size *= dims[i];
			}
		}

		public void SetDims(List<int> d)
		{
			dims = d.ToArray();
			ndim = 3;
			size = 1;
			for (int i = 0; i < ndim; i++)
			{
				size *= dims[i];
			}
		}

		public void SetDims(int x, int y, int z)
		{
			dims = new int[3] { x, y, z };
			ndim = 3;
			size = 1;
			for (int i = 0; i < ndim; i++)
			{
				size *= dims[i];
			}
		}

		public void SetSteps(int t)
		{
			steps = t;
		}

		public void SetCoords(List<float>[] c)
		{
			coords = new float[4][];
			for (int i = 0; i < 4; i++)
			{
				coords[i] = c[i].ToArray();
			}

			// for safety
			if (coords[3].Length == 0)
			{
				// merge coordinates to 4th list
				for (int k = 0; k < dims[2]; k++)
				{
					for (int j = 0; j < dims[1]; j++)
					{
						for (int i = 0; i < dims[0]; i++)
						{
							c[3].Add(c[0][i]);
							c[3].Add(c[1][j]);
							c[3].Add(c[2][k]);
						}
					}
				}
				coords[3] = c[3].ToArray();
			}

			boundMin = GetCoord(0, 0, 0);
			boundMax = GetCoord(0, 0, 0);
			for (int k = 0; k < dims[2]; k++)
			{
				for (int j = 0; j < dims[1]; j++)
				{
					for (int i = 0; i < dims[0]; i++)
					{
						boundMin = Vector3.Min(boundMin, GetCoord(i, j, k));
						boundMax = Vector3.Max(boundMax, GetCoord(i, j, k));
					}
				}
			}
		}

		public void SetFieldType(FieldType type)
		{
			fieldType = type;
		}

		public void SetValues(List<float> v)
		{
			if (size == 0)
			{
				Debug.Log("Error in SetValues in DataElement. You must call SetDims in advance\n");
				return;
			}
			values = v.ToArray();

			if (min != float.MaxValue) return;

			IEnumerable<float> valid_values = values;
			if (useUndef)
			{
				valid_values = values.Where(n => n != undef);
			}

			min       = valid_values.Min();
			max       = valid_values.Max();
			average   = valid_values.Average();
			var sum2  = valid_values.Sum(a => (a - average) * (a - average));
			var count = valid_values.Count();
			variance  = sum2 / count - average * average;
		}

		public void SetValues(float[] v)
		{
			if (size == 0)
			{
				Debug.Log("Error in SetValues in DataElement. You must call SetDims in advance\n");
				return;
			}
//			values = v.ToArray();
			values = new float[v.Length];
			System.Array.Copy(v, values, v.Length);

			if (min != float.MaxValue) return;

			IEnumerable<float> valid_values = values;
			if (useUndef)
			{
				valid_values = values.Where(n => n != undef);
			}

			min = valid_values.Min();
			max = valid_values.Max();
			average = valid_values.Average();
			var sum2 = valid_values.Sum(a => (a - average) * (a - average));
			var count = valid_values.Count();
			variance = sum2 / count - average * average;
		}
/*
		public unsafe void SetValues(byte[] v)
		{
			if (size == 0)
			{
				Debug.Log("Error in SetValues in DataElement. You must call SetDims in advance\n");
				return;
			}
			//			values = v.ToArray();
			values = new float[size];
			System.Buffer.BlockCopy(v, 0, values, 0, v.Length);

			if (min != float.MaxValue) return;

			IEnumerable<float> valid_values = values;
			if (useUndef)
			{
				valid_values = values.Where(n => n != undef);
			}

			min = valid_values.Min();
			max = valid_values.Max();
			average = valid_values.Average();
			var sum2 = valid_values.Sum(a => (a - average) * (a - average));
			var count = valid_values.Count();
			variance = sum2 / count - average * average;
		}
*/
		public void SetVarName(string str)
		{
			varName = str.Replace("\\n", " ");
		}

		public void SetUndef(float v)
		{
			useUndef = true;
			undef    = v;
		}

		public void SetActive(bool state)
		{
			isActive = state;
		}

		public Vector3 GetCoord(int ix, int iy, int iz)
		{
			var idx = iz * dims[1] * dims[0] + iy * dims[0] + ix;
			var ox = coords[3][idx * 3 + 0];
			var oy = coords[3][idx * 3 + 1];
			var oz = coords[3][idx * 3 + 2];

			return new Vector3(ox, oy, oz);
		}
	}
}
