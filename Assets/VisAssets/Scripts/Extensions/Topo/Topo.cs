using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using VisAssets.SciVis.Structured.Common;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.Extensions.Topo
{
	public enum TopoType
	{
		TOPO_NONE,
		TOPO_ETOPO5,
		TOPO_ETOPO2,
		TOPO_ETOPO1
	}

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Topo))]
	public class TopoEditor : Editor
	{
		SerializedProperty syncScale;
		SerializedProperty type;
		SerializedProperty groundScale;
		SerializedProperty seafloorScale;
		SerializedProperty shader;
		SerializedProperty autoFetchBounds;
		SerializedProperty north;
		SerializedProperty south;
		SerializedProperty west;
		SerializedProperty east;

		private void OnEnable()
		{
			syncScale       = serializedObject.FindProperty("syncScale");
			type            = serializedObject.FindProperty("type");
			groundScale     = serializedObject.FindProperty("groundScale");
			seafloorScale   = serializedObject.FindProperty("seafloorScale");
			shader          = serializedObject.FindProperty("shader");
			autoFetchBounds = serializedObject.FindProperty("autoFetchBounds");
			north           = serializedObject.FindProperty("north");
			south           = serializedObject.FindProperty("south");
			west            = serializedObject.FindProperty("west");
			east            = serializedObject.FindProperty("east");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(type, new GUIContent("Topography Data"));

			GUILayout.Space(5f);

			EditorGUILayout.LabelField("--- Scale Settings ---", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(groundScale, new GUIContent("Ground Scale"));

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(syncScale.boolValue);
			EditorGUILayout.PropertyField(seafloorScale, new GUIContent("Seafloor Scale"));
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(syncScale, new GUIContent("Synchronize ground scale and seafloor scale"));

			GUILayout.Space(5f);

			EditorGUILayout.LabelField("--- Grid Bounds Settings ---", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(autoFetchBounds, new GUIContent("Auto-fetch Bounds",
				"When enabled, automatically fetches latitude and longitude from the parent module's data."));

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(autoFetchBounds.boolValue);

			EditorGUI.indentLevel++;

			EditorGUILayout.PropertyField(north);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(south);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(west);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(east);

			GUILayout.Space(5f);

			EditorGUI.indentLevel--;

			EditorGUI.EndDisabledGroup();

			EditorGUILayout.PropertyField(shader, new GUIContent("Shader"));

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
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class Topo : MapperModuleTemplate
	{
		public int LON, LAT;
		[HideInInspector] public int m_LON, m_LAT;

		public bool autoFetchBounds = true;
		[SerializeField]
		public double north;
		[SerializeField]
		public double south;
		[SerializeField]
		public double west;
		[SerializeField]
		public double east;

		[Range(0.0f, 100.0f)]
		public float groundScale = 1.0f;
		[Range(0.0f, 100.0f)]
		public float seafloorScale = 1.0f;
		public bool syncScale = true;

		public TopoType type = TopoType.TOPO_NONE;
		private TopoType type_prev;

		public Shader shader;
		private Material topoMaterial;

		private MeshFilter filter;
		private Coroutine loadCoroutine;

		private double prev_west = -999.0, prev_east = -999.0, prev_south = -999.0, prev_north = -999.0;
		private float prev_groundScale = -999.0f;
		private float prev_seafloorScale = -999.0f;
		private bool prev_syncScale = false;
		private bool prev_autoFetch = false;

		private bool needsLoad = false;

		/// <summary>
		/// Initializes module components such as MeshFilter.
		/// </summary>
		public override void InitModule()
		{
			type_prev = type;

			if (!TryGetComponent<MeshFilter>(out filter))
			{
				filter = gameObject.AddComponent<MeshFilter>();
			}
		}

		/// <summary>
		/// Main execution function that triggers data loading based on boundary updates.
		/// </summary>
		public override int BodyFunc()
		{
			if (CheckNeedsUpdate())
			{
				TriggerLoad();
			}

			return 1;
		}

		/// <summary>
		/// Re-initializes module parameters when upstream data is refreshed.
		/// </summary>
		public override void ReSetParameters()
		{
			if (CheckNeedsUpdate())
			{
				TriggerLoad();
			}
		}

		public override void SetParameters()
		{
		}

		public override void GetParameters()
		{
		}

		public override void ResetUI()
		{
		}

		/// <summary>
		/// Checks if grid bounds or rendering scales have changed, requiring a mesh rebuild.
		/// </summary>
		private bool CheckNeedsUpdate()
		{
			if (autoFetchBounds)
			{
				if (pdf != null && pdf.dataLoaded && pdf.elements != null && pdf.elements.Length > 0)
				{
					var element = pdf.elements[0];

					if (element != null && element.coords != null && element.coords.Length >= 2)
					{
						west  = element.coords[0].Min();
						east  = element.coords[0].Max();
						south = element.coords[1].Min();
						north = element.coords[1].Max();
					}
				}
			}

			if (west == prev_west && east == prev_east && south == prev_south && north == prev_north &&
				Mathf.Approximately(groundScale, prev_groundScale) &&
				Mathf.Approximately(seafloorScale, prev_seafloorScale) &&
				syncScale == prev_syncScale &&
				autoFetchBounds == prev_autoFetch && filter.sharedMesh != null)
			{
				return false;
			}

			prev_west = west; prev_east = east; prev_south = south; prev_north = north;
			prev_groundScale = groundScale; prev_seafloorScale = seafloorScale;
			prev_syncScale = syncScale; prev_autoFetch = autoFetchBounds;

			return true;
		}

		private void OnValidate()
		{
			if (Application.isPlaying)
			{
				if (type != type_prev || CheckNeedsUpdate())
				{
					needsLoad = true;
				}
			}
		}

		/// <summary>
		/// Safely executes flagged mesh rebuilds during the standard framework update cycle.
		/// Counteracts only the parent's vertical exaggeration (zScale) to maintain independent scaling.
		/// </summary>
		private void LateUpdate()
		{
			var parentGrADS = GetComponentInParent<VisAssets.SciVis.Structured.DataLoader.ReadGrADS>();
			if (parentGrADS != null && parentGrADS.zScale > 0.001f)
			{
				// Divide by zScale only. Inherit the parent's sign to ensure correct mesh flipping.
				transform.localScale = new Vector3(1f, 1f, 1f / parentGrADS.zScale);
			}

			if (needsLoad)
			{
				needsLoad = false;
				TriggerLoad();
			}
		}

		/// <summary>
		/// Safely stops any ongoing data loads and triggers a new data loading coroutine.
		/// </summary>
		private void TriggerLoad()
		{
			if (loadCoroutine != null)
			{
				StopCoroutine(loadCoroutine);
			}

			loadCoroutine = StartCoroutine(SetData());
		}

		/// <summary>
		/// Asynchronous coroutine to load, read, and extract elevation data from ETOPO binary files.
		/// </summary>
		IEnumerator SetData()
		{
			string filename = null;

			switch (type)
			{
				case TopoType.TOPO_ETOPO5:
					filename = "ETOPO5.DOS";
					LON = 4320;
					LAT = 2160;
					break;
				case TopoType.TOPO_ETOPO2:
					filename = "ETOPO2v2g_i2_LSB.bin";
					LON = 10801;
					LAT = 5401;
					break;
				case TopoType.TOPO_ETOPO1:
					filename = "etopo1_ice_g_i2.bin";
					LON = 21601;
					LAT = 10801;
					break;
				default:
					if (filter != null && filter.sharedMesh != null)
					{
						filter.sharedMesh.Clear();
					}
					type_prev = TopoType.TOPO_NONE;
					UpdateSeaSurface();

					yield break;
			}

			string fullPath = Path.Combine(Application.streamingAssetsPath, filename);
			bool isAndroidOrWeb = Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.WebGLPlayer || fullPath.Contains("://");

			if (!isAndroidOrWeb && !File.Exists(fullPath))
			{
				Debug.LogWarning($"[Topo] File not found: {fullPath}");
				type = type_prev;

				yield break;
			}

			type_prev = type;

			double dLON = (double)LON; double dLAT = (double)LAT;
			int idx0x = 0, idx1x = 0, idx0y = 0, idx1y = 0;

			if (type == TopoType.TOPO_ETOPO5)
			{
				for (int i = 0; i < LON * 2; i++)
				{
					if (west < 180.0 * (2.0 * i + 1.0) / dLON)
					{
						idx0x = i - 1;
						break;
					}
				}

				for (int i = 0; i < LON * 2; i++)
				{
					if (east < 180.0 * (2.0 * i + 1.0) / dLON)
					{
						idx1x = i;
						break;
					}
				}

				for (int i = 0; i < LAT; i++)
				{
					if (south < -90.0 + (180.0 * i + 90.0) / dLAT)
					{
						idx0y = i;
						break;
					}
				}

				idx1y = LAT - 1;

				for (int i = 0; i < LAT; i++)
				{
					if (north < -90.0 + (180.0 * i + 90.0) / dLAT)
					{
						idx1y = i;
						break;
					}
				}
			}
			else
			{
				for (int i = 0; i < LON * 2; i++)
				{
					if (west < 360.0 / (dLON - 1.0) * i)
					{
						idx0x = i - 1;
						break;
					}
				}

				for (int i = 0; i < LON * 2; i++)
				{
					if (east < 360.0 / (dLON - 1.0) * i)
					{
						idx1x = i;
						break;
					}
				}

				for (int i = 0; i < LAT; i++)
				{
					if (south < -90.0 + 180.0 / (dLAT - 1.0) * i)
					{
						idx0y = i - 1;
						break;
					}
				}

				for (int i = 0; i < LAT; i++)
				{
					if (north <= -90.0 + 180.0 / (dLAT - 1.0) * i)
					{
						idx1y = i;
						break;
					}
				}
			}

			m_LON = idx1x - idx0x + 1;
			m_LAT = idx1y - idx0y + 1;

			if (m_LON <= 0 || m_LAT <= 0)
			{
				Debug.LogWarning("[Topo] Specified latitude and longitude ranges are invalid.");
				yield break;
			}

			short[] m_AreaData = new short[m_LON * m_LAT];
			byte[] downloadedData = null;

			if (isAndroidOrWeb)
			{
				using (UnityWebRequest www = UnityWebRequest.Get(fullPath))
				{
					yield return www.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
					if (www.result == UnityWebRequest.Result.Success)
#else
					if (!www.isHttpError && !www.isNetworkError)
#endif
					{
						downloadedData = www.downloadHandler.data;
					}
					else
					{
						Debug.LogWarning("[Topo] WebRequest Error: " + www.error);
						yield break;
					}
				}
			}

			try
			{
				if (isAndroidOrWeb)
				{
					if (downloadedData != null)
					{
						using (MemoryStream ms = new MemoryStream(downloadedData))
						{
							ExtractElevationData(ms, m_AreaData, idx0x, idx1x, idx0y, idx1y);
						}
					}
				}
				else
				{
					using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						ExtractElevationData(fs, m_AreaData, idx0x, idx1x, idx0y, idx1y);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[Topo] Data Read Exception: " + e.Message);
				yield break;
			}

			GenerateMesh(m_AreaData, dLON, dLAT, idx0x, idx0y);
		}

		/// <summary>
		/// Efficiently extracts line-by-line elevation grids from the binary stream into the target data array.
		/// </summary>
		private void ExtractElevationData(Stream stream, short[] areaData, int idx0x, int idx1x, int idx0y, int idx1y)
		{
			byte[] lineBuffer = new byte[LON * sizeof(short)];
			int ptr = 0;

			for (int j = idx0y; j <= idx1y; j++)
			{
				long lineStartOffset = (long)LON * (LAT - (j + 1)) * sizeof(short);

				stream.Position = lineStartOffset;
				stream.Read(lineBuffer, 0, lineBuffer.Length);

				for (int i = idx0x; i <= idx1x; i++)
				{
					int lonPos = i;

					if (type == TopoType.TOPO_ETOPO5)
					{
						if (i == -1)
						{
							lonPos = LON - 1;
						}
						else if (i >= LON)
						{
							lonPos = i - LON;
						}
					}
					else
					{
						lonPos = i + (LON / 2);

						if (lonPos >= LON)
						{
							lonPos -= LON;
						}
					}

					areaData[ptr++] = BitConverter.ToInt16(lineBuffer, lonPos * sizeof(short));
				}
			}
		}

		/// <summary>
		/// Constructs the 3D terrain mesh geometry based on extracted elevation values and assigned scaling properties.
		/// Cancels out parent scaling to maintain independent visual magnification for Topography.
		/// </summary>
		private void GenerateMesh(short[] areaData, double dLON, double dLAT, int idx0x, int idx0y)
		{
			double[] coordX = new double[m_LON];
			double[] coordY = new double[m_LAT];

			if (type == TopoType.TOPO_ETOPO5)
			{
				for (int i = 0; i < m_LON; i++)
				{
					coordX[i] = 180.0 * (2.0 * (i + idx0x) + 1.0) / dLON;
				}

				for (int j = 0; j < m_LAT; j++)
				{
					coordY[j] = -90.0 + (180.0 * (j + idx0y) + 90.0) / dLAT;
				}
			}
			else
			{
				for (int i = 0; i < m_LON; i++)
				{
					coordX[i] = 360.0 / (dLON - 1.0) * (i + idx0x);
				}

				for (int j = 0; j < m_LAT; j++)
				{
					coordY[j] = -90.0 + 180.0 / (dLAT - 1.0) * (j + idx0y);
				}
			}

			Vector3[] verts = new Vector3[m_LON * m_LAT];
			Color[] cols = new Color[m_LON * m_LAT];
			Vector3[] norms = new Vector3[m_LON * m_LAT];
			int[] inds = new int[(m_LON - 1) * (m_LAT - 1) * 6];

			var parentGrADS = GetComponentInParent<VisAssets.SciVis.Structured.DataLoader.ReadGrADS>();
			float unitConversion = 1f;

			if (parentGrADS != null && parentGrADS.zUnit == VisAssets.SciVis.Structured.DataLoader.ReadGrADS.ZUnit.KILOMETERS)
			{
				unitConversion = 0.001f;
			}

			int vIdx = 0;
			for (int j = 0; j < m_LAT; j++)
			{
				for (int i = 0; i < m_LON; i++)
				{
					float z = (float)areaData[vIdx]; // Elevation in meters

					if (z > 0.0f)
					{
						float red = Mathf.Clamp01(0.17f + z / 1000.0f * 0.3f);
						cols[vIdx] = new Color(red, 0.5f, 0.17f, 1.0f);
					}
					else
					{
						cols[vIdx] = new Color(0.6f, 0.6f, 0.6f, 1.0f);
					}

					float scaleMultiplier = (z > 0.0f) ? groundScale : (syncScale ? groundScale : seafloorScale);

					// Raw generation in GrADS space. Let the parent's Transform handle the overall scale!
					float finalZ = z * unitConversion * scaleMultiplier;

					verts[vIdx] = new Vector3((float)coordX[i], (float)coordY[j], finalZ);
					vIdx++;
				}
			}

			int indIdx = 0;

			for (int j = 0; j < m_LAT - 1; j++)
			{
				for (int i = 0; i < m_LON - 1; i++)
				{
					int idx0 = m_LON * j + i;
					int idx1 = m_LON * (j + 1) + i;
					int idx2 = m_LON * (j + 1) + (i + 1);
					int idx3 = m_LON * j + (i + 1);

					Vector3 norm0 = Vector3.Cross(verts[idx2] - verts[idx0], verts[idx1] - verts[idx0]).normalized;
					Vector3 norm1 = Vector3.Cross(verts[idx3] - verts[idx0], verts[idx2] - verts[idx0]).normalized;

					norms[idx1] += norm0; norms[idx0] += norm0; norms[idx2] += norm0;
					norms[idx2] += norm1; norms[idx0] += norm1; norms[idx3] += norm1;

					// Standard winding order
					inds[indIdx++] = idx0; inds[indIdx++] = idx2; inds[indIdx++] = idx1;
					inds[indIdx++] = idx0; inds[indIdx++] = idx3; inds[indIdx++] = idx2;
				}
			}

			for (int i = 0; i < norms.Length; i++)
			{
				norms[i] = norms[i].normalized;
			}

			var mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(verts);
			mesh.SetColors(cols);
			mesh.SetIndices(inds, MeshTopology.Triangles, 0);
			mesh.RecalculateNormals(); // Unity handles correct shading calculation based on final scale

			if (filter == null)
			{
				filter = GetComponent<MeshFilter>();
			}
			filter.sharedMesh = mesh;

			SetupMaterial();
			UpdateSeaSurface();
		}

		/// <summary>
		/// Assigns the appropriate material shader based on the current render pipeline configurations.
		/// </summary>
		private void SetupMaterial()
		{
			bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
			string expectedShader = isURP ? "Universal Render Pipeline/Particles/Lit" : "VisAssets/Isosurface";

			if (shader == null || shader.name != expectedShader)
			{
				shader = Shader.Find(expectedShader);

				if (shader == null && !isURP)
				{
					shader = Shader.Find("Standard");
				}
			}

			if (shader != null)
			{
				if (topoMaterial == null || topoMaterial.shader != shader)
				{
					topoMaterial = new Material(shader);

					if (isURP && topoMaterial.HasProperty("_Smoothness"))
					{
						topoMaterial.SetFloat("_Smoothness", 0f);
					}
				}

				if (TryGetComponent<MeshRenderer>(out var renderer))
				{
					renderer.sharedMaterial = topoMaterial;
				}
			}
		}

		/// <summary>
		/// Commands the child SeaSurface component to adapt its dimensions according to newly parsed bounds.
		/// </summary>
		private void UpdateSeaSurface()
		{
			var sea = GetComponentInChildren<SeaSurface>();

			if (sea != null)
			{
				sea.UpdateSurface(west, east, south, north);
			}
		}
	}
}