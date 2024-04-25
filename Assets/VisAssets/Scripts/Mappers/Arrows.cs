using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets.SciVis.Structured.Arrows
{
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CustomEditor(typeof(Arrows))]
	public class ArrowsEditor : Editor
	{
		SerializedProperty axis;
		SerializedProperty slice;
		SerializedProperty scale_weight;
		SerializedProperty normalize;
		int previousAxis = 0;
		float previousSlice = 0;

		private void OnEnable()
		{
			axis  = serializedObject.FindProperty("axis");
			slice = serializedObject.FindProperty("slice");
//			scale_weight = serializedObject.FindProperty("scale_weight");
			scale_weight = serializedObject.FindProperty("arrowscale");
			normalize = serializedObject.FindProperty("normalize");
		}

		public override void OnInspectorGUI()
		{
			var arrows = target as Arrows;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);
			var currentAxis = EditorGUILayout.IntSlider("Axis: ", axis.intValue, 0, 2);
			GUILayout.Space(3f);
			var currentSlice = EditorGUILayout.Slider("Slice: ", slice.floatValue, 0, 1f);
			GUILayout.Space(3f);
			scale_weight.floatValue = EditorGUILayout.Slider("Scale: ", scale_weight.floatValue, 0, 10f);
			GUILayout.Space(3f);
			normalize.boolValue = EditorGUILayout.ToggleLeft("Normalize", normalize.boolValue, GUILayout.MaxWidth(100.0f));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				if (currentAxis != previousAxis)
				{
					arrows.SetAxis(currentAxis);
					previousAxis = currentAxis;
				}
				else if (currentSlice != previousSlice)
				{
					arrows.SetSlice(currentSlice);
					previousSlice = currentSlice;
				}
				arrows.SetScale();
				arrows.Normalize();
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();

			base.DrawDefaultInspector();
		}
	}
