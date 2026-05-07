using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisAssets.SciVis.Structured.Common;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Arrows
{
	using FieldType = DataElement.FieldType;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Arrows))]
	public class ArrowsEditor : Editor
	{
		SerializedProperty axis;
		SerializedProperty slice;
		SerializedProperty restrictToSlice;
		SerializedProperty arrowscale;
		SerializedProperty normalize;
		SerializedProperty useMagnitudeColor;
		SerializedProperty arrowPrefab;
		SerializedProperty builtinMaterial;
		SerializedProperty urpMaterial;

		private void OnEnable()
		{
			axis              = serializedObject.FindProperty("sliceHelper.axis");
			slice             = serializedObject.FindProperty("sliceHelper.slice");
			restrictToSlice   = serializedObject.FindProperty("restrictToSlice");
			arrowscale        = serializedObject.FindProperty("arrowscale");
			normalize         = serializedObject.FindProperty("normalize");
			useMagnitudeColor = serializedObject.FindProperty("useMagnitudeColor");
			arrowPrefab       = serializedObject.FindProperty("arrowPrefab");
			builtinMaterial   = serializedObject.FindProperty("builtinMaterial");
			urpMaterial       = serializedObject.FindProperty("urpMaterial");
		}

		public override void OnInspectorGUI()
		{
			var arrows = target as Arrows;

			if (arrows == null) return;

			serializedObject.Update();

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();
			if (axis != null)
			{
				EditorGUILayout.IntSlider(axis, 0, 2, new GUIContent("Axis: "));
			}
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				arrows.SetAxis(axis.intValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();
			if (slice != null)
			{
				EditorGUILayout.Slider(slice, 0f, 1f, new GUIContent("Slice: "));
			}
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				arrows.SetSlice(slice.floatValue);
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();
			if (restrictToSlice != null)
			{
				EditorGUILayout.PropertyField(restrictToSlice, new GUIContent("Restrict to Slice Plane"));
			}
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				arrows.SetRestrictToSlice();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();
			if (arrowscale != null)
			{
				EditorGUILayout.Slider(arrowscale, 0f, arrows.maxArrowScale, new GUIContent("Scale: "));
			}
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				arrows.SetScale();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (normalize != null)
			{
				EditorGUILayout.PropertyField(normalize, new GUIContent("Normalize"));
			}

			GUILayout.Space(5f);

			if (useMagnitudeColor != null)
			{
				EditorGUILayout.PropertyField(useMagnitudeColor, new GUIContent("Use Magnitude Color"));
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				// Normalizing or changing color mode triggers visual update
				arrows.Normalize();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();
			if (arrowPrefab != null)
			{
				EditorGUILayout.PropertyField(arrowPrefab, new GUIContent("Arrow Prefab"));
			}
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				arrows.UpdatePrefab();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(builtinMaterial, new GUIContent("Built-in Material"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(urpMaterial, new GUIContent("URP Material"));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Arrows");
				serializedObject.ApplyModifiedProperties();
				arrows.UpdatePrefab();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	/// <summary>
	/// Renders a vector field as a dense set of arrows on a specified slice plane.
	/// Utilizes Graphics.DrawMeshInstanced for high-performance rendering.
	/// Implements a "Color Binning" approach (quantizing magnitudes into 64 color groups)
	/// to efficiently apply gradient colors across thousands of instances
	/// without requiring customized shaders for per-instance color properties.
	/// </summary>
	[DisallowMultipleComponent]
	public class Arrows : MapperModuleTemplate
	{
		[SerializeField]
		public SliceHelper sliceHelper = new SliceHelper();

		private struct SubMeshInfo
		{
			public Mesh      mesh;
			public Material  material;
			public Matrix4x4 localMatrix;
			public Color     defaultColor;
		}

		[SerializeField, Range(0, 10f)]
		public float scale;
		public float maxScale;

		public bool restrictToSlice   = false;
		public bool normalize         = false;
		public bool useMagnitudeColor = false;

		[SerializeField, ReadOnly]
		public DataElement[] elements;
		[SerializeField, ReadOnly]
		public float average;
		[SerializeField, ReadOnly]
		public float variance;

		[SerializeField]
		private Material builtinMaterial;

		[SerializeField]
		private Material urpMaterial;

		public GameObject arrowPrefab;

		[SerializeField]
		public float arrowscale;
		public float scale_weight = 5.0f;

		[HideInInspector]
		public float maxArrowScale = 10f;
		private float maxMagnitude = 1f;

		public List<int> activeElements;
		public int[]     dims;
		public bool      useUndef;
		public float     undef;

		private List<SubMeshInfo> subMeshes = new List<SubMeshInfo>();

		private List<Vector3> cachedLocalPositions  = new List<Vector3>();
		private List<Vector3> cachedLocalDirections = new List<Vector3>();
		private List<float>   cachedScales          = new List<float>();

		// --- Color Binning (for 64-step smooth gradient) ---
		private const int COLOR_BINS = 64;
		private List<int>[] colorBins    = new List<int>[COLOR_BINS];
		private Matrix4x4[] matrixBuffer = new Matrix4x4[1023]; // Reusable buffer for DrawMeshInstanced

		public override void InitModule()
		{
			dims     = new int[3] { -1, -1, -1 };
			useUndef = false;
			scale    = maxScale = 1f;
			elements = new DataElement[3];

			for (int i = 0; i < COLOR_BINS; i++)
			{
				colorBins[i] = new List<int>();
			}

			if (sliceHelper == null)
			{
				sliceHelper = new SliceHelper();
			}

			sliceHelper.Init();

			ExtractMultiMeshes();

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
			if (subMeshes.Count == 0) ExtractMultiMeshes();

			if (activeElements != null && activeElements.Count > 0)
			{
				CalcSlice();
			}
			else
			{
				cachedLocalPositions.Clear();
			}

			return 1;
		}

		void LateUpdate()
		{
			if (cachedLocalPositions.Count == 0 || subMeshes.Count == 0) return;

			Matrix4x4 localToWorld = transform.localToWorldMatrix;
			float parentScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
			MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

			foreach (var sub in subMeshes)
			{
				if (!useMagnitudeColor)
				{
					// Solid color mode: Set the default color to the property block and draw all elements.
					propertyBlock.SetColor("_BaseColor", sub.defaultColor);
					propertyBlock.SetColor("_Color", sub.defaultColor);

					int remaining = cachedLocalPositions.Count;
					int currentIndex = 0;

					while (remaining > 0)
					{
						int count = Mathf.Min(1023, remaining);
						for (int i = 0; i < count; i++)
						{
							matrixBuffer[i] = ComputeArrowMatrix(currentIndex++, localToWorld, parentScale) * sub.localMatrix;
						}

						Graphics.DrawMeshInstanced(
							sub.mesh, 0, sub.material, matrixBuffer, count, propertyBlock,
							UnityEngine.Rendering.ShadowCastingMode.On, true, gameObject.layer, null,
							UnityEngine.Rendering.LightProbeUsage.BlendProbes
						);

						remaining -= count;
					}
				}
				else
				{
					// Magnitude gradient mode: Group by color bins and draw them in batches.
					// This approach bypasses the limitations of standard shaders which do not
					// support per-instance color properties by default.
					for (int b = 0; b < COLOR_BINS; b++)
					{
						int binCount = colorBins[b].Count;
						if (binCount == 0) continue;

						float level = b / (float)(COLOR_BINS - 1);
						// Gradient from Blue (0.66) to Red (0.0)
						Color binColor = Color.HSVToRGB(Mathf.Lerp(0.66f, 0f, level), 1f, 1f);

						propertyBlock.SetColor("_BaseColor", binColor);
						propertyBlock.SetColor("_Color", binColor);

						int remaining = binCount;
						int currentInBin = 0;

						while (remaining > 0)
						{
							int count = Mathf.Min(1023, remaining);
							for (int i = 0; i < count; i++)
							{
								int arrowIndex = colorBins[b][currentInBin++];
								matrixBuffer[i] = ComputeArrowMatrix(arrowIndex, localToWorld, parentScale) * sub.localMatrix;
							}

							Graphics.DrawMeshInstanced(
								sub.mesh, 0, sub.material, matrixBuffer, count, propertyBlock,
								UnityEngine.Rendering.ShadowCastingMode.On, true, gameObject.layer, null,
								UnityEngine.Rendering.LightProbeUsage.BlendProbes
							);

							remaining -= count;
						}
					}
				}
			}
		}

		private Matrix4x4 ComputeArrowMatrix(int index, Matrix4x4 localToWorld, float parentScale)
		{
			Vector3 worldPos = localToWorld.MultiplyPoint3x4(cachedLocalPositions[index]);
			Vector3 localDir = cachedLocalDirections[index];
			Vector3 worldDir = localToWorld.MultiplyVector(localDir);

			Quaternion worldRot = Quaternion.identity;
			float baseScale     = normalize ? (maxMagnitude * arrowscale) : cachedScales[index];

			if (worldDir.sqrMagnitude > 1e-8f)
			{
				worldRot = Quaternion.FromToRotation(Vector3.up, worldDir);
			}
			else
			{
				baseScale = 0f;
			}

			float s = baseScale * parentScale;
			Vector3 safeScale = new Vector3(s, s, s);

			return Matrix4x4.TRS(worldPos, worldRot, safeScale);
		}

		public override void IdleFunc()
		{
		}

		public override void SetParameters()
		{
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (pdf == null || pdf.elements == null || pdf.elements.Length < 3) return;

			if (elements == null || elements.Length != pdf.elements.Length)
			{
				elements = new DataElement[pdf.elements.Length];
			}

			for (int i = 0; i < 3; i++) elements[i] = pdf.elements[i];

			CheckActiveElements();

			if (activeElements.Count > 0)
			{
				if (sliceHelper == null)
				{
					sliceHelper = new SliceHelper();
				}

				sliceHelper.Reset(pdf.elements[activeElements[0]]);

				CheckData();
			}
		}

		public override void ResetUI()
		{
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

			if (sliceHelper != null)
			{
				sliceHelper.Validate();
			}

			ParameterChanged();
		}

		/// <summary>
		/// Checks which elements are currently active and initializes grid dimensions and undefined values.
		/// </summary>
		private void CheckActiveElements()
		{
			activeElements = new List<int>();

			for (int i = 0; i < 3; i++)
			{
				if (i < elements.Length && elements[i].isActive)
				{
					activeElements.Add(i);
				}
			}

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

		/// <summary>
		/// Analyzes the data to determine the maximum vector magnitude and the grid spacing.
		/// Calculates the initial optimal scale for the arrows to prevent them from overlapping.
		/// </summary>
		void CheckData()
		{
			var valueList = new List<float>();

			for (int k = 0; k < dims[2]; k++)
			{
				int index0 = k * dims[1] * dims[0];

				for (int j = 0; j < dims[1]; j++)
				{
					int index1 = j * dims[0];

					for (int i = 0; i < dims[0]; i++)
					{
						int   index          = index0 + index1 + i;
						float sum_of_squares = 0f;
						bool  isUndef        = false;

						for (int n = 0; n < 3; n++)
						{
							if (n < elements.Length && elements[n].isActive)
							{
								float val = elements[n].values[index];

								if (useUndef && val == undef)
								{
									isUndef = true;
								}
								else
								{
									sum_of_squares += val * val;
								}
							}
						}

						if (isUndef)
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

			float[]            values       = valueList.ToArray();
			IEnumerable<float> valid_values = useUndef ? values.Where(n => n != undef) : values;

			if (!valid_values.Any()) return;

			average      = valid_values.Average();
			var sum2     = valid_values.Sum(a => a * a);
			variance     = sum2 / valid_values.Count() - average * average;
			maxMagnitude = valid_values.Max();

			if (maxMagnitude == 0f) maxMagnitude = 1f;

			if (elements[activeElements[0]].fieldType == FieldType.RECTILINEAR ||
				elements[activeElements[0]].fieldType == FieldType.UNIFORM)
			{
				float dmin = float.MaxValue;

				for (int n = 0; n < 3; n++)
				{
					float[] coord = elements[activeElements[0]].coords[n];

					for (int i = 0; i < dims[n] - 1; i++)
					{
						dmin = Math.Min(dmin, Math.Abs(coord[i + 1] - coord[i]));
					}
				}

				arrowscale = (dmin / maxMagnitude) * 0.8f;
			}
			else if (elements[activeElements[0]].fieldType == FieldType.IRREGULAR)
			{
				arrowscale = (1f / maxMagnitude) * 0.8f;
			}

			maxArrowScale = arrowscale * 5f;

			if (maxArrowScale <= 0f)
			{
				maxArrowScale = 1f;
			}
		}

		/// <summary>
		/// Sets the target slice axis (e.g., X, Y, or Z) and triggers a parameter update.
		/// </summary>
		public void SetAxis(int _axis)
		{
			if (sliceHelper == null) sliceHelper = new SliceHelper();

			if (sliceHelper.SetAxis(_axis))
			{
				if (IsDataLoadedToParent()) ParameterChanged();
			}
		}

		/// <summary>
		/// Sets the normalized position of the slice plane and triggers a parameter update.
		/// </summary>
		public void SetSlice(float _slice)
		{
			if (sliceHelper == null) sliceHelper = new SliceHelper();

			if (sliceHelper.SetSlice(_slice))
			{
				if (IsDataLoadedToParent()) ParameterChanged();
			}
		}

		public void SetRestrictToSlice()
		{
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void SetScale()
		{
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		public void Normalize()
		{
			if (IsDataLoadedToParent()) ParameterChanged();
		}

		/// <summary>
		/// Core calculation routine. Computes the position and direction of all valid arrows
		/// on the active slice plane, storing them in cached lists for GPU instancing.
		/// Registers each arrow into a specific color bin if magnitude coloring is enabled.
		/// </summary>
		public void CalcSlice()
		{
			if (activeElements == null || activeElements.Count == 0 || pdf == null || pdf.elements == null) return;

			DataElement element = pdf.elements[activeElements[0]];
			if (element == null || element.values == null) return;

			if (sliceHelper == null) sliceHelper = new SliceHelper();

			float ratio;
			int   idx = sliceHelper.GetIndexOfCuttingEdge(element, out ratio);

			int slice_w, slice_h, slice_d;
			sliceHelper.GetSliceDimensions(element, out slice_w, out slice_h, out slice_d);

			cachedLocalPositions.Clear();
			cachedLocalDirections.Clear();
			cachedScales.Clear();

			for (int i = 0; i < COLOR_BINS; i++)
			{
				if (colorBins[i] == null) colorBins[i] = new List<int>();
				colorBins[i].Clear();
			}

			float[] slicedata = new float[slice_w * slice_h * 3];

			for (int n = 0; n < 3; n++)
			{
				if (n >= elements.Length || !elements[n].isActive) continue;

				for (int j = 0; j < slice_h; j++)
				{
					for (int i = 0; i < slice_w; i++)
					{
						int idx0, idx1;
						sliceHelper.GetIndices(i, j, slice_w, slice_h, slice_d, idx, out idx0, out idx1);

						float   ansf   = 0;
						float[] values = elements[n].values;

						if (slice_d == 1)
						{
							ansf = values[idx0];
							if (useUndef && ansf == undef) ansf = 0;
						}
						else
						{
							bool is_undef = useUndef && (values[idx0] == undef || values[idx1] == undef);

							if (!is_undef)
							{
								if (sliceHelper.value == sliceHelper.value_min)
								{
									ansf = values[idx0];
								}
								else if (sliceHelper.value == sliceHelper.value_max)
								{
									ansf = values[idx1];
								}
								else
								{
									ansf = values[idx0] * (1f - ratio) + values[idx1] * ratio;
								}
							}
						}

						int flatIdx = slice_w * j + i;
						slicedata[flatIdx * 3 + n] = ansf;

						if (n == activeElements[0])
						{
							float[] coord3 = element.coords[3];
							Vector3 pos    = Vector3.zero;

							if (slice_d == 1)
							{
								pos = new Vector3(
									coord3[idx0 * 3 + 0],
									coord3[idx0 * 3 + 1],
									coord3[idx0 * 3 + 2]
								);
							}
							else
							{
								pos = new Vector3(
									coord3[idx0 * 3 + 0] + (coord3[idx1 * 3 + 0] - coord3[idx0 * 3 + 0]) * ratio,
									coord3[idx0 * 3 + 1] + (coord3[idx1 * 3 + 1] - coord3[idx0 * 3 + 1]) * ratio,
									coord3[idx0 * 3 + 2] + (coord3[idx1 * 3 + 2] - coord3[idx0 * 3 + 2]) * ratio
								);
							}

							cachedLocalPositions.Add(pos);
						}
					}
				}
			}

			for (int i = 0; i < cachedLocalPositions.Count; i++)
			{
				float ux = slicedata[i * 3 + 0];
				float uy = slicedata[i * 3 + 1];
				float uz = slicedata[i * 3 + 2];

				if (restrictToSlice)
				{
					if (sliceHelper.axis == 0)      ux = 0f;
					else if (sliceHelper.axis == 1) uy = 0f;
					else if (sliceHelper.axis == 2) uz = 0f;
				}

				float sumSq = ux * ux + uy * uy + uz * uz;
				float mag   = Mathf.Sqrt(sumSq);

				cachedScales.Add(mag * arrowscale);
				cachedLocalDirections.Add(new Vector3(ux, uy, uz));

				// Register the instance to the appropriate color bin (0 to 63) based on its magnitude level
				float level = maxMagnitude > 0f ? Mathf.Clamp01(mag / maxMagnitude) : 0f;
				int bin = Mathf.Clamp(Mathf.FloorToInt(level * (COLOR_BINS - 1)), 0, COLOR_BINS - 1);
				colorBins[bin].Add(i);
			}
		}

		/// <summary>
		/// Triggers a re-extraction of the sub-meshes and material properties when the assigned prefab is swapped.
		/// </summary>
		public void UpdatePrefab()
		{
			ExtractMultiMeshes();

			if (IsDataLoadedToParent())
			{
				CalcSlice();
				ParameterChanged();
			}
		}

		/// <summary>
		/// Clears previous caches and extracts mesh/material data from the currently assigned arrow prefab for GPU Instancing.
		/// </summary>
		private void ExtractMultiMeshes()
		{
			subMeshes.Clear();

			if (arrowPrefab == null) return;

			TraversePrefab(arrowPrefab.transform, Matrix4x4.identity);
		}

		/// <summary>
		/// Recursively traverses the prefab hierarchy to collect all MeshFilters and MeshRenderers.
		/// Bakes their relative transforms into a combined local matrix to maintain the correct internal offset during instancing.
		/// Automatically upgrades/downgrades materials based on the active Render Pipeline.
		/// </summary>
		private void TraversePrefab(Transform current, Matrix4x4 parentMatrix)
		{
			Matrix4x4 localTRS       = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale);
			Matrix4x4 combinedMatrix = parentMatrix * localTRS;

			var mf = current.GetComponent<MeshFilter>();
			var mr = current.GetComponent<MeshRenderer>();

			if (mf != null && mr != null && mf.sharedMesh != null)
			{
				// Clone the material and enable GPU instancing
				var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ?? UnityEngine.QualitySettings.renderPipeline;
				bool isURP = pipeline != null;

				Material targetMaterial = isURP ? urpMaterial : builtinMaterial;
				Material baseMaterial = targetMaterial != null ? targetMaterial : mr.sharedMaterial;
				Material instancedMaterial = new Material(baseMaterial) { enableInstancing = true };

				// Determine the default color based on the material name
				Color defColor = Color.white;
				if (mr.sharedMaterial.name.Contains("Cone"))
				{
					defColor = new Color(1f, 1f, 0.5f);
				}
				else if (mr.sharedMaterial.name.Contains("Cylinder"))
				{
					defColor = new Color(0f, 0.8f, 1f);
				}
				subMeshes.Add(new SubMeshInfo
				{
					mesh         = mf.sharedMesh,
					material     = instancedMaterial,
					localMatrix  = combinedMatrix,
					defaultColor = defColor
				});
			}

			foreach (Transform child in current)
			{
				TraversePrefab(child, combinedMatrix);
			}
		}
	}
}