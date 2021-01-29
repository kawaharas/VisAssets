using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Downsize))]
	public class DownsizeEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var downsize = target as Downsize;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			bool IsVisible = false;
			if (downsize.activeElements.Count > 0)
			{
				IsVisible = true;
			}
			int[] idims = new int[3] { -1, -1, -1 };
			EditorGUI.BeginDisabledGroup(!IsVisible);
			GUILayout.Space(10f);
			for (int i = 0; i < downsize.idims_max.Length; i++)
			{
				string str = "I" + (char)('X' + i) + ": ";
				if (downsize.idims[i] > 1)
				{
					idims[i] = EditorGUILayout.IntSlider(str, downsize.idims[i], 2, downsize.idims_max[i]);
				}
				else
				{
					idims[i] = EditorGUILayout.IntSlider(str, downsize.idims[i], 1, downsize.idims_max[i]);
				}
				GUILayout.Space(3f);
			}
			EditorGUI.EndDisabledGroup();

			GUIContent label = new GUIContent("All input elements must have the same number of grids.");
			EditorGUILayout.BeginHorizontal(GUI.skin.box);
			GUILayout.Space(5f);
			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.alignment = TextAnchor.MiddleLeft;
			style.wordWrap = true;
			style.CalcSize(label);
			if (!IsVisible)
			{
				EditorGUILayout.LabelField(label, style);
			}
			else
			{
				EditorGUILayout.LabelField("", style);
			}
			GUILayout.Space(5f);
			EditorGUILayout.EndHorizontal();
			GUILayout.Space(3f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Downsize");
				downsize.SetDims(idims);
				EditorUtility.SetDirty(target);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class Downsize : FilterModuleTemplate
	{
		public int safetyValue = 50;

		DataElement[] elements; // data elements of parent gameobject
		int elements_num;
		List<float>[] icoords; // 0:x, 1:y, 2:z, 3:(x, y, z)
		public int[] idims; // dims of interpolated data

		public List<int> activeElements;
		public List<int> parentActiveElements;
		public int[] dims;
		public int[] idims_max; // maximum dims of interpolated data
		public bool  useUndef;
		public float undef;

		public override void InitModule()
		{
			dims      = new int[3] { -1, -1, -1 };
			idims_max = new int[3] { -1, -1, -1 };
			idims     = new int[3] { -1, -1, -1 };
			useUndef  = false;
			activeElements = new List<int>();
		}

		public override int BodyFunc()
		{
			if (activeElements.Count > 0)
			{
				InitCoords();
				InitData();
			}

			return 1;
		}

		public override void ReSetParameters() // runs when parent was updated
		{
			df.DisposeElements();
			elements = pdf.elements;
			elements_num = elements.Length;
			df.CreateElements(elements_num);
			for (int i = 0; i < elements_num; i++)
			{
				df.elements[i] = elements[i].Clone();
			}

			CheckActiveElements();
		}

		public override void SetParameters() // runs when parameters were updated
		{
		}

		public void SetDims(int[] ndims)
		{
			for (int i = 0; i < 3; i++)
			{
				idims[i] = Mathf.Clamp(ndims[i], 1, idims_max[i]);
			}

			ParameterChanged();
		}

		public override void ResetUI()
		{
			int[] dims = new int[3]; // for safety
			for (int i = 0; i < dims.Length; i++)
			{
				dims[i] = safetyValue;
			}

			Slider slider;
			slider = UIPanel.transform.Find("X/Slider").GetComponent<Slider>();
			slider.minValue = 1;
			slider.maxValue = dims[0];
			slider.value = dims[0];
			slider = UIPanel.transform.Find("Y/Slider").GetComponent<Slider>();
			slider.minValue = 1;
			slider.maxValue = dims[1];
			slider.value = dims[1];
			slider = UIPanel.transform.Find("Z/Slider").GetComponent<Slider>();
			slider.minValue = 1;
			slider.maxValue = dims[2];
			slider.value = dims[2];
		}

		private void CheckActiveElements()
		{
			// create a list of active elements in the parent module
			parentActiveElements = new List<int>();
			for (int i = 0; i < pdf.elements.Length; i++)
			{
				if (pdf.elements[i].isActive)
				{
					parentActiveElements.Add(i);
				}
			}

			// check if all active elements have the same dimension
			dims = new int[3] { -1, -1, -1 };
			bool check_result = true;
			if (parentActiveElements.Count > 0)
			{
				// get variables in the first active element
				int index = parentActiveElements[0];
				for (int i = 0; i < 3; i++)
				{
					dims[i] = df.elements[index].dims[i];
				}
				bool useUndef = df.elements[index].useUndef;
				float undef = df.elements[index].undef;

				// compare variables in the first active element 
				// with variables in other active elements
				for (int i = 1; i < parentActiveElements.Count; i++)
				{
					for (int n = 0; n < 3; n++)
					{
						if (dims[n] != df.elements[i].dims[n])
						{
							check_result = false;
						}
					}
					if (useUndef == df.elements[i].useUndef)
					{
						if (undef != df.elements[i].undef)
						{
							check_result = false;
						}
					}
					else
					{
						check_result = false;
					}
				}
				if (check_result)
				{
					for (int i = 0; i < parentActiveElements.Count; i++)
					{
						df.elements[parentActiveElements[i]].isActive = true;
					}
					for (int i = 0; i < 3; i++)
					{
						idims[i] = Math.Min(dims[i], safetyValue);
						idims_max[i] = idims[i];
					}
				}
				else
				{
					dims = new int[3] { -1, -1, -1 };
					idims_max = new int[3] { -1, -1, -1 };
				}
			}

			// create a list of "current" active elements
			activeElements.Clear();
			for (int i = 0; i < elements.Length; i++)
			{
				if (df.elements[i].isActive)
				{
					activeElements.Add(i);
				}
			}
		}

		private int GetIndex(int i, int j, int k, int element_id)
		{
			int mx = elements[element_id].dims[0];
			int my = elements[element_id].dims[1];
			int mz = elements[element_id].dims[2];
			int index = k * my * mx + j * mx + i;

			return index;
		}

		int GetIndex(int i, int j, int k)
		{
			int index = dims[1] * dims[0] * k + dims[0] * j + i;

			return index;
		}

		private void InitCoords()
		{
			DataElement activeElement = df.elements[activeElements[0]];
			FieldType fieldType = activeElement.fieldType;

			// for uniform and rectilinear
			float[] delta = new float[3] { 0, 0, 0 };
			float[] imin  = new float[3] { 0, 0, 0 };
			float[] imax  = new float[3] { 0, 0, 0 };

			if (fieldType == FieldType.UNIFORM)
			{
				for (int i = 0; i < 3; i++)
				{
					imin[i] = 0;
					imax[i] = (float)(activeElement.dims[i] - 1);
					if (idims[i] != 1)
					{
						delta[i] = (imax[i] - imin[i]) / (float)(idims[i] - 1);
					}
				}
			}
			else if (fieldType == FieldType.RECTILINEAR)
			{
				for (int i = 0; i < 3; i++)
				{
					imin[i] = activeElement.coords[i].First();
					imax[i] = activeElement.coords[i].Last();
					if (idims[i] != 1)
					{
						delta[i] = (imax[i] - imin[i]) / (float)(idims[i] - 1);
					}
				}
			}
			else if (fieldType == FieldType.IRREGULAR)
			{
				// not implemented yet
			}
			else
			{
			}

			if ((fieldType == FieldType.UNIFORM) || (fieldType == FieldType.RECTILINEAR))
			{
				icoords = new List<float>[4];
				for (int i = 0; i < 4; i++)
				{
					icoords[i] = new List<float>();
				}
				// set new coords in the 1st to 3rd lists
				for (int j = 0; j < 3; j++)
				{
					for (int i = 0; i < idims[j]; i++)
					{
						icoords[j].Add(imin[j] + delta[j] * i);
					}
				}
				// merge coords to 4th list
				for (int k = 0; k < idims[2]; k++)
				{
					for (int j = 0; j < idims[1]; j++)
					{
						for (int i = 0; i < idims[0]; i++)
						{
							icoords[3].Add(icoords[0][i]);
							icoords[3].Add(icoords[1][j]);
							icoords[3].Add(icoords[2][k]);
						}
					}
				}
			}
			else if (fieldType == FieldType.IRREGULAR)
			{
				// not implemented yet
			}
			else
			{
			}

			// set dims and coords to all active elements
			for (int i = 0; i < activeElements.Count; i++)
			{
				int idx = activeElements[i];
				df.elements[idx].SetDims(idims[0], idims[1], idims[2]);
				df.elements[idx].SetCoords(icoords);
			}
		}

		private void InitData()
		{
			int[][] idx = new int[3][];
			for (int n = 0; n < 3; n++)
			{
				idx[n] = new int[idims[n]];
			}

			DataElement activeElement = pdf.elements[activeElements[0]];
			for (int n = 0; n < 3; n++)
			{

				float[] coord = activeElement.coords[n];
				int size = activeElement.dims[n];
				if (coord.First() < coord.Last())
				{
					for (int j = 0; j < idims[n]; j++)
					{
						for (int i = 0; i < size - 1; i++)
						{
							if (coord[i + 1] >= icoords[n][j])
							{
								idx[n][j] = i;
								break;
							}
						}
					}
				}
				else
				{
					for (int j = 0; j < idims[n]; j++)
					{
						for (int i = 0; i < size - 1; i++)
						{
							if (coord[i + 1] <= icoords[n][j])
							{
								idx[n][j] = i;
								break;
							}
						}
					}
				}
			}

			int xa, xb, ya, yb, za, zb;
			float x0, x1, y0, y1, z0, z1;
			float v0, v1, v2, v3, v4, v5, v6, v7;
			float p, q, r;

			for (int n = 0; n < activeElements.Count; n++)
			{
				List<float> ivalues = new List<float>();
				int elements_id = activeElements[n];
				for (int k = 0; k < idims[2]; k++)
				{
					for (int j = 0; j < idims[1]; j++)
					{
						for (int i = 0; i < idims[0]; i++)
						{
							xa = idx[0][i];
							if (xa == elements[elements_id].dims[0] - 1)
							{
								xb = xa;
							}
							else
							{
								xb = xa + 1;
							}
							ya = idx[1][j];
							if (ya == elements[elements_id].dims[1] - 1)
							{
								yb = ya;
							}
							else
							{
								yb = ya + 1;
							}
							if (idims[2] == 1)
							{
								za = idx[2][k];
								zb = za;
							}
							else
							{
								za = idx[2][k];
								if (za == elements[elements_id].dims[2] - 1)
								{
									zb = za;
								}
								else
								{
									zb = za + 1;
								}
							}

							x0 = elements[elements_id].coords[0][xa];
							x1 = elements[elements_id].coords[0][xb];
							y0 = elements[elements_id].coords[1][ya];
							y1 = elements[elements_id].coords[1][yb];
							z0 = elements[elements_id].coords[2][za];
							z1 = elements[elements_id].coords[2][zb];

							int sx = elements[elements_id].dims[0];
							int sy = elements[elements_id].dims[1];
							int sz = elements[elements_id].dims[2];

							v0 = elements[elements_id].values[(za * sx * sy) + (ya * sx) + xa];
							v1 = elements[elements_id].values[(za * sx * sy) + (ya * sx) + xb];
							v2 = elements[elements_id].values[(zb * sx * sy) + (ya * sx) + xa];
							v3 = elements[elements_id].values[(zb * sx * sy) + (ya * sx) + xb];
							v4 = elements[elements_id].values[(za * sx * sy) + (yb * sx) + xa];
							v5 = elements[elements_id].values[(za * sx * sy) + (yb * sx) + xb];
							v6 = elements[elements_id].values[(zb * sx * sy) + (yb * sx) + xa];
							v7 = elements[elements_id].values[(zb * sx * sy) + (yb * sx) + xb];

							float min = elements[elements_id].min;
							float max = elements[elements_id].max;
							float undef = elements[elements_id].undef;
							float ansf = 0f;
							bool useUndef = elements[elements_id].useUndef;

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
									p = (icoords[0][i] - x0) / (x1 - x0);
								}
								if (y0 != y1)
								{
									q = (icoords[1][j] - y0) / (y1 - y0);
								}
								if (z0 != z1)
								{
									r = (icoords[2][k] - z0) / (z1 - z0);
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
								ivalues.Add(undef);
							}
							else
							{
								if (ansf < min)
								{
									ivalues.Add(min);
								}
								else if (ansf > max)
								{
									ivalues.Add(max);
								}
								else
								{
									ivalues.Add(ansf);
								}
							}
						}
					}
				}

				df.elements[elements_id].SetValues(ivalues);
			}
		}
	}
}
			