#endif

	[DisallowMultipleComponent]
	[System.Serializable]
	public class Arrows : MapperModuleTemplate
	{
		public class Slice
		{
			public float slider = 0;
			public float value = 0;
			public float min = 0;
			public float max = 1f;
		};

		[SerializeField, Range(0, 10f)]
		public float scale;
		public float maxScale;
		public bool  normalize;

		[SerializeField, ReadOnly]
		public DataElement[] elements;
//		[SerializeField, ReadOnly]
//		public float min;
//		[SerializeField, ReadOnly]
//		public float max;
		[SerializeField, ReadOnly]
		public float average;
		[SerializeField, ReadOnly]
		public float variance;

		public GameObject arrowPrefab;
		GameObject[] arrows;
		[SerializeField]
		public float arrowscale;
		public float scale_weight = 0.9f;

		public List<int> activeElements;
		public int[] dims;
		public bool  useUndef;
		public float undef;

		public int   axis;
		public float slice;
		public float value;
		public float value_min;
		public float value_max;
		public Slice[] slices;
		int   prev_axis;
		float prev_slice;
		float prev_value;
//		int   idx;
		float ratio;
		List<Vector3> positions;
		List<Vector3> scales;
		List<Vector3> eularAngles;

		public override void InitModule()
		{
			dims = new int[3] { -1, -1, -1 };
			useUndef = false;

			scale = maxScale = 1f;
//			normalize = false;

			elements = new DataElement[3];

			InitSliceParams();

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				meshRenderer.hideFlags = HideFlags.HideInInspector;
			}
		}

		public override int BodyFunc()
		{
			if (activeElements.Count > 0)
			{
				CalcSlice();
			}

			return 1;
		}

		public override void ReSetParameters()
		{
			if (pdf.elements.Length != 3)
			{
				// error
			}

			// for safety
			if (slices == null)
			{
				slices = new Slice[3];
				for (int i = 0; i < 3; i++)
				{
					slices[i] = new Slice();
				}
			}

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				elements[i] = pdf.elements[i];
			}

			CheckActiveElements();

			if (activeElements.Count > 0)
			{
				ResetSliceParams();
				CheckData();
			}
		}

		public override void SetParameters()
		{
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

			ValidateSlice();

			ParameterChanged();
		}

		public override void ResetUI()
		{
		}

		private void CreateArrows(int arrowNum)
		{
			if (arrowNum <= 0) return;

			arrows = new GameObject[arrowNum];
			for (int i = 0; i < arrowNum; i++)
			{
				arrows[i] = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
				arrows[i].transform.SetParent(transform, false);
			}
		}

		private void DeleteArrows()
		{
			if (arrows == null) return;

			for (int i = 0; i < arrows.Length; i++)
			{
				if (arrows[i] != null)
				{
					Destroy(arrows[i]);
				}
			}
		}

		private void ResizeArrows(int size)
		{
			if (arrows == null) return;

			int current_size = arrows.Length;
			if (current_size < size)
			{
				Array.Resize(ref arrows, size);
				for (int i = current_size; i < size; i++)
				{
					arrows[i] = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
//					arrows[i].transform.parent = this.gameObject.transform;
//					arrows[i].transform.localScale = new Vector3(1f, 1f, 1f);
					var _scale = new Vector3(1f / pdf.scale.x, 1f / pdf.scale.y, 1f / pdf.scale.z);
					if (pdf.coordinateSystem == DataField.CoordinateSystem.RIGHT_HANDED)
					{
						_scale = new Vector3(1f / pdf.scale.x, 1f / pdf.scale.y, 1f / -pdf.scale.z);
					}
					arrows[i].transform.localScale = _scale;
					arrows[i].transform.SetParent(transform, false);
				}
			}
			else if (current_size > size)
			{
				for (int i = size - 1; i < current_size; i++)
				{
					if (arrows[i] != null)
					{
						Destroy(arrows[i]);
					}
				}
				Array.Resize(ref arrows, size);
			}
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
				int index = activeElements[0];
				for (int n = 0; n < 3; n++)
				{
					dims[n] = elements[index].dims[n];
				}
				useUndef = elements[index].useUndef;
				undef    = elements[index].undef;
			}
		}

		void CheckData()
		{
			// scalarization
			var valueList = new List<float>();
			for (int k = 0; k < dims[2]; k++)
			{
				int index0 = k * dims[1] * dims[0];
				for (int j = 0; j < dims[1]; j++)
				{
					int index1 = j * dims[0];
					for (int i = 0; i < dims[0]; i++)
					{
						int index = index0 + index1 + i;
						float sum_of_squares = 0f;
						float value = 0f;
						for (int n = 0; n < 3; n++)
						{
							if (elements[n].isActive)
							{
								value = elements[n].values[index];
								if (!((useUndef == true) && (value == undef)))
								{
									sum_of_squares += value * value;
								}
							}
						}
						if ((useUndef == true) && (value == undef))
						{
							valueList.Add(undef);
						}
						else
						{
							valueList.Add(Mathf.Sqrt(sum_of_squares));
						}
					}
				}
			}

			// check values for scaling
			float[] values = valueList.ToArray();
			IEnumerable<float> valid_values = values;
			if (useUndef)
			{
				valid_values = values.Where(n => n != undef);
			}

			average   = valid_values.Average();
//			var sum2  = valid_values.Sum(a => (a - average) * (a - average));
			var sum2  = valid_values.Sum(a => a * a);
			var count = valid_values.Count();
			variance  = sum2 / count - average * average;

			if ((elements[activeElements[0]].fieldType == FieldType.UNIFORM) ||
				(elements[activeElements[0]].fieldType == FieldType.RECTILINEAR))
			{
				// for rectilinear coordinate
				float dmin = float.MaxValue;
				for (int n = 0; n < 3; n++)
				{
					float[] coord = elements[activeElements[0]].coords[n];
					for (int i = 0; i < dims[n] - 1; i++)
					{
						dmin = Math.Min(dmin, Math.Abs(coord[i + 1] - coord[i]));
					}
				}
//				arrowscale = dmin / max * scale_weight;
				arrowscale = dmin / valid_values.Max() * scale_weight;
			}
			else if (elements[activeElements[0]].fieldType == FieldType.IRREGULAR)
			{
				float[] coord = elements[activeElements[0]].coords[3];
				float dmin = float.MaxValue;

				for (int k = 0; k < dims[2] - 1; k++)
				{
				for (int j = 0; j < dims[1] - 1; j++)
				{
				for (int i = 0; i < dims[0] - 1; i++)
				{
					int i0 = i;
					int i1 = i + 1;
					int j0 = j;
					int j1 = j + 1;
					int k0 = k;
					int k1 = k + 1;

					int idx000 = dims[1] * dims[0] * k0 + dims[0] * j0 + i0;
					int idx100 = dims[1] * dims[0] * k0 + dims[0] * j0 + i1;
					int idx010 = dims[1] * dims[0] * k0 + dims[0] * j1 + i0;
					int idx001 = dims[1] * dims[0] * k1 + dims[0] * j0 + i0;
					float x000 = coord[idx000 * 3 + 0];
					float y000 = coord[idx000 * 3 + 1];
					float z000 = coord[idx000 * 3 + 2];
					float x100 = coord[idx100 * 3 + 0];
					float y100 = coord[idx100 * 3 + 1];
					float z100 = coord[idx100 * 3 + 2];
					float x010 = coord[idx010 * 3 + 0];
					float y010 = coord[idx010 * 3 + 1];
					float z010 = coord[idx010 * 3 + 2];
					float x001 = coord[idx001 * 3 + 0];
					float y001 = coord[idx001 * 3 + 1];
					float z001 = coord[idx001 * 3 + 2];
					var v000 = new Vector3(x000, y000, z000);
					var v100 = new Vector3(x100, y100, z100);
					var v010 = new Vector3(x010, y010, z010);
					var v001 = new Vector3(x001, y001, z001);

					dmin = Math.Min(dmin, Vector3.Distance(v000, v100));
					dmin = Math.Min(dmin, Vector3.Distance(v000, v010));
					dmin = Math.Min(dmin, Vector3.Distance(v000, v001));
				}
				}
				}
				arrowscale = dmin / valid_values.Max() * scale_weight;
//				arrowscale = 2f;
			}
		}

		void InitSliceParams()
		{
			positions   = new List<Vector3>();
			scales      = new List<Vector3>();
			eularAngles = new List<Vector3>();

			axis  = prev_axis  = 0;
			slice = prev_slice = 0;
			value = prev_value = 0;
			value_min = 0;
			value_max = 1f;

			slices = new Slice[3];
			for (int i = 0; i < 3; i++)
			{
				slices[i] = new Slice();
			}
		}

		void ResetSliceParams()
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

			DataElement element = pdf.elements[activeElements[0]];
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
			value_min = slices[axis].min;
			value_max = slices[axis].max;
			value = prev_value = value_min + (value_max - value_min) * slice;
		}

		void ValidateSlice()
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

			if (axis != prev_axis)
			{
				// backup current variables
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = value_min;
				slices[prev_axis].max    = value_max;

				// load variables to restore updated axis information
				slice     = slices[axis].slider;
				value     = slices[axis].value;
				value_min = slices[axis].min;
				value_max = slices[axis].max;

				prev_slice = slice;
				prev_value = value;
				prev_axis  = axis;
			}
			else
			{
				if (value != prev_value)
				{
					var _value = Mathf.Clamp(value, value_min, value_max);
					slice = (_value - value_min) / (value_max - value_min);
					prev_value = _value;
				}

				if (slice != prev_slice)
				{
					value = value_min + (value_max - value_min) * slice;
					prev_slice = slice;
				}
			}
		}

		public void SetAxis(int _axis)
		{
			if (_axis != prev_axis)
			{
				// backup current variables
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = value_min;
				slices[prev_axis].max    = value_max;

				// load variables to restore updated axis information
				slice     = slices[_axis].slider;
				value     = slices[_axis].value;
				value_min = slices[_axis].min;
				value_max = slices[_axis].max;

				prev_slice = slice;
				prev_value = value;
				prev_axis  = _axis;
				axis       = _axis;
			}

			ParameterChanged();
		}

		public void SetSlice(float _slice)
		{
			if (_slice != prev_slice)
			{
				value = value_min + (value_max - value_min) * _slice;

				prev_slice = _slice;
				slice      = _slice;
			}

			ParameterChanged();
		}

		public void SetScale()
		{
			for (int i = 0; i < positions.Count; i++)
			{
				if (normalize)
				{
					arrows[i].transform.localScale = scales[i] * scale_weight;
				}
				else
				{
					var _scale = arrows[i].transform.localScale;
					_scale.x *= scale_weight;
					_scale.y *= scale_weight;
					_scale.z *= scale_weight;
//					arrows[i].transform.localScale = _scale;
				}
//				arrows[i].transform.localEulerAngles = eularAngle[i];
//				arrows[i].transform.localPosition = positions[i];
			}

			ParameterChanged();
		}

		public void Normalize()
		{
			ParameterChanged();
		}

		int GetIndexOfCuttingEdge()
		{
			// get a coordinate index for cutting edge
			DataElement element = pdf.elements[activeElements[0]];
			int idx = 0;
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
							if (value == value_max)
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
							if (value == value_max)
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
				idx = (int)Mathf.Clamp(value, value_min, value_max - 1f);
				ratio = (value % 1);
				if (value == value_max)
				{
					ratio = 1f;
				}
			}
			else
			{
				// not implemented yet
				// element.fieldType == DataElement.FieldType.UNSTRUCTURE
			}

			return idx;
		}

		public void CalcSlice()
		{
			DataElement element = pdf.elements[activeElements[0]];
			int idx = GetIndexOfCuttingEdge();

			positions.Clear();
			scales.Clear();
			eularAngles.Clear();
			DeleteArrows();

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

			float[] slicedata = new float[slice_w * slice_h * 3];
			float[] sum_of_squares = new float[slice_w * slice_h];
			for (int n = 0; n < 3; n++)
			{
				for (int j = 0; j < slice_h; j++)
				{
					for (int i = 0; i < slice_w; i++)
					{
						int idx0 = 0;
						int idx1 = 0;
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
						if (elements[n].isActive)
						{
							float[] values = elements[n].values;
							if (slice_d == 1)
							{
								ansf = values[idx0];
								if (useUndef && (ansf == undef))
								{
									ansf = 0;
								}
							}
							else
							{
								bool is_undef = false;
								if (elements[n].useUndef)
								{
									if ((values[idx0] == elements[n].undef) ||
										(values[idx1] == elements[n].undef))
									{
										ansf = 0;
										is_undef = true;
									}
								}
								if (!is_undef)
								{
									if (element.fieldType == FieldType.RECTILINEAR)
									{
										float[] coord = elements[n].coords[axis];
										ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
									}
									else if ((element.fieldType == FieldType.UNIFORM) ||
											 (element.fieldType == FieldType.IRREGULAR))
									{
										if (value == value_min)
										{
											ansf = values[idx0];
										}
										else if (value == value_max)
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
						}
						slicedata[(slice_w * j + i) * 3 + n] = ansf;
						sum_of_squares[slice_w * j + i] += ansf * ansf;

						if (n == activeElements[0])
						{
							float v0, v1, v2;
							float[] coord3 = element.coords[3];
							if (slice_d == 1)
							{
								v0 = coord3[idx0 * 3 + 0];
								v1 = coord3[idx0 * 3 + 1];
								v2 = coord3[idx0 * 3 + 2];
								positions.Add(new Vector3(v0, v1, v2));
							}
							else
							{
								v0 = coord3[idx0 * 3 + 0] + (coord3[idx1 * 3 + 0] - coord3[idx0 * 3 + 0]) * ratio;
								v1 = coord3[idx0 * 3 + 1] + (coord3[idx1 * 3 + 1] - coord3[idx0 * 3 + 1]) * ratio;
								v2 = coord3[idx0 * 3 + 2] + (coord3[idx1 * 3 + 2] - coord3[idx0 * 3 + 2]) * ratio;
								positions.Add(new Vector3(v0, v1, v2));
							}
						}
					}
				}
			}

			CreateArrows(positions.Count);

			// get scale to reset child object's scale
			var root = transform.root; // set transform of root game object
			var current = transform; // variable for check transform of current game object
			var upstreamScale = Vector3.one;
			while (root != current)
			{
				current = current.transform.parent;
				upstreamScale = Vector3.Scale(upstreamScale, current.transform.localScale);
			}
			var upstreamReciprocalScale = GetReciprocalVector3(upstreamScale);

			for (int i = 0; i < positions.Count; i++)
			{
				// calculate scale
				float s = (float)Math.Sqrt((double)sum_of_squares[i]) * arrowscale;
				scales.Add(new Vector3(s, s, s));

				// calculate eular angle
				var ux = slicedata[i * 3 + 0];
				var uy = slicedata[i * 3 + 1];
				var uz = slicedata[i * 3 + 2];
				var r2 = Mathf.Sqrt(ux * ux + uy * uy);
				var phi = Mathf.Atan2(uy, ux) / Mathf.PI * 180.0f;
				var tht = Mathf.Atan2(r2, uz) / Mathf.PI * 180.0f;
				var rotX = Quaternion.AngleAxis(   0, new Vector3(1, 0, 0));
				var rotY = Quaternion.AngleAxis( tht, new Vector3(0, 1, 0));
				var rotZ = Quaternion.AngleAxis(-phi, new Vector3(0, 0, 1));
				var quaternion = rotY * rotZ;
				eularAngles.Add(new Vector3(0f, 0f, phi + 270f));

				if (normalize)
				{
					arrows[i].transform.localScale = Vector3.Scale(scales[i].normalized, upstreamReciprocalScale) / 10f;
				}
				else
				{
					arrows[i].transform.localScale = Vector3.Scale(scales[i], upstreamReciprocalScale);
				}
				arrows[i].transform.localEulerAngles = eularAngles[i];
				arrows[i].transform.localPosition    = positions[i];
			}
		}

		Vector3 GetReciprocalVector3(Vector3 vec3)
		{
			var ret = new Vector3(1f / vec3.x, 1f / vec3.y, 1f / vec3.z);
			return ret;
		}
	}
}