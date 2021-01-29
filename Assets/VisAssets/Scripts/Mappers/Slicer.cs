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
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Slicer))]
	public class SlicerEditor : Editor
	{
		SerializedProperty mode;
		SerializedProperty axis;
		SerializedProperty slice;
		SerializedProperty shift;
		int   previousAxis = 0;
		float previousSlice = 0;

		private void OnEnable()
		{
			mode  = serializedObject.FindProperty("filterMode");
			axis  = serializedObject.FindProperty("axis");
			slice = serializedObject.FindProperty("slice");
			shift = serializedObject.FindProperty("shift");
		}

		public override void OnInspectorGUI()
		{
			var slicer = target as Slicer;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			var label = new GUIContent("Filter Mode:");
			EditorGUILayout.PropertyField(mode, label, true);
			GUILayout.Space(5f);
			var currentAxis  = EditorGUILayout.IntSlider("Axis: ", axis.intValue, 0, 2);
			GUILayout.Space(3f);
			var currentSlice = EditorGUILayout.Slider("Slice: ", slice.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var currentShift = EditorGUILayout.Slider("Color Shift: ", shift.floatValue, 0, 1f);
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Slicer");
				if (currentAxis != previousAxis)
				{
					slicer.SetAxis(currentAxis);
					previousAxis = currentAxis;
				}
				else if (currentSlice != previousSlice)
				{
					slicer.SetSlice(currentSlice);
					previousSlice = currentSlice;
				}
				else
				{
					slicer.SetColorShift(currentShift);
				}
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

//	[DisallowMultipleComponent]
//	[System.Serializable]
	public class Slicer : MapperModuleTemplate
	{
		public class Slice
		{
			public float slider = 0;
			public float value = 0;
			public float min = 0;
			public float max = 1f;
		};

		public enum FILTER_MODE
		{
			TRILINEAR,
			BILINEAR,
			POINT
		};

		[SerializeField, Range(0, 2)]
		public int axis;
		[SerializeField, Range(0, 1f)]
		public float slice;
		[SerializeField]
		public float value;
//		[SerializeField, ReadOnly]
		[SerializeField]
		public float min, max;
		[SerializeField]
		public FILTER_MODE filterMode;
		[SerializeField, Range(0, 1f)]
		public float shift; // shift hue value to calculate RGB color
		[SerializeField]
		public int[] dims;
		[SerializeField]
		public DataElement element;
		Mesh mesh;

		[SerializeField]
		public Slice[] slices;
		int   prev_axis;
		float prev_slice;
		float prev_value;

		List<Vector3> vertices;
		List<Vector3> normals;
		List<Color>   colors;
		List<int>     triangles;
		List<Vector2> texture_uv;
		Material      material;

		Texture2D texture;
		Color[]   texcolor;
		Texture3D texture3d;
		Color[]   texcolor3d;

		public int idx = 0;
		public int tri_idx = 0;
		public int idx0 = 0;
		public int idx1 = 0;
		public float ratio, ratio2;

		public override void InitModule()
		{
			vertices   = new List<Vector3>();
			normals    = new List<Vector3>();
			colors     = new List<Color>();
			triangles  = new List<int>();
			texture_uv = new List<Vector2>();

			// 1. open [Edit]-[Project Settings]-[Graphics]-[Built-in Shader Settings]
			// 2. increase value of "Size" in [Always Included Shaders]
			// 3. set a shader (e.g. "Unlit/SliceShader") to last of elements
			material = new Material(Shader.Find("Unlit/SliceShader"));

			filterMode = FILTER_MODE.TRILINEAR;
			axis  = prev_axis  = 0;
			slice = prev_slice = 0;
			value = prev_value = 0;
			min = 0;
			max = 1f;

			slices = new Slice[3];
			for (int i = 0; i < 3; i++)
			{
				slices[i] = new Slice();
			}

			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			var meshFilter = GetComponent<MeshFilter>();
			meshFilter.mesh = mesh;
			meshFilter.hideFlags = HideFlags.HideInInspector;
			var meshRenderer = GetComponent<MeshRenderer>();
			meshRenderer.material = material;
			meshRenderer.hideFlags = HideFlags.HideInInspector;
		}

		public override int BodyFunc()
		{
			Calc();

			return 1;
		}

		public override void ReSetParameters()
		{
			// for safety
			if (slices == null)
			{
				slices = new Slice[3];
				for (int i = 0; i < 3; i++)
				{
					slices[i] = new Slice();
				}
			}

			element = pdf.elements[0];
			dims = element.dims;
			for (int i = 0; i < 3; i++)
			{
				slices[i].slider = 0;
				if (element.fieldType == FieldType.RECTILINEAR)
				{
					float first = element.coords[i].First();
					float last  = element.coords[i].Last();
					if (first < last)
					{
						slices[i].min = first;
						slices[i].max = last;
					}
					else
					{
						slices[i].min = last;
						slices[i].max = first;
					}
				}
				else if ((element.fieldType == FieldType.UNIFORM) ||
						 (element.fieldType == FieldType.IRREGULAR))
				{
					slices[i].min = 0;
					slices[i].max = (float)element.dims[i] - 1;
				}
				else
				{
					// not implemented yet
					// element.fieldType == FieldType.UNSTRUCTURE
				}
				slices[i].value = slices[i].min;
			}

//			axis  = prev_axis  = 0;
//			slice = prev_slice = 0;
//			value = prev_value = slices[axis].value;
			min   = slices[axis].min;
			max   = slices[axis].max;
			value = prev_value = min + (max - min) * slice;
		}

		public override void SetParameters()
		{
		}

//		public override void GetParameters()
//		{
//		}

		// override from template class
		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

			// for safety
			if (slices == null)
			{
				slices = new Slice[3];
				for (int i = 0; i < 3; i++)
				{
					slices[i] = new Slice();
				}
			}

			if (axis != prev_axis)
			{
				// backup current variables
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = min;
				slices[prev_axis].max    = max;

				// load variables to restore updated axis information
				slice = slices[axis].slider;
				value = slices[axis].value;
				min   = slices[axis].min;
				max   = slices[axis].max;
				prev_slice = slice;
				prev_value = value;
				prev_axis  = axis;
			}
			else
			{
				if (value != prev_value)
				{
					var _value = Mathf.Clamp(value, min, max);
					slice = (_value - min) / (max - min);
					prev_value = _value;
				}

				if (slice != prev_slice)
				{
					value = min + (max - min) * slice;
					prev_slice = slice;
				}
			}

			activation.SetParameterChanged(1);
		}

		public override void ResetUI()
		{
		}

		Color GetColor(float level)
		{
			level += shift;
			if (level > 1f) level -= 1f;
			Color c = Color.HSVToRGB(level, 1f, 1f);
			return new Color(c.r, c.g, c.b, 1f);
		}

		public void SetData()
		{
/*
			texture3d = new Texture3D(
				element.dims[0], element.dims[1], element.dims[2], TextureFormat.RGBA32, true);
			texcolor3d = new Color[element.size];
			for (int i = 0; i < element.size; i++)
			{
				// case:(element.max == element.min) and undef are not implemented yet
				var value = element.values[i];
				var level = (value - element.min) / (element.max - element.min);
				texcolor3d[i] = GetColor(level);
			}
			texture3d.SetPixels(texcolor3d);
			texture3d.Apply();
			if (filterMode == FILTER_MODE.POINT)
			{
				texture3d.filterMode = FilterMode.Point;
			}
			else if (filterMode == FILTER_MODE.BILINEAR)
			{
				texture3d.filterMode = FilterMode.Bilinear;
			}
			else
			{
				texture3d.filterMode = FilterMode.Trilinear;
			}
			texture3d.wrapMode = TextureWrapMode.Clamp;
//			texture3d.anisoLevel = 1;
//			GetComponent<Renderer>().material.SetTexture("_Volume", texture3d);
//			GetComponent<Renderer>().material.SetTexture("_MainTex", texture3d);
*/
		}

		public void SetMode(int mode)
		{
			filterMode = (FILTER_MODE)mode;

			ParameterChanged();
		}

		public void SetAxis(int _axis)
		{
			if (_axis != prev_axis)
			{
				// backup current variables
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = min;
				slices[prev_axis].max    = max;

				// load variables to restore updated axis information
				slice = slices[_axis].slider;
				value = slices[_axis].value;
				min   = slices[_axis].min;
				max   = slices[_axis].max;

				prev_slice = slice;
				prev_value = value;
				prev_axis  = _axis;
				axis       = _axis;
			}

			activation.SetParameterChanged(1);
		}

		public void SetSlice(float _slice)
		{
			if (_slice != prev_slice)
			{
				value = min + (max - min) * _slice;

				prev_slice = _slice;
				slice      = _slice;
			}

			activation.SetParameterChanged(1);
		}

		public void SetColorShift(float _shift)
		{
			shift = _shift;

			activation.SetParameterChanged(1);
		}

		void GetIndexOfCuttingEdge()
		{
			// get a coordinate index for cutting edge
			idx = 0;
			ratio = 0;
			if (element.fieldType == FieldType.RECTILINEAR)
			{
				float[] coord = element.coords[axis];
				int size = element.dims[axis];
				if (coord.First() < coord.Last())
				{
					for (int i = 0; i < size - 1; i++)
					{
						if (coord[i + 1] >= value)
						{
							idx = i;
							ratio = (value - coord[i]) / (coord[i + 1] - coord[i]);
							if (ratio < 0)
							{
								ratio += 1f;
							}
							if (value == max)
							{
								ratio = 1f;
							}
							break;
						}
					}
				}
				else
				{
					for (int i = 0; i < size - 1; i++)
					{
						if (coord[i + 1] <= value)
						{
							idx = i;
							ratio = (value - coord[i]) / (coord[i + 1] - coord[i]);
							if (ratio < 0)
							{
								ratio += 1f;
							}
							if (value == max)
							{
								ratio = 0;
							}
							break;
						}
					}
				}
			}
			else if ((element.fieldType == FieldType.UNIFORM) ||
					 (element.fieldType == FieldType.IRREGULAR))
			{
				idx = (int)Mathf.Clamp(value, min, max - 1f);
				ratio = (value % 1);
				if (value == max)
				{
					ratio = 1f;
				}
			}
			else
			{
				// not implemented yet
				// element.fieldType == FieldType.UNSTRUCTURE
			}
		}

		public void Calc()
		{
			GetIndexOfCuttingEdge();

			vertices.Clear();
			normals.Clear();
			colors.Clear();
			triangles.Clear();
			texture_uv.Clear();

			int slice_w = 0; // width
			int slice_h = 0; // height
			int slice_d = 0; // depth
			if (axis == 0)
			{
				slice_w = element.dims[1];
				slice_h = element.dims[2];
				slice_d = element.dims[0];
			}
			else if (axis == 1)
			{
				slice_w = element.dims[0];
				slice_h = element.dims[2];
				slice_d = element.dims[1];
			}
			else
			{
				slice_w = element.dims[0];
				slice_h = element.dims[1];
				slice_d = element.dims[2];
			}

			texture  = new Texture2D(slice_w, slice_h, TextureFormat.RGBA32, true);
			texcolor = new Color[slice_w * slice_h];
			float[] values      = element.values;
			float   values_min  = element.min;
			float   values_max  = element.max;
			float   values_diff = values_max - values_min;
			float[] slicedata   = new float[slice_w * slice_h];

			for (int j = 0; j < slice_h; j++)
			{
				for (int i = 0; i < slice_w; i++)
				{
					idx0 = 0;
					idx1 = 0;
					if (axis == 0)
					{
						idx0 = slice_d * (slice_w * j + i) + idx;
						idx1 = idx0 + 1;
					}
					else if (axis == 1)
					{
						idx0 = slice_w * slice_d * j + i + slice_w * idx;
						idx1 = idx0 + slice_w;
					}
					else
					{
						idx0 = slice_w * slice_h * idx + slice_w * j + i;
						idx1 = idx0 + slice_w * slice_h;
					}
					float ansf = 0;
					if (slice_d == 1)
					{
						ansf = values[idx0];
					}
					else
					{
						bool is_undef = false;
						if (element.useUndef)
						{
							if ((values[idx0] == element.undef) ||
								(values[idx1] == element.undef))
							{
								ansf = element.undef;
								is_undef = true;
							}
						}
						if (!is_undef)
						{
							if (element.fieldType == FieldType.RECTILINEAR)
							{
								float[] coord = element.coords[axis];
								ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
							}
							else if ((element.fieldType == FieldType.UNIFORM) ||
									 (element.fieldType == FieldType.IRREGULAR))
							{
								if (value == min)
								{
									ansf = values[idx0];
								}
								else if (value == max)
								{
									ansf = values[idx1];
								}
								else
								{
									ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
								}
							}
							else
							{
								// not implemented yet
								// element.fieldType == FieldType.UNSTRUCTURE
							}
						}
					}
					if ((element.useUndef) && (ansf == element.undef))
					{
						texcolor[slice_w * j + i] = new Color(1f, 1f, 1f, 0);
					}
					else
					{
						var color = Mathf.Clamp((ansf - values_min) / values_diff, 0, 1f);
						texcolor[slice_w * j + i] = GetColor(color);
					}

					float v0, v1, v2;
					float[] coord3 = element.coords[3];

					if (slice_d == 1)
					{
						v0 = coord3[idx0 * 3 + 0];
						v1 = coord3[idx0 * 3 + 1];
						v2 = coord3[idx0 * 3 + 2];
						vertices.Add(new Vector3(v0, v1, v2));
					}
					else
					{
						v0 = coord3[idx0 * 3 + 0] + (coord3[idx1 * 3 + 0] - coord3[idx0 * 3 + 0]) * ratio;
						v1 = coord3[idx0 * 3 + 1] + (coord3[idx1 * 3 + 1] - coord3[idx0 * 3 + 1]) * ratio;
						v2 = coord3[idx0 * 3 + 2] + (coord3[idx1 * 3 + 2] - coord3[idx0 * 3 + 2]) * ratio;
						vertices.Add(new Vector3(v0, v1, v2));
					}
				}
			}
			texture.SetPixels(texcolor, 0);
			texture.Apply();

			if (filterMode == FILTER_MODE.POINT)
			{
				texture.filterMode = FilterMode.Point;
			}
			else if (filterMode == FILTER_MODE.BILINEAR)
			{
				texture.filterMode = FilterMode.Bilinear;
			}
			else
			{
				texture.filterMode = FilterMode.Trilinear;
			}
			texture.wrapMode = TextureWrapMode.Clamp;
//			texture.anisoLevel = 1;

			// set triangle indices
			tri_idx = 0;
			for (int j = 0; j < slice_h - 1; j++)
			{
				int j0 = j;
				int j1 = j + 1;
				for (int i = 0; i < slice_w - 1; i++)
				{
					int i0 = i;
					int i1 = i + 1;
					int v0 = slice_w * j0 + i0;
					int v1 = slice_w * j1 + i0;
					int v2 = slice_w * j0 + i1;
					int v3 = slice_w * j1 + i1;
					triangles.Add(v0);
					triangles.Add(v1);
					triangles.Add(v3);
					triangles.Add(v0);
					triangles.Add(v3);
					triangles.Add(v2);
					tri_idx += 2;
				}
			}

			// set texture_uv
			float du = 1f / (float)(slice_w - 1);
			float dv = 1f / (float)(slice_h - 1);
			for (int j = 0; j < slice_h; j++)
			{
				float v = dv * (float)j;
				for (int i = 0; i < slice_w; i++)
				{
					float u = du * (float)i;
					texture_uv.Add(new Vector2(u, v));
				}
			}

			GetComponent<MeshFilter>().sharedMesh = CreatePlane();
			GetComponent<Renderer>().material.mainTexture = texture;
			GetComponent<Renderer>().material.mainTexture.wrapMode = TextureWrapMode.Clamp;
		}

		Mesh CreatePlane()
		{
			mesh.SetVertices(vertices);
			mesh.SetUVs(0, texture_uv);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
//			mesh.hideFlags = HideFlags.HideAndDontSave;
			return mesh;
		}
	}
}