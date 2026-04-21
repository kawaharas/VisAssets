using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.DataLoader
{
	using ModuleState = Activation.ModuleState;
	using FieldType   = DataElement.FieldType;

	// =========================================================================
	// Editor Extension
	// =========================================================================
	#if UNITY_EDITOR
	[CustomEditor(typeof(ReadGrADS))]
	public class ReadGrADSEditor : ReadModuleTemplateEditor
	{
		SerializedProperty logicalFields;
		SerializedProperty upAxis;
		SerializedProperty currentStep;
		SerializedProperty zUnit;
		SerializedProperty zScale;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			logicalFields = serializedObject.FindProperty("logicalFields");
			upAxis        = serializedObject.FindProperty("upAxis");
			currentStep   = serializedObject.FindProperty("currentStep");
			zUnit         = serializedObject.FindProperty("zUnit");
			zScale        = serializedObject.FindProperty("zScale");
		}

		/// <summary>
		/// Overrides the base class method. Draws additional data source settings, including the time step slider.
		/// </summary>
		protected override void DrawDataSourceSettingsExtension()
		{
			EditorGUILayout.PropertyField(upAxis, new GUIContent("Up Axis"));

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(zUnit, new GUIContent("Z Unit"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(zScale, new GUIContent("Z Scale"));

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();

				if (Application.isPlaying)
				{
					ReadGrADS modRef = (ReadGrADS)target;
					modRef.UpdateScaling();
				}
			}

			ReadGrADS mod = (ReadGrADS)target;

			if (mod.ParsedTimeInfo != null && mod.ParsedTimeInfo.Steps > 1)
			{
				GUILayout.Space(5f);

				EditorGUILayout.LabelField("[Time Control]", EditorStyles.boldLabel);

				if (currentStep != null)
				{
					EditorGUI.BeginDisabledGroup(mod.HasAnimator);

					EditorGUI.BeginChangeCheck();

					EditorGUILayout.IntSlider(currentStep, 0, mod.ParsedTimeInfo.Steps - 1, new GUIContent("Time Step"));

					if (EditorGUI.EndChangeCheck())
					{
						serializedObject.ApplyModifiedProperties();

						if (Application.isPlaying)
						{
							mod.SetData(currentStep.intValue);
						}
					}

					EditorGUI.EndDisabledGroup();

					if (mod.ParsedTimeInfo.StartTime != DateTime.MinValue)
					{
						DateTime t = mod.ParsedTimeInfo.GetTimeAtStep(currentStep.intValue);
						EditorGUILayout.LabelField("Current Time", t.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC)");
					}
				}
			}
		}

		/// <summary>
		/// Overrides the base class method. Draws additional common settings, displaying parsed variables.
		/// </summary>
		protected override void DrawCommonSettingsExtension()
		{
			EditorGUILayout.LabelField("[Parsed GrADS Variables]", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(logicalFields, new GUIContent("Variables"), true);
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(5f);
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	public class ReadGrADS : ReadModuleTemplate
	{
		public enum ZUnit
		{
			METERS,
			KILOMETERS
		}

		public enum TimeUnit
		{
			Year,
			Month,
			Day,
			Hour,
			Minute,
			Second
		}

		[SerializeField]
		public float[] offsets;

		public ZUnit zUnit = ZUnit.METERS;

		[Range(1f, 1000f)]
		public float zScale = 1.0f;

		public double ScaleRatioPerMeter { get; private set; } = 1.0;

		private byte[] bytedata;
		private GrADSMeta meta;
		private List<float>[] coords;

		public int currentParsedStep = -1;
		private string cachedDataSource = "";
		private bool isGeometryInitialized = false;

		// --- Caching variables for safe scaling updates ---
		private double distX = 1.0;
		private double distY = 1.0;
		private double axis_min_z = 0.0;
		private double axis_max_z = 0.0;
		private float axis_width_x = 1f;
		private float axis_width_y = 1f;
		private float axis_width_z = 1f;
		private float baseScaleX = 1f;
		private float prev_zScale = 1.0f;
		private ZUnit prev_zUnit = ZUnit.METERS;

		public List<LogicalFieldInfo> logicalFields = new List<LogicalFieldInfo>();
		public TimeInfo ParsedTimeInfo { get; private set; }

		[System.Serializable]
		public class LogicalFieldInfo
		{
			public string VarName;
			public int Levels;
			public string Description;
		}

		[System.Serializable]
		public struct TimeIncrement
		{
			public int Value;
			public TimeUnit Unit;
		}

		[System.Serializable]
		public class TimeInfo
		{
			public DateTime StartTime;
			public TimeIncrement Increment;
			public int Steps;

			/// <summary>
			/// Calculates the corresponding datetime for a specific simulation step based on the configured increments.
			/// </summary>
			public DateTime GetTimeAtStep(int step)
			{
				DateTime t = StartTime;

				switch (Increment.Unit)
				{
					case TimeUnit.Year:   return t.AddYears(Increment.Value * step);
					case TimeUnit.Month:  return t.AddMonths(Increment.Value * step);
					case TimeUnit.Day:    return t.AddDays(Increment.Value * step);
					case TimeUnit.Hour:   return t.AddHours(Increment.Value * step);
					case TimeUnit.Minute: return t.AddMinutes(Increment.Value * step);
					case TimeUnit.Second: return t.AddSeconds(Increment.Value * step);
					default:              return t;
				}
			}
		}

		/// <summary>
		/// Resets the component to its default values.
		/// </summary>
		protected override void Reset()
		{
#if UNITY_EDITOR
			base.Reset();
#endif
			sourceType = DataSourceType.FILE;
			useUndefMenu      = false;
			usePrecisionMenu  = false;
			precision         = Precision.SINGLE;
			useByteswapMenu   = false;
			useHeaderSkipMenu = false;
			upAxis = DataField.UpAxis.Z;
			currentParsedStep = -1;
			cachedDataSource = "";
			isGeometryInitialized = false;
			zUnit  = ZUnit.METERS;
			zScale = 1.0f;
		}

		/// <summary>
		/// Initializes module-specific settings such as initial offsets.
		/// </summary>
		public override void InitModule()
		{
			offsets = new float[3];
		}

		/// <summary>
		/// The main execution function of the module that triggers the data loading.
		/// </summary>
		public override int BodyFunc()
		{
			if (string.IsNullOrEmpty(dataSource)) return 0;

			if (dataSource == cachedDataSource && bytedata != null)
			{
				SetData(currentStep);
				return 1;
			}

			cachedDataSource = dataSource;
			currentParsedStep = -1;

			StartCoroutine(Load());

			return 1;
		}

		private void OnValidate()
		{
			if (Application.isPlaying && isGeometryInitialized && df != null && df.dataLoaded)
			{
				UpdateScaling();
			}
		}

		/// <summary>
		/// Dynamically recalculates and applies the Z-axis scaling transformation.
		/// Preserves the Right-Handed to Left-Handed conversion sign established by the framework.
		/// </summary>
		public void UpdateScaling()
		{
			if (!isGeometryInitialized) return;

			// Preserve the coordinate system conversion sign (-1 or 1)
			float currentZSign = Mathf.Sign(transform.localScale.z);

			double distZ_raw = axis_max_z - axis_min_z;
			double distZ_meters = (zUnit == ZUnit.KILOMETERS) ? distZ_raw * 1000.0 : distZ_raw;

			double maxDist = Math.Max(Math.Max(distX, distY), distZ_meters);

			ScaleRatioPerMeter = 10.0 / maxDist;

			float baseNewScaleX = 10f / axis_width_x * (float)(distX / maxDist);
			float baseNewScaleY = 10f / axis_width_y * (float)(distY / maxDist);
			float baseNewScaleZ = 10f / axis_width_z * (float)(distZ_meters / maxDist);

			float currentOverallScale = 1.0f;

			if (baseScaleX > 0.0001f)
			{
				currentOverallScale = Mathf.Abs(transform.localScale.x / baseScaleX);
			}

			baseScaleX = baseNewScaleX;

			float finalScaleX = baseNewScaleX * currentOverallScale;
			float finalScaleY = baseNewScaleY * currentOverallScale;
			// Apply the preserved sign to keep the correct orientation
			float finalScaleZ = baseNewScaleZ * zScale * currentOverallScale * currentZSign;

			transform.localScale = new Vector3(finalScaleX, finalScaleY, finalScaleZ);

			SetParentChangedIntoAllChildren();
		}

		/// <summary>
		/// Updates the vertical scaling dynamically from external UI scripts.
		/// </summary>
		public void SetZScale(float scale)
		{
			zScale = scale;

			if (activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Updates the unit of the vertical axis dynamically from external UI scripts.
		/// </summary>
		public void SetZUnit(int unitIndex)
		{
			zUnit = (ZUnit)unitIndex;

			if (activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Manually triggers a parameter change notification to force an update.
		/// </summary>
		public void Exec()
		{
			if (!string.IsNullOrEmpty(dataSource))
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Coroutine that orchestrates the GrADS data loading, metadata parsing, and background data processing.
		/// </summary>
		IEnumerator Load()
		{
			if (dataSource == cachedDataSource && bytedata != null)
			{
				int step = currentStep > 0 ? currentStep : 0;
				yield return StartCoroutine(SetDataAsync(step));
				yield break;
			}

			cachedDataSource = dataSource;
			currentParsedStep = -1;
			isGeometryInitialized = false;

			string ctlText = null;
			bool hasError = false;

			Debug.Log($"[ReadGrADS] Loading Control File: {dataSource}");

			yield return StartCoroutine(FetchTextRoutine(dataSource, (text) => { ctlText = text; }, (err) => { hasError = true; }));

			if (hasError || string.IsNullOrEmpty(ctlText)) yield break;

			meta = ParseCtl(ctlText);

			if (meta.dims[0] * meta.dims[1] * meta.dims[2] == 0) yield break;

			this.ParsedTimeInfo = meta.timeInfo;

			useUndef = meta.useUndef;
			undef = meta.undef;
			byteswap = meta.byteswap;
			skipHeader = meta.headerBytes > 0;
			headerBytes = meta.headerBytes;

			coords = new List<float>[4];
			for (int i = 0; i < 3; i++)
			{
				coords[i] = new List<float>(meta.coords[i]);
			}

			coords[3] = new List<float>();
			for (int k = 0; k < meta.dims[2]; k++)
			{
				for (int j = 0; j < meta.dims[1]; j++)
				{
					for (int i = 0; i < meta.dims[0]; i++)
					{
						coords[3].Add(coords[0][i]);
						coords[3].Add(coords[1][j]);
						coords[3].Add(coords[2][k]);
					}
				}
			}

			df.CreateElements(meta.varInfo.Count);
			df.upAxis = this.upAxis;
			df.coordinateSystem = DataField.CoordinateSystem.RIGHT_HANDED;
			logicalFields.Clear();

			for (int i = 0; i < meta.varInfo.Count; i++)
			{
				int levels = meta.varInfo[i].levs == 0 ? 1 : meta.varInfo[i].levs;

				if (levels == meta.dims[2])
				{
					df.elements[i].SetDims(new List<int> { meta.dims[0], meta.dims[1], meta.dims[2] });
					df.elements[i].SetCoords(coords);
				}
				else
				{
					List<float>[] coords_tmp = new List<float>[4];

					coords_tmp[0] = new List<float>(coords[0]);
					coords_tmp[1] = new List<float>(coords[1]);
					coords_tmp[2] = new List<float>();
					coords_tmp[3] = new List<float>();

					for (int n = 0; n < levels; n++)
					{
						coords_tmp[2].Add(coords[2][n]);
					}

					for (int z = 0; z < levels; z++)
					{
						for (int y = 0; y < meta.dims[1]; y++)
						{
							for (int x = 0; x < meta.dims[0]; x++)
							{
								coords_tmp[3].Add(coords[0][x]);
								coords_tmp[3].Add(coords[1][y]);
								coords_tmp[3].Add(coords_tmp[2][z]);
							}
						}
					}

					df.elements[i].SetDims(new List<int> { meta.dims[0], meta.dims[1], levels });
					df.elements[i].SetCoords(coords_tmp);
				}

				df.elements[i].SetSteps(meta.dims[3]);
				df.elements[i].SetFieldType(FieldType.RECTILINEAR);
				df.elements[i].varName = meta.varInfo[i].description;

				if (useUndef)
				{
					df.elements[i].SetUndef(undef);
				}

				df.elements[i].SetActive(true);

				logicalFields.Add(new LogicalFieldInfo {
					VarName = meta.varInfo[i].varName,
					Levels = levels,
					Description = meta.varInfo[i].description
				});
			}

			InitAnimator();

			Debug.Log($"[ReadGrADS] Loading Binary Data: {meta.dataFile}");

			bool useWebRequest = Application.platform == RuntimePlatform.Android ||
								 Application.platform == RuntimePlatform.WebGLPlayer ||
								 meta.dataFile.Contains("://");

			if (useWebRequest)
			{
				yield return StartCoroutine(FetchBinaryRoutine(meta.dataFile, (data) => { bytedata = data; }));
			}
			else
			{
				string absolutePath = meta.dataFile;

				if (!Path.IsPathRooted(absolutePath))
				{
					absolutePath = Path.Combine(Application.streamingAssetsPath, absolutePath);
				}

				Task<byte[]> loadTask = Task.Run(() => File.ReadAllBytes(absolutePath));
				yield return new WaitUntil(() => loadTask.IsCompleted);

				if (loadTask.Exception != null)
				{
					Debug.LogError($"[ReadGrADS] Load Error: {loadTask.Exception.InnerException.Message}");
				}
				else
				{
					bytedata = loadTask.Result;
				}
			}

			if (bytedata != null)
			{
				Debug.Log("[ReadGrADS] Data loaded. Calculating Global Statistics...");

				yield return StartCoroutine(CalculateGlobalStatsCoroutine());

				Debug.Log("[ReadGrADS] Statistics ready. Parsing initial step...");

				int step = currentStep > 0 ? currentStep : 0;
				currentParsedStep = -1;

				yield return StartCoroutine(SetDataAsync(step));
			}
		}

		/// <summary>
		/// Applies GrADS-specific coordinate offsets and scales before notifying downstream modules.
		/// Prevents overwriting geometry transformations on continuous parameter updates.
		/// </summary>
		protected override void ApplyLoadedData()
		{
			df.dataLoaded = true;

			if (!isGeometryInitialized)
			{
				CalcOffsets();
				SetCoordinateSystem();
				isGeometryInitialized = true;
				prev_zScale = zScale;
				prev_zUnit  = zUnit;
			}

			SetParentChangedIntoAllChildren();
		}

		/// <summary>
		/// Iterates through all time steps in the binary data to pre-calculate global minimum and maximum values for consistent color mapping.
		/// </summary>
		private IEnumerator CalculateGlobalStatsCoroutine()
		{
			DataElement[] elements = df.elements;
			int numVars = elements.Length;
			int numSteps = meta.dims[3] > 0 ? meta.dims[3] : 1;
			int elementsPerPlane = meta.dims[0] * meta.dims[1];
			int bytesPerPlane = elementsPerPlane * 4;
			int fortranMarkerSize = meta.isSequential ? 4 : 0;

			int skipBytesPerStep = 0;

			for (int i = 0; i < numVars; i++)
			{
				skipBytesPerStep += (elements[i].size / elementsPerPlane) * (fortranMarkerSize + bytesPerPlane + fortranMarkerSize);
			}

			Precision prec = precision;
			bool bSwap = byteswap;
			int hBytes = meta.headerBytes;

			Task statsTask = Task.Run(() =>
			{
				float[]  gMins   = new float[numVars];
				float[]  gMaxs   = new float[numVars];
				double[] gSums   = new double[numVars];
				double[] gSumSqs = new double[numVars];
				long[]   gCounts = new long[numVars];

				for (int i = 0; i < numVars; i++)
				{
					gMins[i] = float.MaxValue;
					gMaxs[i] = float.MinValue;
				}

				for (int step = 0; step < numSteps; step++)
				{
					int offset = hBytes + (skipBytesPerStep * step);

					for (int i = 0; i < numVars; i++)
					{
						int   levels   = elements[i].size / elementsPerPlane;
						bool  chkUndef = elements[i].useUndef;
						float uVal     = elements[i].undef;

						for (int z = 0; z < levels; z++)
						{
							offset += fortranMarkerSize;
							List<float> plane = ParseBinaryToFloatList(bytedata, elementsPerPlane, prec, bSwap, true, offset);

							AccumulateStats(plane, chkUndef, uVal, ref gMins[i], ref gMaxs[i], ref gSums[i], ref gSumSqs[i], ref gCounts[i]);

							offset += bytesPerPlane + fortranMarkerSize;
						}
					}
				}

				for (int i = 0; i < numVars; i++)
				{
					if (gCounts[i] > 0)
					{
						elements[i].min = gMins[i];
						elements[i].max = gMaxs[i];
						elements[i].average  = (float)(gSums[i] / gCounts[i]);
						elements[i].variance = (float)((gSumSqs[i] / gCounts[i]) - (elements[i].average * elements[i].average));
					}
				}
			});

			yield return new WaitUntil(() => statsTask.IsCompleted);

			if (statsTask.Exception != null)
			{
				Debug.LogError($"[ReadGrADS] Global Stats Error: {statsTask.Exception.InnerException?.Message}");
			}
		}

		/// <summary>
		/// Prepares the module to load and apply data for a specific time step.
		/// </summary>
		public override void SetData(int step)
		{
			if (bytedata == null || df.elements == null) return;
			if (step == currentParsedStep) return;

			currentParsedStep = step;
			StartCoroutine(SetDataAsync(step));
		}

		/// <summary>
		/// Asynchronously loads, parses, and applies the data for a specified time step without blocking the main thread.
		/// </summary>
		private IEnumerator SetDataAsync(int step)
		{
			df.dataLoaded = false;

			int dataLength = 4;
			int elementsPerPlane = meta.dims[0] * meta.dims[1];
			int bytesPerPlane = elementsPerPlane * dataLength;
			int fortranMarkerSize = meta.isSequential ? 4 : 0;

			int skipBytesPerStep = 0;

			for (int i = 0; i < df.elements.Length; i++)
			{
				int levels = df.elements[i].size / elementsPerPlane;
				skipBytesPerStep += levels * (fortranMarkerSize + bytesPerPlane + fortranMarkerSize);
			}

			int currentOffset = meta.headerBytes + (skipBytesPerStep * step);
			List<float>[] parsedValues = new List<float>[df.elements.Length];

			Task parseTask = Task.Run(() =>
			{
				for (int i = 0; i < df.elements.Length; i++)
				{
					int levels = df.elements[i].size / elementsPerPlane;
					List<float> varValues = new List<float>(df.elements[i].size);
					List<List<float>> planes = new List<List<float>>(levels);

					for (int z = 0; z < levels; z++)
					{
						currentOffset += fortranMarkerSize;

						List<float> planeValues = ParseBinaryToFloatList(
							bytedata,
							elementsPerPlane,
							Precision.SINGLE,
							byteswap,
							true,
							currentOffset
						);

						if (meta.isYRev)
						{
							int xSize = meta.dims[0];
							int ySize = meta.dims[1];
							List<float> yRevPlane = new List<float>(planeValues.Capacity);

							for (int y = ySize - 1; y >= 0; y--)
							{
								int startIndex = y * xSize;

								for (int x = 0; x < xSize; x++)
								{
									yRevPlane.Add(planeValues[startIndex + x]);
								}
							}
							planeValues = yRevPlane;
						}

						planes.Add(planeValues);

						currentOffset += bytesPerPlane;
						currentOffset += fortranMarkerSize;
					}

					if (meta.isZRev)
					{
						planes.Reverse();
					}

					for (int p = 0; p < planes.Count; p++)
					{
						varValues.AddRange(planes[p]);
					}

					parsedValues[i] = varValues;
				}
			});

			yield return new WaitUntil(() => parseTask.IsCompleted);

			if (parseTask.Exception != null)
			{
				Debug.LogError($"[ReadGrADS] Parse Error: {parseTask.Exception.InnerException.Message}");
				yield break;
			}

			for (int i = 0; i < df.elements.Length; i++)
			{
				df.elements[i].SetValues(parsedValues[i]);
			}

			ApplyLoadedData();
		}

		/// <summary>
		/// Calculates the geometric offsets and initial scaling factors based on physical distances.
		/// Caches the dimensions and base scales required for dynamic Z-axis updates.
		/// </summary>
		private void CalcOffsets()
		{
			float[] axis_min   = new float[3];
			float[] axis_max   = new float[3];
			float[] axis_width = new float[3];

			for (int i = 0; i < 3; i++)
			{
				float v0 = coords[i][0];
				float v1 = coords[i][meta.dims[i] - 1];
				axis_width[i] = Mathf.Abs(v1 - v0);
				offsets[i] = v0 + (v1 - v0) / 2f;
				axis_min[i] = coords[i].Min();
				axis_max[i] = coords[i].Max();
			}

			axis_width_x = axis_width[0];
			axis_width_y = axis_width[1];
			axis_width_z = axis_width[2];
			axis_min_z = axis_min[2];
			axis_max_z = axis_max[2];

			double EARTH_RADIUS_NS_WGS84 = 6356752.3142;
			double EARTH_RADIUS_WE_WGS84 = 6378137.0;

			distX = 2.0 * Math.PI * EARTH_RADIUS_WE_WGS84
					* Math.Cos((axis_min[1] + (axis_max[1] - axis_min[1]) / 2.0) / 180.0 * Math.PI)
					/ 360.0 * (axis_max[0] - axis_min[0]);

			distY = 2.0 * Math.PI * EARTH_RADIUS_NS_WGS84 / 360.0 * (axis_max[1] - axis_min[1]);

			double distZ_raw = axis_max_z - axis_min_z;
			double distZ_meters = (zUnit == ZUnit.KILOMETERS) ? distZ_raw * 1000.0 : distZ_raw;

			double maxDist = Math.Max(Math.Max(distX, distY), distZ_meters);

			ScaleRatioPerMeter = 10.0 / maxDist;

			float ScaleX = 10f / axis_width_x * (float)(distX / maxDist);
			float ScaleY = 10f / axis_width_y * (float)(distY / maxDist);
			float ScaleZ = 10f / axis_width_z * (float)(distZ_meters / maxDist) * zScale;

			baseScaleX = ScaleX;

			foreach (Transform child in transform)
			{
				child.gameObject.transform.localPosition = new Vector3(-offsets[0], -offsets[1], -offsets[2]);
			}

			transform.localScale = new Vector3(ScaleX, ScaleY, ScaleZ);
		}

		private struct VarInfo
		{
			public string varName;
			public int levs;
			public string description;
		}

		private class GrADSMeta
		{
			public int[] dims = new int[4];
			public List<float>[] coords = new List<float>[3];
			public string dataFile = "";
			public bool useUndef = false;
			public float undef = 0f;
			public int headerBytes = 0;
			public bool byteswap = false;
			public bool isSequential = false;
			public List<VarInfo> varInfo = new List<VarInfo>();

			public bool isZRev = false;
			public bool isYRev = false;
			public bool zInvertedScale = false;

			public TimeInfo timeInfo = new TimeInfo();

			public GrADSMeta()
			{
				for (int i = 0; i < 3; i++)
				{
					coords[i] = new List<float>();
				}
			}
		}

		/// <summary>
		/// Parses the GrADS control (CTL) file to extract metadata such as dimensions, variables, and time information.
		/// </summary>
		private GrADSMeta ParseCtl(string text)
		{
			GrADSMeta m = new GrADSMeta();
			string filePath = Path.GetDirectoryName(dataSource);

			using (StringReader reader = new StringReader(text))
			{
				string line;

				while ((line = reader.ReadLine()) != null)
				{
					if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("*")) continue;

					string[] tokens = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
					string keyword = tokens[0].ToUpper();

					if (keyword == "DSET")
					{
						string dsetPath = tokens[1].Replace("^", "");
						m.dataFile = Path.Combine(filePath, dsetPath).Replace('\\', '/');
					}
					else if (keyword == "UNDEF")
					{
						m.useUndef = true;
						m.undef = Convert.ToSingle(tokens[1]);
					}
					else if (keyword == "XDEF")
					{
						ParseCoord(0, tokens, reader, m);
					}
					else if (keyword == "YDEF")
					{
						ParseCoord(1, tokens, reader, m);
					}
					else if (keyword == "ZDEF")
					{
						ParseCoord(2, tokens, reader, m);
					}
					else if (keyword == "TDEF")
					{
						m.dims[3] = Convert.ToInt32(tokens[1]);
						m.timeInfo = ParseTdef(tokens);
					}
					else if (keyword == "FILEHEADER")
					{
						m.headerBytes = Convert.ToInt32(tokens[1]);
					}
					else if (keyword == "OPTIONS")
					{
						for (int n = 1; n < tokens.Length; n++)
						{
							string opt = tokens[n].ToUpper();

							if (opt == "BYTESWAPPED" || opt == "BIG_ENDIAN" || opt == "CRAY_32BIT_IEEE")
							{
								m.byteswap = true;
							}
							else if (opt == "SEQUENTIAL")
							{
								m.isSequential = true;
							}
							else if (opt == "ZREV")
							{
								m.isZRev = !m.isZRev;
							}
							else if (opt == "YREV")
							{
								m.isYRev = !m.isYRev;
							}
						}
					}
					else if (keyword == "VARS")
					{
						int varnum = Convert.ToInt32(tokens[1]);

						for (int n = 0; n < varnum; n++)
						{
							line = reader.ReadLine();

							if (line == null) break;

							string[] vTokens = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

							m.varInfo.Add(new VarInfo {
								varName = vTokens[0],
								levs = Convert.ToInt32(vTokens[1]),
								description = string.Join(" ", vTokens, 3, vTokens.Length - 3)
							});
						}
					}
				}
			}

			return m;
		}

		/// <summary>
		/// Parses coordinate mapping information from the control file.
		/// </summary>
		private void ParseCoord(int axisIdx, string[] tokens, StringReader reader, GrADSMeta m)
		{
			m.dims[axisIdx] = Convert.ToInt32(tokens[1]);
			string mapping = tokens[2].ToUpper();

			if (mapping == "LINEAR")
			{
				double start = Convert.ToDouble(tokens[3]);
				double delta = Convert.ToDouble(tokens[4]);

				for (int n = 0; n < m.dims[axisIdx]; n++)
				{
					m.coords[axisIdx].Add((float)(start + (double)n * delta));
				}
			}
			else if (mapping == "LEVELS")
			{
				int count = 0;

				for (int n = 3; n < tokens.Length; n++)
				{
					m.coords[axisIdx].Add(Convert.ToSingle(tokens[n]));
					count++;
				}

				while (count < m.dims[axisIdx])
				{
					string line = reader.ReadLine();

					if (line == null) break;

					string[] nextTokens = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

					foreach (var token in nextTokens)
					{
						m.coords[axisIdx].Add(Convert.ToSingle(token));
						count++;
					}
				}
			}

			if (axisIdx == 2 && m.coords[axisIdx].Count > 1)
			{
				if (m.coords[axisIdx][0] > m.coords[axisIdx][m.coords[axisIdx].Count - 1])
				{
					m.isZRev = !m.isZRev;
					m.coords[axisIdx].Reverse();
					m.zInvertedScale = true;
					Debug.Log("m.isZRev = !m.isZRev");
				}
			}
		}

		/// <summary>
		/// Parses time definition (TDEF) information from the control file.
		/// </summary>
		private TimeInfo ParseTdef(string[] tokens)
		{
			if (tokens.Length < 5) return new TimeInfo();

			TimeInfo info = new TimeInfo();
			info.Steps = int.Parse(tokens[1]);

			string startStr = tokens[3].ToUpper();
			string incStr = tokens[4].ToLower();

			var incMatch = Regex.Match(incStr, @"^(\d+)(yr|mo|dy|hr|mn|sc)$");

			if (incMatch.Success)
			{
				info.Increment.Value = int.Parse(incMatch.Groups[1].Value);
				switch (incMatch.Groups[2].Value)
				{
					case "yr":
						info.Increment.Unit = TimeUnit.Year;
						break;
					case "mo":
						info.Increment.Unit = TimeUnit.Month;
						break;
					case "dy":
						info.Increment.Unit = TimeUnit.Day;
						break;
					case "hr":
						info.Increment.Unit = TimeUnit.Hour;
						break;
					case "mn":
						info.Increment.Unit = TimeUnit.Minute;
						break;
					case "sc":
						info.Increment.Unit = TimeUnit.Second;
						break;
				}
			}

			var startMatch = Regex.Match(startStr, @"^(?:(?:(\d{1,2})(?::(\d{1,2}))?(?::(\d{1,2}))?)?Z)?(?:(\d{1,2}))?([A-Z]{3})(\d{2,4})$");

			if (startMatch.Success)
			{
				int hour   = startMatch.Groups[1].Success ? int.Parse(startMatch.Groups[1].Value) : 0;
				int minute = startMatch.Groups[2].Success ? int.Parse(startMatch.Groups[2].Value) : 0;
				int second = startMatch.Groups[3].Success ? int.Parse(startMatch.Groups[3].Value) : 0;
				int day    = startMatch.Groups[4].Success ? int.Parse(startMatch.Groups[4].Value) : 1;
				string monthStr = startMatch.Groups[5].Value;
				int year   = int.Parse(startMatch.Groups[6].Value);

				if (year < 100)
				{
					year += 1950;
				}

				int month = 1;
				string[] months = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

				for (int i = 0; i < months.Length; i++)
				{
					if (months[i] == monthStr)
					{
						month = i + 1;
						break;
					}
				}

				try
				{
					info.StartTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
				}
				catch (Exception e)
				{
					Debug.LogWarning($"[ReadGrADS] Invalid TDEF DateTime: {startStr} -> {e.Message}");
					info.StartTime = DateTime.MinValue;
				}
			}

			return info;
		}
	}
}