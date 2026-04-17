using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Downsize
{
	using ModuleState = Activation.ModuleState;
	using FieldType   = DataElement.FieldType;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Downsize))]
	public class DownsizeEditor : Editor
	{
		SerializedProperty targetDims, androidMaxDim;
		private bool pendingUpdate = false;

		/// <summary>
		/// Initializes serialized properties when the object is selected in the Inspector.
		/// </summary>
		private void OnEnable()
		{
			targetDims = serializedObject.FindProperty("targetDims");
			androidMaxDim = serializedObject.FindProperty("androidMaxDim");
		}

		/// <summary>
		/// Renders the custom Inspector GUI for the Downsize module.
		/// </summary>
		public override void OnInspectorGUI()
		{
			var downsize = target as Downsize;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			int maxX = 100; int maxY = 100; int maxZ = 100;

			// Dynamically determine the maximum allowed dimensions based on the loaded data field
			if (downsize.pdf != null && downsize.pdf.dataLoaded && downsize.pdf.elements != null)
			{
				maxX = 1; maxY = 1; maxZ = 1;
				foreach (var el in downsize.pdf.elements)
				{
					if (el != null && el.dims != null && el.dims.Length >= 3)
					{
						maxX = Mathf.Max(maxX, el.dims[0]);
						maxY = Mathf.Max(maxY, el.dims[1]);
						maxZ = Mathf.Max(maxZ, el.dims[2]);
					}
				}

#if UNITY_ANDROID
				maxX = Mathf.Min(maxX, downsize.androidMaxDim);
				maxY = Mathf.Min(maxY, downsize.androidMaxDim);
				maxZ = Mathf.Min(maxZ, downsize.androidMaxDim);
#endif
			}

			Vector3Int currentDims = targetDims.vector3IntValue;

			GUILayout.Space(5f);

			currentDims.x = EditorGUILayout.IntSlider("New Grid Size X", currentDims.x, 1, maxX);

			GUILayout.Space(5f);

			currentDims.y = EditorGUILayout.IntSlider("New Grid Size Y", currentDims.y, 1, maxY);

			GUILayout.Space(5f);

			currentDims.z = EditorGUILayout.IntSlider("New Grid Size Z", currentDims.z, 1, maxZ);

			GUILayout.Space(5f);

			targetDims.vector3IntValue = currentDims;

			// Disable modification of the Android Max Size Cap during runtime to prevent inconsistencies
			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			EditorGUILayout.PropertyField(androidMaxDim, new GUIContent("Android Max Size Cap"));
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Downsize");
				EditorUtility.SetDirty(target);

				if (EditorApplication.isPlaying)
				{
					pendingUpdate = true;
				}
			}

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			serializedObject.ApplyModifiedProperties();

			if (pendingUpdate && GUIUtility.hotControl == 0)
			{
				pendingUpdate = false;

				if (downsize.activation != null)
				{
					downsize.activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
				}
			}
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	[System.Serializable]
	public class Downsize : FilterModuleTemplate
	{
		[SerializeField]
		public Vector3Int targetDims = new Vector3Int(50, 50, 50);

		[Tooltip("Maximum grid size per axis for Android builds (prevents memory exhaustion).")]
		public int androidMaxDim = 100;

		[HideInInspector] public List<int> activeElements = new List<int>();

		// ==========================================================
		// uGUI Compatibility Properties & Methods
		// ==========================================================

		/// <summary>
		/// Compatibility property for external uGUI scripts (e.g., IPSlider, IPValue) to access and modify the target dimensions.
		/// </summary>
		public Vector3Int idims
		{
			get { return targetDims; }
			set { targetDims = value; }
		}

		// ==========================================================
		// Core Module Logic
		// ==========================================================

		/// <summary>
		/// Initializes module-specific internal variables.
		/// </summary>
		public override void InitModule()
		{
			activeElements = new List<int>();
		}

		/// <summary>
		/// The main execution function of the module that triggers the downsize processing.
		/// </summary>
		public override int BodyFunc()
		{
			if (activeElements.Count > 0)
			{
				for (int i = 0; i < activeElements.Count; i++)
				{
					ProcessElement(activeElements[i]);
				}
			}
			return 1;
		}

		/// <summary>
		/// Resets and prepares the local DataField elements based on the parent data field.
		/// </summary>
		public override void ReSetParameters()
		{
			df.DisposeElements();
			df.CreateElements(pdf.elements.Length);

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				df.elements[i] = pdf.elements[i].Clone();
			}
			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale = pdf.scale;
			df.offset = pdf.offset;

			CheckActiveElements();
		}

		/// <summary>
		/// Applies parameter changes from the UI. (Empty in this module as updates are handled dynamically)
		/// </summary>
		public override void SetParameters()
		{
		}

		/// <summary>
		/// Resets the associated uGUI components (sliders and input fields) to their default states based on the current data dimensions.
		/// </summary>
		public override void ResetUI()
		{
			if (UIPanel == null || pdf == null || pdf.elements == null || pdf.elements.Length == 0) return;

			int[] oDims = pdf.elements[0].dims;
			if (oDims == null || oDims.Length < 3) return;

			string[] axes = { "X", "Y", "Z" };
			int[] limits = { oDims[0], oDims[1], oDims[2] };
			int[] currentVals = { targetDims.x, targetDims.y, targetDims.z };

			for (int i = 0; i < 3; i++)
			{
				Transform sliderTransform = UIPanel.transform.Find($"{axes[i]}/Slider");
				if (sliderTransform != null)
				{
					Slider slider = sliderTransform.GetComponent<Slider>();
					if (slider != null)
					{
						slider.minValue = 1;
						slider.maxValue = limits[i];
						slider.value = currentVals[i];
					}
				}
			}
		}

		/// <summary>
		/// Updates the target dimensions from external uGUI components, clamps them to valid ranges, and triggers an update.
		/// </summary>
		public void SetDims(int[] ndims)
		{
			if (ndims == null || ndims.Length < 3) return;

			int maxX = int.MaxValue; int maxY = int.MaxValue; int maxZ = int.MaxValue;

			if (pdf != null && pdf.dataLoaded && pdf.elements != null && pdf.elements.Length > 0)
			{
				int[] oDims = pdf.elements[0].dims;
				if (oDims != null && oDims.Length >= 3)
				{
					maxX = oDims[0];
					maxY = oDims[1];
					maxZ = oDims[2];
				}
			}

			targetDims.x = Mathf.Clamp(ndims[0], 1, maxX);
			targetDims.y = Mathf.Clamp(ndims[1], 1, maxY);
			targetDims.z = Mathf.Clamp(ndims[2], 1, maxZ);

			if (activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Evaluates and caches which data elements are currently active and need processing.
		/// </summary>
		private void CheckActiveElements()
		{
			activeElements.Clear();

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				if (pdf.elements[i].isActive)
				{
					df.elements[i].isActive = true;
					activeElements.Add(i);
				}
				else
				{
					df.elements[i].isActive = false;
				}
			}
		}

		/// <summary>
		/// Core logic for downsizing a single data element using trilinear interpolation.
		/// Handles both rectilinear and irregular grids.
		/// </summary>
		private void ProcessElement(int index)
		{
			DataElement src = pdf.elements[index];
			DataElement dst = df.elements[index];

			int[] oDims = src.dims;
			int[] nDims = new int[3];

			int capX = targetDims.x; int capY = targetDims.y; int capZ = targetDims.z;
#if UNITY_ANDROID && !UNITY_EDITOR
			capX = Mathf.Min(capX, androidMaxDim);
			capY = Mathf.Min(capY, androidMaxDim);
			capZ = Mathf.Min(capZ, androidMaxDim);
#endif
			nDims[0] = Mathf.Clamp(capX, 1, oDims[0]);
			nDims[1] = Mathf.Clamp(capY, 1, oDims[1]);
			nDims[2] = Mathf.Clamp(capZ, 1, oDims[2]);

			dst.SetDims(nDims[0], nDims[1], nDims[2]);

			// Pre-allocate list capacities to completely eliminate dynamic resizing overhead during loops
			List<float>[] icoords = new List<float>[4];
			icoords[0] = new List<float>(nDims[0]);
			icoords[1] = new List<float>(nDims[1]);
			icoords[2] = new List<float>(nDims[2]);
			icoords[3] = new List<float>(nDims[0] * nDims[1] * nDims[2] * 3);

			List<float> newValues = new List<float>(nDims[0] * nDims[1] * nDims[2]);

			// ==========================================================
			// Branch 1: Rectilinear or Uniform Grid (Interpolation in physical space)
			// ==========================================================
			if (src.fieldType == FieldType.RECTILINEAR || src.fieldType == FieldType.UNIFORM)
			{
				for (int i = 0; i < 3; i++)
				{
					float imin = src.coords[i].First();
					float imax = src.coords[i].Last();
					float delta = (nDims[i] > 1) ? (imax - imin) / (nDims[i] - 1) : 0;

					for (int j = 0; j < nDims[i]; j++)
					{
						icoords[i].Add(imin + delta * j);
					}
				}

				for (int k = 0; k < nDims[2]; k++)
				{
					for (int j = 0; j < nDims[1]; j++)
					{
						for (int i = 0; i < nDims[0]; i++)
						{
							icoords[3].Add(icoords[0][i]);
							icoords[3].Add(icoords[1][j]);
							icoords[3].Add(icoords[2][k]);
						}
					}
				}

				int[][] idx = new int[3][];
				for (int n = 0; n < 3; n++)
				{
					idx[n] = new int[nDims[n]];
					float[] coord = src.coords[n].ToArray();
					bool ascending = coord.First() < coord.Last();

					for (int j = 0; j < nDims[n]; j++)
					{
						idx[n][j] = Mathf.Max(0, oDims[n] - 2);

						for (int i = 0; i < oDims[n] - 1; i++)
						{
							if (ascending ? coord[i + 1] >= icoords[n][j] : coord[i + 1] <= icoords[n][j])
							{
								idx[n][j] = i;
								break;
							}
						}
					}
				}

				for (int k = 0; k < nDims[2]; k++)
				{
					for (int j = 0; j < nDims[1]; j++)
					{
						for (int i = 0; i < nDims[0]; i++)
						{
							int xa = idx[0][i];
							int xb = (xa == oDims[0] - 1) ? xa : xa + 1;
							int ya = idx[1][j];
							int yb = (ya == oDims[1] - 1) ? ya : ya + 1;
							int za = idx[2][k];
							int zb = (oDims[2] == 1 || za == oDims[2] - 1) ? za : za + 1;

							float x0 = src.coords[0][xa];
							float x1 = src.coords[0][xb];
							float y0 = src.coords[1][ya];
							float y1 = src.coords[1][yb];
							float z0 = src.coords[2][za];
							float z1 = src.coords[2][zb];

							int sx = oDims[0]; int sy = oDims[1];
							float v0 = src.values[(za * sx * sy) + (ya * sx) + xa];
							float v1 = src.values[(za * sx * sy) + (ya * sx) + xb];
							float v2 = src.values[(zb * sx * sy) + (ya * sx) + xa];
							float v3 = src.values[(zb * sx * sy) + (ya * sx) + xb];
							float v4 = src.values[(za * sx * sy) + (yb * sx) + xa];
							float v5 = src.values[(za * sx * sy) + (yb * sx) + xb];
							float v6 = src.values[(zb * sx * sy) + (yb * sx) + xa];
							float v7 = src.values[(zb * sx * sy) + (yb * sx) + xb];

							float p = 0, q = 0, r = 0;

							if (x0 != x1) p = (icoords[0][i] - x0) / (x1 - x0);
							if (y0 != y1) q = (icoords[1][j] - y0) / (y1 - y0);
							if (z0 != z1) r = (icoords[2][k] - z0) / (z1 - z0);

							bool isUndef = false; float undef = src.undef;
							if (src.useUndef && (v0 == undef || v1 == undef || v2 == undef || v3 == undef ||
								v4 == undef || v5 == undef || v6 == undef || v7 == undef))
							{
								isUndef = true;
							}

							if (isUndef)
							{
								newValues.Add(undef);
							}
							else
							{
								double ans =
									v0 * (1 - p) * (1 - q) * (1 - r) +
									v1 *      p  * (1 - q) * (1 - r) +
									v2 * (1 - p) * (1 - q) *      r  +
									v3 *      p  * (1 - q) *      r  +
									v4 * (1 - p) *      q  * (1 - r) +
									v5 *      p  *      q  * (1 - r) +
									v6 * (1 - p) *      q  *      r  +
									v7 *      p  *      q  *      r;

								newValues.Add(Mathf.Clamp((float)ans, src.min, src.max));
							}
						}
					}
				}
			}
			// ==========================================================
			// Branch 2: Irregular / Curvilinear Grid (Interpolation in topological space)
			// ==========================================================
			else if (src.fieldType == FieldType.IRREGULAR)
			{
				// Ratio (step size) of original array size to new array size for I, J, and K axes
				float ratioX = (nDims[0] > 1) ? (float)(oDims[0] - 1) / (nDims[0] - 1) : 0;
				float ratioY = (nDims[1] > 1) ? (float)(oDims[1] - 1) / (nDims[1] - 1) : 0;
				float ratioZ = (nDims[2] > 1) ? (float)(oDims[2] - 1) / (nDims[2] - 1) : 0;

				int sx = oDims[0]; int sy = oDims[1];

				for (int k = 0; k < nDims[2]; k++)
				{
					float srcZ = k * ratioZ; int za = (int)Mathf.Floor(srcZ); int zb = Mathf.Min(za + 1, oDims[2] - 1); float r = srcZ - za;

					for (int j = 0; j < nDims[1]; j++)
					{
						float srcY = j * ratioY; int ya = (int)Mathf.Floor(srcY); int yb = Mathf.Min(ya + 1, oDims[1] - 1); float q = srcY - ya;

						for (int i = 0; i < nDims[0]; i++)
						{
							float srcX = i * ratioX;
							int xa = (int)Mathf.Floor(srcX);
							int xb = Mathf.Min(xa + 1, oDims[0] - 1);
							float p = srcX - xa;

							// Calculate the indices of the 8 surrounding vertices
							int idx0 = (za * sx * sy) + (ya * sx) + xa;
							int idx1 = (za * sx * sy) + (ya * sx) + xb;
							int idx2 = (zb * sx * sy) + (ya * sx) + xa;
							int idx3 = (zb * sx * sy) + (ya * sx) + xb;
							int idx4 = (za * sx * sy) + (yb * sx) + xa;
							int idx5 = (za * sx * sy) + (yb * sx) + xb;
							int idx6 = (zb * sx * sy) + (yb * sx) + xa;
							int idx7 = (zb * sx * sy) + (yb * sx) + xb;

							// 1. Interpolate the scalar/vector values
							float v0 = src.values[idx0];
							float v1 = src.values[idx1];
							float v2 = src.values[idx2];
							float v3 = src.values[idx3];
							float v4 = src.values[idx4];
							float v5 = src.values[idx5];
							float v6 = src.values[idx6];
							float v7 = src.values[idx7];

							bool isUndef = false; float undef = src.undef;
							if (src.useUndef && (v0 == undef || v1 == undef || v2 == undef || v3 == undef ||
								v4 == undef || v5 == undef || v6 == undef || v7 == undef))
							{
								isUndef = true;
							}

							if (isUndef)
							{
								newValues.Add(undef);
							}
							else
							{
								double ans =
									v0 * (1 - p) * (1 - q) * (1 - r) +
									v1 *      p  * (1 - q) * (1 - r) +
									v2 * (1 - p) * (1 - q) *      r  +
									v3 *      p  * (1 - q) *      r  +
									v4 * (1 - p) *      q  * (1 - r) +
									v5 *      p  *      q  * (1 - r) +
									v6 * (1 - p) *      q  *      r +
									v7 *      p  *      q  *      r;

								newValues.Add(Mathf.Clamp((float)ans, src.min, src.max));
							}

							// 2. Interpolate the physical 3D coordinates (coords[3]: X, Y, Z)
							// Note: coords[0] to [2] are not used in IRREGULAR fields, so they are ignored
							if (src.coords.Length > 3 && src.coords[3].Length > 0)
							{
								for (int axis = 0; axis < 3; axis++)
								{
									float c0 = src.coords[3][idx0 * 3 + axis];
									float c1 = src.coords[3][idx1 * 3 + axis];
									float c2 = src.coords[3][idx2 * 3 + axis];
									float c3 = src.coords[3][idx3 * 3 + axis];
									float c4 = src.coords[3][idx4 * 3 + axis];
									float c5 = src.coords[3][idx5 * 3 + axis];
									float c6 = src.coords[3][idx6 * 3 + axis];
									float c7 = src.coords[3][idx7 * 3 + axis];

									float c_ans =
										c0 * (1 - p) * (1 - q) * (1 - r) +
										c1 *      p  * (1 - q) * (1 - r) +
										c2 * (1 - p) * (1 - q) *      r  +
										c3 *      p  * (1 - q) *      r  +
										c4 * (1 - p) *      q  * (1 - r) +
										c5 *      p  *      q  * (1 - r) +
										c6 * (1 - p) *      q  *      r  +
										c7 *      p  *      q  *      r;

									icoords[3].Add(c_ans);
								}
							}
						}
					}
				}
			}
			// ==========================================================
			// Exception Handling: Unsupported data types like UNDEFINED or UNSTRUCTURED
			// ==========================================================
			else
			{
				Debug.LogWarning($"[Downsize] Skipped downsizing for Element {index} due to unsupported FieldType ({src.fieldType}).");

				// Disable this element's output to prevent errors from propagating to downstream modules (e.g., Slicer)
				dst.isActive = false;

				// Communicate "no data" to downstream modules and safely halt execution
				df.dataLoaded = false;

				return;
			}

			dst.SetCoords(icoords);
			dst.SetValues(newValues);
		}
	}
}