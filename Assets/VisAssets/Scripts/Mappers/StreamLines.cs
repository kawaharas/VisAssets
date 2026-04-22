using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.StreamLines
{
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(StreamLines))]
	public class StreamLinesEditor : Editor
	{
		SerializedProperty p0;
		SerializedProperty drawMode;
		SerializedProperty ribbonWidth;
		SerializedProperty useMagnitudeColor;
		SerializedProperty lineColor;
		SerializedProperty sphereColor;
		SerializedProperty activeSeeds;
		SerializedProperty lineShader;
		SerializedProperty sphereShader;
		SerializedProperty linePrefab;

		private void OnEnable()
		{
			p0           = serializedObject.FindProperty("p0");
			drawMode     = serializedObject.FindProperty("drawMode");
			ribbonWidth  = serializedObject.FindProperty("ribbonWidth");
			useMagnitudeColor = serializedObject.FindProperty("useMagnitudeColor");
			lineColor    = serializedObject.FindProperty("lineColor");
			sphereColor  = serializedObject.FindProperty("sphereColor");
			activeSeeds  = serializedObject.FindProperty("activeSeeds");
			lineShader   = serializedObject.FindProperty("lineShader");
			sphereShader = serializedObject.FindProperty("sphereShader");
			linePrefab   = serializedObject.FindProperty("linePrefab");
		}

		public override void OnInspectorGUI()
		{
			var streamlines = target as StreamLines;

			if (streamlines == null) return;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			char baseLabel = 'X';

			for (int i = 0; i < 3; i++)
			{
				var label = (char)(Convert.ToUInt16(baseLabel) + i);

				GUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(label.ToString(), GUILayout.Width(20));
				p0.GetArrayElementAtIndex(i).floatValue = EditorGUILayout.Slider(p0.GetArrayElementAtIndex(i).floatValue, 0, 1f);
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "StreamLines");
				serializedObject.ApplyModifiedProperties();
				streamlines.RequestGuideLineUpdate();
				EditorUtility.SetDirty(target);
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(drawMode, new GUIContent("Draw Mode"));

			if (drawMode.enumValueIndex == (int)StreamLines.DrawMode.RIBBON)
			{
				GUILayout.Space(5f);

				EditorGUILayout.PropertyField(ribbonWidth, new GUIContent("Ribbon Width"));
			}

			GUILayout.Space(5f);

			useMagnitudeColor.boolValue = EditorGUILayout.Toggle("Use Magnitude Color", useMagnitudeColor.boolValue);

			if (!useMagnitudeColor.boolValue)
			{
				GUILayout.Space(5f);
				EditorGUILayout.PropertyField(lineColor, new GUIContent("Line/Ribbon Color"));

				if (drawMode.enumValueIndex == (int)StreamLines.DrawMode.LINE)
				{
					GUILayout.Space(5f);
					EditorGUILayout.PropertyField(sphereColor, new GUIContent("Sphere Color"));
				}
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "StreamLines");
				serializedObject.ApplyModifiedProperties();
				streamlines.RequestGuideLineUpdate();
				streamlines.UpdateLineColors();
				streamlines.RefreshAllLines();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(10f);

			if (GUILayout.Button("Add New Seed"))
			{
				streamlines.AddSeedFromUI();
			}

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(activeSeeds, new GUIContent("Seeds List (Read Only)"), true);
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(10f);

			EditorGUI.BeginChangeCheck();

			if (lineShader != null)
			{
				EditorGUILayout.PropertyField(lineShader, new GUIContent("Line/Ribbon Shader"));
			}

			GUILayout.Space(5f);

			if (sphereShader != null)
			{
				EditorGUILayout.PropertyField(sphereShader, new GUIContent("Sphere Shader"));
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "StreamLines");
				serializedObject.ApplyModifiedProperties();
				streamlines.UpdateMaterial();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(linePrefab, new GUIContent("Line Prefab"));

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
	/// Manager class that centralizes vector field data and manages multiple StreamLine instances.
	/// Implements object pooling for streamlines and handles UI-driven seed point placement.
	/// </summary>
	[DisallowMultipleComponent]
	public class StreamLines : MapperModuleTemplate
	{
		public enum DrawMode
		{
			LINE,
			RIBBON
		}

		[SerializeField]
		public Shader lineShader;

		[SerializeField]
		public Shader sphereShader;

		[SerializeField]
		public DrawMode drawMode = DrawMode.LINE;

		[SerializeField, Range(0.001f, 0.5f)]
		public float ribbonWidth = 0.05f;

		[SerializeField]
		public bool useMagnitudeColor = false;

		[SerializeField]
		public Color lineColor = Color.cyan;

		[SerializeField]
		public Color sphereColor = Color.yellow;

		[SerializeField, Range(0, 1f)]
		public float[] p0 = new float[3];

		public GameObject linePrefab;
		public int        maxStep      = 5000;
		public int        max_line_num = 100;
		public float      displayTime  = 5f;
		public Vector3    upstreamReciprocalScale = Vector3.one;

		[SerializeField, ReadOnly]
		private List<Vector3> activeSeeds = new List<Vector3>();

		private DataElement[] elements       = new DataElement[3];
		private List<int>     activeElements = new List<int>();
		private int[]         dims           = new int[3] { -1, -1, -1 };
		private bool          useUndef;
		private float         undef;
		private float         min;
		private float         max;
		private Vector3       boundMin;
		private Vector3       boundMax;
		private float         magMin;
		private float         magMax;

		private List<StreamLine> linePool         = new List<StreamLine>();
		private int              currentLineIndex = 0;

		private Mesh     guideMesh;
		private Material sharedMaterial;
		private Material sharedSphereMaterial;

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();

			EnsureCorrectShader();
		}
#endif

#if UNITY_EDITOR
		/// <summary>
		/// Automatically detects the current Render Pipeline and returns the appropriate default shaders.
		/// </summary>
		private void EnsureCorrectShader()
		{
			var pipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ?? UnityEngine.QualitySettings.renderPipeline;
			bool isURP = pipelineAsset != null;

			string expectedLineShader   = isURP ? "Universal Render Pipeline/Particles/Unlit" : "Sprites/Default";
			string expectedSphereShader = isURP ? "Universal Render Pipeline/Lit" : "Standard";

			string wrongLineShader   = isURP ? "Sprites/Default" : "Universal Render Pipeline/Particles/Unlit";
			string wrongSphereShader = isURP ? "Standard" : "Universal Render Pipeline/Lit";

			if (lineShader == null || 
			    lineShader.name == wrongLineShader || 
			    lineShader.name == "Universal Render Pipeline/Unlit" || 
			    lineShader.name == "Hidden/InternalErrorShader")
			{
				lineShader = Shader.Find(expectedLineShader);
			}

			if (sphereShader == null || 
			    sphereShader.name == wrongSphereShader || 
			    sphereShader.name == "Hidden/InternalErrorShader")
			{
				sphereShader = Shader.Find(expectedSphereShader);
			}
/*
			bool isURP = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null;
			string expectedLineShader   = isURP ? "Universal Render Pipeline/Unlit" : "Sprites/Default";
			string expectedSphereShader = isURP ? "Universal Render Pipeline/Lit" : "Standard";

			if (lineShader == null)
			{
				lineShader = Shader.Find(expectedLineShader);
			}

			if (sphereShader == null)
			{
				sphereShader = Shader.Find(expectedSphereShader);
			}
*/
		}
#endif

		public override void InitModule()
		{
			for (int i = 0; i < 3; i++)
			{
				p0[i] = 0;
			}

#if UNITY_EDITOR
			EnsureCorrectShader();
#endif

			guideMesh = new Mesh();
			guideMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null) meshFilter.sharedMesh = guideMesh;

			UpdateMaterial();
			InitializeLinePool();
		}

		public override int BodyFunc()
		{
			Transform current       = transform;
			Vector3   upstreamScale = Vector3.one;

			while (current.parent != null)
			{
				current       = current.parent;
				upstreamScale = Vector3.Scale(upstreamScale, current.localScale);
			}

			upstreamReciprocalScale = new Vector3(1f / upstreamScale.x, 1f / upstreamScale.y, 1f / upstreamScale.z);

			return 1;
		}

		public override void IdleFunc()
		{
			if (displayTime > 0)
			{
				displayTime -= Time.deltaTime;

				if (displayTime < 0)
				{
					displayTime = 0;
				}

				UpdateGuideLineMesh();
			}
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

			for (int i = 0; i < 3; i++)
			{
				elements[i] = pdf.elements[i];
			}

			CheckActiveElements();

			if (activeElements.Count > 0)
			{
				boundMin = elements[activeElements[0]].boundMin;
				boundMax = elements[activeElements[0]].boundMax;
			}

			foreach (var line in linePool)
			{
				line.ClearTrace();
			}

			currentLineIndex = 0;
			activeSeeds.Clear();
			RequestGuideLineUpdate();
		}

		public override void ResetUI()
		{
		}

		/// <summary>
		/// Triggers parameter update when inspector values are modified.
		/// </summary>
		private void OnValidate()
		{
#if UNITY_EDITOR
			EnsureCorrectShader();
#endif
			if (!IsDataLoadedToParent()) return;
			activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

		/// <summary>
		/// Updates the shared materials based on the selected shaders and synchronizes all streamlines.
		/// </summary>
		public void UpdateMaterial()
		{
			if (lineShader == null || sphereShader == null) return;

			// Line/Ribbon Material
			if (sharedMaterial == null || sharedMaterial.shader != lineShader)
			{
				if (sharedMaterial != null)
				{
					if (Application.isPlaying)
					{
						Destroy(sharedMaterial);
					}
					else
					{
						DestroyImmediate(sharedMaterial);
					}
				}

				sharedMaterial = new Material(lineShader);
				sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
			}

			// Sphere Material (Lit)
			if (sharedSphereMaterial == null || sharedSphereMaterial.shader != sphereShader)
			{
				if (sharedSphereMaterial != null)
				{
					if (Application.isPlaying)
					{
						Destroy(sharedSphereMaterial);
					}
					else
					{
						DestroyImmediate(sharedSphereMaterial);
					}
				}

				sharedSphereMaterial = new Material(sphereShader);
			}

			var meshRenderer = GetComponent<MeshRenderer>();

			if (meshRenderer != null)
			{
				meshRenderer.sharedMaterial = sharedMaterial;
			}

			foreach (var line in linePool)
			{
				if (line != null)
				{
					line.SetMaterial(sharedMaterial, sharedSphereMaterial);
				}
			}
		}

		/// <summary>
		/// Pre-instantiates a pool of streamlines to avoid runtime performance spikes.
		/// </summary>
		private void InitializeLinePool()
		{
			if (linePrefab == null) return;

			foreach (var line in linePool)
			{
				if (line != null)
				{
					Destroy(line.gameObject);
				}
			}

			linePool.Clear();
			activeSeeds.Clear();

			for (int i = 0; i < max_line_num; i++)
			{
				var go = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, transform);
				go.transform.localPosition = Vector3.zero;

				var sl = go.GetComponent<StreamLine>();

				if (sl != null)
				{
					sl.SetMaterial(sharedMaterial, sharedSphereMaterial);
					linePool.Add(sl);
				}
			}
		}

		/// <summary>
		/// Checks which data elements are currently active and initializes grid dimensions.
		/// </summary>
		private void CheckActiveElements()
		{
			activeElements.Clear();

			for (int i = 0; i < 3; i++)
			{
				if (elements[i] != null && elements[i].isActive)
				{
					activeElements.Add(i);
				}
			}

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

		/// <summary>
		/// Scans the active vector field to determine the minimum and maximum velocity magnitudes.
		/// </summary>
		private void CheckRange()
		{
			magMin   = float.MaxValue;
			magMax   = float.MinValue;
			int size = dims[0] * dims[1] * dims[2];

			for (int i = 0; i < size; i++)
			{
				bool    isUndefLoc = false;
				Vector3 vec3       = Vector3.zero;

				for (int n = 0; n < 3; n++)
				{
					if (elements[n].isActive)
					{
						float value = elements[n].values[i];

						if (useUndef && value == undef)
						{
							isUndefLoc = true;
						}
						else
						{
							vec3[n] = value;
						}
					}
				}

				if (!isUndefLoc)
				{
					float mag = vec3.magnitude;
					magMin = Mathf.Min(magMin, mag);
					magMax = Mathf.Max(magMax, mag);
				}
			}
		}

		/// <summary>
		/// Calculates a seed point based on inspector coordinates and starts a trace.
		/// </summary>
		public void AddSeedFromUI()
		{
			if (activeElements.Count == 0) return;

			var seed = new Vector3(
				boundMin.x + (boundMax.x - boundMin.x) * p0[0],
				boundMin.y + (boundMax.y - boundMin.y) * p0[1],
				boundMin.z + (boundMax.z - boundMin.z) * p0[2]
			);

			StartNewStreamLine(seed);
		}

		/// <summary>
		/// Starts a new streamline trace from the specified world coordinate seed.
		/// </summary>
		public void AddSeed(Vector3 seed)
		{
			StartNewStreamLine(seed);
		}

		public void ClearAllSeeds()
		{
			InitializeLinePool();
		}

		/// <summary>
		/// Uses ring-buffer logic to reuse streamlines from the pool and updates the active seed list.
		/// </summary>
		private void StartNewStreamLine(Vector3 seed)
		{
			if (linePool.Count == 0 || activeElements.Count == 0) return;

			var line = linePool[currentLineIndex];
			line.StartTrace(seed, this, lineColor);

			if (activeSeeds.Count < max_line_num)
			{
				activeSeeds.Add(seed);
			}
			else
			{
				activeSeeds[currentLineIndex] = seed;
			}

			currentLineIndex = (currentLineIndex + 1) % max_line_num;
		}

		/// <summary>
		/// Calculates interpolated velocity at a given spatial coordinate.
		/// Centralized method called by all streamline workers.
		/// </summary>
		public Vector3 GetVelocityAt(Vector3 position, out bool isValid)
		{
			isValid = false;

			if (activeElements.Count == 0) return Vector3.zero;

			var activeElement = elements[activeElements[0]];

			for (int i = 0; i < 3; i++)
			{
				if (position[i] < boundMin[i] || position[i] > boundMax[i]) return Vector3.zero;
			}

			int[] idx = new int[3];

			for (int n = 0; n < 3; n++)
			{
				float[] coord       = activeElement.coords[n];
				int     size        = activeElement.dims[n];
				bool    isAscending = coord.First() < coord.Last();

				for (int i = 0; i < size - 1; i++)
				{
					if ((isAscending && coord[i + 1] >= position[n]) || (!isAscending && coord[i + 1] <= position[n]))
					{
						idx[n] = i;
						break;
					}
				}
			}

			int xa = idx[0], xb = Mathf.Min(xa + 1, dims[0] - 1);
			int ya = idx[1], yb = Mathf.Min(ya + 1, dims[1] - 1);
			int za = idx[2], zb = Mathf.Min(za + 1, dims[2] - 1);

			float x0 = activeElement.coords[0][xa], x1 = activeElement.coords[0][xb];
			float y0 = activeElement.coords[1][ya], y1 = activeElement.coords[1][yb];
			float z0 = activeElement.coords[2][za], z1 = activeElement.coords[2][zb];

			float p = (x1 != x0) ? (position[0] - x0) / (x1 - x0) : 0f;
			float q = (y1 != y0) ? (position[1] - y0) / (y1 - y0) : 0f;
			float r = (z1 != z0) ? (position[2] - z0) / (z1 - z0) : 0f;

			Vector3 result = Vector3.zero;

			for (int n = 0; n < 3; n++)
			{
				if (!elements[n].isActive) continue;

				var values = elements[n].values;
				int sx     = dims[0], sy = dims[1];
				int sxsy   = sx * sy;

				float v000 = values[(za * sxsy) + (ya * sx) + xa];
				float v100 = values[(za * sxsy) + (ya * sx) + xb];
				float v001 = values[(zb * sxsy) + (ya * sx) + xa];
				float v101 = values[(zb * sxsy) + (ya * sx) + xb];
				float v010 = values[(za * sxsy) + (yb * sx) + xa];
				float v110 = values[(za * sxsy) + (yb * sx) + xb];
				float v011 = values[(zb * sxsy) + (yb * sx) + xa];
				float v111 = values[(zb * sxsy) + (yb * sx) + xb];

				if (useUndef)
				{
					if (v000 == undef || v100 == undef || v001 == undef || v101 == undef ||
						v010 == undef || v110 == undef || v011 == undef || v111 == undef)
					{
						return Vector3.zero;
					}
				}

				float ans = v000 * (1 - p) * (1 - q) * (1 - r) +
							v100 *  p      * (1 - q) * (1 - r) +
							v001 * (1 - p) * (1 - q) *  r      +
							v101 *  p      * (1 - q) *  r      +
							v010 * (1 - p) *  q      * (1 - r) +
							v110 *  p      *  q      * (1 - r) +
							v011 * (1 - p) *  q      *  r      +
							v111 *  p      *  q      *  r;

				result[n] = Mathf.Clamp(ans, min, max);
			}

			isValid = true;
			return result;
		}

		/// <summary>
		/// Returns an HSV-based color mapped to the vector magnitude.
		/// </summary>
		public Color GetMagnitudeColor(float magnitude)
		{
			float level = Mathf.Clamp((magnitude - magMin) / (magMax - magMin), 0, 1f);
			Color c     = Color.HSVToRGB(level, 1f, 1f);

			return new Color(c.r, c.g, c.b, 1f);
		}

		/// <summary>
		/// Resets the fade timer and triggers guide line visualization.
		/// </summary>
		public void RequestGuideLineUpdate()
		{
			displayTime = 5f;

			UpdateGuideLineMesh();
		}

		/// <summary>
		/// Draws the X, Y, Z axis guides crossing at the current seed point.
		/// </summary>
		private void UpdateGuideLineMesh()
		{
			if (guideMesh == null) return;

			if (activeElements.Count == 0)
			{
				guideMesh.Clear();
				return;
			}

			float alpha = displayTime / 5f;

			if (alpha <= 0f)
			{
				guideMesh.Clear();
				return;
			}

			var p = new float[3];

			for (int i = 0; i < 3; i++)
			{
				p[i] = boundMin[i] + (boundMax[i] - boundMin[i]) * p0[i];
			}

			Vector3[] verts = new Vector3[]
			{
				new Vector3(boundMin[0], p[1], p[2]),
				new Vector3(boundMax[0], p[1], p[2]),
				new Vector3(p[0], boundMin[1], p[2]),
				new Vector3(p[0], boundMax[1], p[2]),
				new Vector3(p[0], p[1], boundMin[2]),
				new Vector3(p[0], p[1], boundMax[2])
			};

			Color[] cols = new Color[]
			{
				new Color(1f, 0f, 0f, alpha),
				new Color(1f, 0f, 0f, alpha),
				new Color(0f, 1f, 0f, alpha),
				new Color(0f, 1f, 0f, alpha),
				new Color(0f, 0f, 1f, alpha),
				new Color(0f, 0f, 1f, alpha)
			};

			int[] inds = new int[] { 0, 1, 2, 3, 4, 5 };

			guideMesh.Clear();
			guideMesh.SetVertices(verts);
			guideMesh.SetColors(cols);
			guideMesh.SetIndices(inds, MeshTopology.Lines, 0);
		}

		/// <summary>
		/// Dynamically updates the color of all currently active streamlines in the pool.
		/// </summary>
		public void UpdateLineColors()
		{
			foreach (var line in linePool)
			{
				if (line != null)
				{
					line.UpdateSolidColor(lineColor);
				}
			}
		}

		/// <summary>
		/// Forces all active lines to refresh their meshes.
		/// </summary>
		public void RefreshAllLines()
		{
			foreach (var line in linePool)
			{
				if (line != null)
				{
					line.ForceMeshUpdate();
				}
			}
		}

		public void SetMode(int mode)
		{
			drawMode = (DrawMode)mode;
//			UpdateLineColors();
			RefreshAllLines();

			if (IsDataLoadedToParent()) ParameterChanged();
		}
	}
}