using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
	[CustomEditor(typeof(ReadV5))]
	public class ReadV5Editor : ReadModuleTemplateEditor
	{
		SerializedProperty logicalFields;
		SerializedProperty currentStep;
		SerializedProperty enableMemoryCache;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			logicalFields = serializedObject.FindProperty("logicalFields");
			currentStep   = serializedObject.FindProperty("currentStep");
			enableMemoryCache = serializedObject.FindProperty("enableMemoryCache");
		}

		protected override void DrawDataSourceSettingsExtension()
		{
			ReadV5 mod = (ReadV5)target;

			enableMemoryCache.boolValue = EditorGUILayout.ToggleLeft(
				new GUIContent("Enable Memory Cache",
				"Caches loaded timesteps in RAM for instant playback. Disable if experiencing Out-Of-Memory on mobile/VR."), enableMemoryCache.boolValue);

			GUILayout.Space(5f);

			if (mod.ParsedNTime > 1)
			{
				EditorGUILayout.LabelField("[Time Control]", EditorStyles.boldLabel);
				GUILayout.Space(5f);

				if (currentStep != null)
				{
					EditorGUI.BeginDisabledGroup(mod.HasAnimator);
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.IntSlider(currentStep, 0, mod.ParsedNTime - 1, new GUIContent("Time Step"));

					if (EditorGUI.EndChangeCheck())
					{
						serializedObject.ApplyModifiedProperties();
						if (Application.isPlaying) mod.SetData(currentStep.intValue);
					}
					EditorGUI.EndDisabledGroup();
				}
			}
		}

		/// <summary>
		/// Overrides the base class method. Draws additional common settings, displaying parsed logical fields.
		/// </summary>
		protected override void DrawCommonSettingsExtension()
		{
			GUILayout.Space(5f);

			EditorGUILayout.LabelField("[Parsed Logical Fields]", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(logicalFields, new GUIContent("Logical Fields"), true);
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(5f);
		}

		/// <summary>
		/// Overrides the base class method to prevent rendering default custom settings at the bottom.
		/// </summary>
		protected override void DrawCustomSettingsBottom()
		{
		}
	}
#endif

	[System.Serializable]
	public class LogicalFieldInfo
	{
		public string FieldName;
		public bool IsVector;
		public int[] ElementIndices;
	}

	// =========================================================================
	// Main Class
	// =========================================================================
	public class ReadV5 : ReadModuleTemplate
	{
		public List<LogicalFieldInfo> logicalFields = new List<LogicalFieldInfo>();
		public int ParsedNTime { get; private set; } = 1;

		[Tooltip("If true, loaded timesteps are kept in RAM. Playback becomes extremely fast but consumes more memory.")]
		public bool enableMemoryCache = true;

		private V5Metadata meta;
		private List<float>[] coords;
		private int currentParsedStep = -1;
		private string cachedDataSource = "";
		private bool isGeometryInitialized = false;

		private class StepCache
		{
			public float[][] values;
			public float[] mins;
			public float[] maxs;
			public float[] averages;
			public float[] variances;
		}
		private StepCache[] stepCaches;

#if UNITY_EDITOR
		/// <summary>
		/// Resets the component to its default values and applies component reordering.
		/// </summary>
		protected override void Reset()
		{
			// Execute the base class component reordering logic.
			base.Reset();

			sourceType = DataSourceType.FILE;

			useUndefMenu      = false;
			usePrecisionMenu  = true;
			precision         = Precision.DOUBLE;
			useByteswapMenu   = true;
			useHeaderSkipMenu = true;
			currentParsedStep = -1;
			cachedDataSource  = "";
			enableMemoryCache = true;
		}
#endif

		/// <summary>
		/// Overrides the base class method. Initializes module-specific settings.
		/// </summary>
		public override void InitModule()
		{
		}

		/// <summary>
		/// Overrides the base class method. The main execution function of the module that triggers the data loading.
		/// </summary>
		public override int BodyFunc()
		{
			if (string.IsNullOrEmpty(dataSource)) return 0;

			if (dataSource == cachedDataSource && meta.dims != null)
			{
				SetData(currentStep);

				return 1;
			}

			cachedDataSource = dataSource;
			currentParsedStep = -1;
			isGeometryInitialized = false;

			StartCoroutine(Load());

			return 1;
		}

		/// <summary>
		/// Coroutine that orchestrates the VFIVE metadata and binary data loading, parsing, and application processes.
		/// </summary>
		IEnumerator Load()
		{
			string v5Text = null;
			bool hasError = false;

			Debug.Log($"[ReadV5] Loading Meta Data: {dataSource}");

			yield return StartCoroutine(FetchTextRoutine(dataSource, (text) => { v5Text = text; }, (err) => { hasError = true; }));

			if (hasError || string.IsNullOrEmpty(v5Text)) yield break;

			meta = ParseV5Metadata(v5Text);
			ParsedNTime = meta.ntime;

			if (meta.dims[0] * meta.dims[1] * meta.dims[2] == 0) yield break;

			stepCaches = new StepCache[meta.ntime];

			coords = new List<float>[4];
			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}

			for (int i = 0; i < 3; i++)
			{
				if (string.IsNullOrEmpty(meta.coordFiles[i])) continue;

				byte[] coordBytes = null;

				yield return StartCoroutine(FetchBinaryRoutine(meta.coordFiles[i], (data) => { coordBytes = data; }));

				if (coordBytes != null)
				{
					coords[i] = ParseBinaryToFloatList(coordBytes, meta.dims[i], precision, byteswap, skipHeader, headerBytes);
				}
			}

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

			int totalElements = meta.scalars.Count + (meta.vectors.Count * 3);
			df.CreateElements(totalElements);
			df.upAxis = DataField.UpAxis.Z;

			int elementCounter = 0;
			logicalFields.Clear();

			List<LogicalFieldDef> allFields = new List<LogicalFieldDef>();
			allFields.AddRange(meta.scalars.Values);
			allFields.AddRange(meta.vectors.Values);
			allFields.Sort((a, b) => a.sequence.CompareTo(b.sequence));

			foreach (var def in allFields)
			{
				var fieldInfo = new LogicalFieldInfo { FieldName = def.label, IsVector = def.isVector, ElementIndices = new int[def.isVector ? 3 : 1] };

				if (!def.isVector)
				{
					df.elements[elementCounter].SetDims(new List<int>(meta.dims));
					df.elements[elementCounter].SetCoords(coords);
					df.elements[elementCounter].SetFieldType(FieldType.RECTILINEAR);
					df.elements[elementCounter].varName = def.label.Replace("\\n", " ");
					df.elements[elementCounter].SetSteps(meta.ntime);
					fieldInfo.ElementIndices[0] = elementCounter;
					elementCounter++;
				}
				else
				{
					string[] axisNames = { " (U)", " (V)", " (W)" };

					for (int i = 0; i < 3; i++)
					{
						df.elements[elementCounter].SetDims(new List<int>(meta.dims));
						df.elements[elementCounter].SetCoords(coords);
						df.elements[elementCounter].SetFieldType(FieldType.RECTILINEAR);
						df.elements[elementCounter].varName = (def.label + axisNames[i]).Replace("\\n", " ");
						df.elements[elementCounter].SetSteps(meta.ntime);
						fieldInfo.ElementIndices[i] = elementCounter;
						elementCounter++;
					}
				}

				logicalFields.Add(fieldInfo);
			}

			InitAnimator();

			int initialStep = currentStep > 0 ? currentStep : 0;
			yield return StartCoroutine(SetDataAsync(initialStep));

			if (enableMemoryCache)
			{
				Debug.Log("[ReadV5] Starting background cache preload...");
				StartCoroutine(PreloadAllCachesRoutine());
			}
		}

		/// <summary>
		/// Asynchronously preloads uncached timesteps in the background.
		/// </summary>
		private IEnumerator PreloadAllCachesRoutine()
		{
			for (int s = 0; s < meta.ntime; s++)
			{
				if (stepCaches[s] != null) continue;

				yield return StartCoroutine(BuildCacheForStepAsync(s));
			}

			Debug.Log("[ReadV5] Background cache preload completed.");
		}

		public override void SetData(int step)
		{
			if (meta.dims == null || df.elements == null) return;
			if (step == currentParsedStep) return;

			StartCoroutine(SetDataAsync(step));
		}

		/// <summary>
		/// Asynchronously sets the data for a specific timestep, utilizing the memory cache if available.
		/// </summary>
		private IEnumerator SetDataAsync(int step)
		{
			if (step < 0 || step >= meta.ntime) yield break;

			currentParsedStep = step;
			df.dataLoaded = false;

			if (enableMemoryCache && stepCaches != null && stepCaches[step] != null)
			{
				StepCache cache = stepCaches[step];
				for (int i = 0; i < df.elements.Length; i++)
				{
					df.elements[i].values = cache.values[i];
					df.elements[i].min = cache.mins[i];
					df.elements[i].max = cache.maxs[i];
					df.elements[i].average = cache.averages[i];
					df.elements[i].variance = cache.variances[i];
					df.elements[i].SetActive(true);
				}

				ApplyLoadedData();
				yield break;
			}

			yield return StartCoroutine(BuildCacheForStepAsync(step));

			if (stepCaches[step] != null)
			{
				StepCache cache = stepCaches[step];
				for (int i = 0; i < df.elements.Length; i++)
				{
					df.elements[i].values = cache.values[i];
					df.elements[i].min = cache.mins[i];
					df.elements[i].max = cache.maxs[i];
					df.elements[i].average = cache.averages[i];
					df.elements[i].variance = cache.variances[i];
					df.elements[i].SetActive(true);
				}
			}

			ApplyLoadedData();
		}

		/// <summary>
		/// Builds a memory cache for a specific timestep using the optimized parallel routine.
		/// </summary>
		private IEnumerator BuildCacheForStepAsync(int step)
		{
			int totalGridSize = meta.dims[0] * meta.dims[1] * meta.dims[2];

			List<LogicalFieldDef> allFields = new List<LogicalFieldDef>();
			allFields.AddRange(meta.scalars.Values);
			allFields.AddRange(meta.vectors.Values);
			allFields.Sort((a, b) => a.sequence.CompareTo(b.sequence));

			string[] absolutePaths = new string[df.elements.Length];

			int elementCounter = 0;
			for (int i = 0; i < allFields.Count; i++)
			{
				var def = allFields[i];
				int numComps = def.isVector ? 3 : 1;

				for (int c = 0; c < numComps; c++)
				{
					absolutePaths[elementCounter] = def.files[step][c];
					elementCounter++;
				}
			}

			bool useWebRequest = Application.platform == RuntimePlatform.Android ||
			                     Application.platform == RuntimePlatform.WebGLPlayer ||
			                     dataSource.StartsWith("http://") || dataSource.StartsWith("https://");

			for (int i = 0; i < df.elements.Length; i++)
			{
				string path = absolutePaths[i];

				if (!string.IsNullOrEmpty(path))
				{
					if (!useWebRequest)
					{
						if (path.StartsWith("file://"))
						{
							path = new System.Uri(path).LocalPath;
						}
						else if (!Path.IsPathRooted(path))
						{
							path = Path.Combine(Application.streamingAssetsPath, path);
						}

						absolutePaths[i] = path.Replace('\\', '/');
					}
				}
			}

			float[][] parsedValues = new float[df.elements.Length][];

			// Delegate the heavy lifting to the shared parallel routine in ReadModuleTemplate
			yield return StartCoroutine(FetchParseAndCalcStatsParallelRoutine(
				absolutePaths,
				totalGridSize,
				df.elements,
				parsedValues,
				precision,
				byteswap,
				skipHeader,
				headerBytes,
				useWebRequest
			));

			StepCache newCache = new StepCache
			{
				values    = new float[df.elements.Length][],
				mins      = new float[df.elements.Length],
				maxs      = new float[df.elements.Length],
				averages  = new float[df.elements.Length],
				variances = new float[df.elements.Length]
			};

			bool cacheIsValid = false;

			for (int i = 0; i < df.elements.Length; i++)
			{
				if (parsedValues[i] != null)
				{
					newCache.values[i]    = parsedValues[i];
					newCache.mins[i]      = df.elements[i].min;
					newCache.maxs[i]      = df.elements[i].max;
					newCache.averages[i]  = df.elements[i].average;
					newCache.variances[i] = df.elements[i].variance;
					cacheIsValid = true;
				}
			}

			if (cacheIsValid && enableMemoryCache)
			{
				stepCaches[step] = newCache;
			}
		}

		protected override void ApplyLoadedData()
		{
			if (!isGeometryInitialized)
			{
				base.ApplyLoadedData();
				isGeometryInitialized = true;
			}
			else
			{
				df.dataLoaded = true;
				SetParentChangedIntoAllChildren();
			}
		}

		private class LogicalFieldDef
		{
			public int sequence;
			public bool isVector;
			public string label = "Unknown";
			public string[][] files; // [timeStep][component]

			public LogicalFieldDef(int ntime, bool vector)
			{
				isVector = vector;
				files = new string[ntime][];

				for (int i = 0; i < ntime; i++)
				{
					files[i] = new string[vector ? 3 : 1];
				}
			}
		}

		private struct V5Metadata
		{
			public int ntime;
			public int[] dims;
			public string[] coordFiles;
			public SortedDictionary<int, LogicalFieldDef> scalars;
			public SortedDictionary<int, LogicalFieldDef> vectors;

			public V5Metadata(int dummy)
			{
				ntime = 1;
				dims = new int[3];
				coordFiles = new string[3];
				scalars = new SortedDictionary<int, LogicalFieldDef>();
				vectors = new SortedDictionary<int, LogicalFieldDef>();
			}
		}

		/// <summary>
		/// Parses the VFIVE metadata text. Extracts time dimension first, then variables.
		/// </summary>
		private V5Metadata ParseV5Metadata(string text)
		{
			V5Metadata meta = new V5Metadata(0);
			string filePath = Path.GetDirectoryName(dataSource);

			// Pre-pass to find NTIME
			using (StringReader reader = new StringReader(text))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					if (line.TrimStart().StartsWith("#")) continue;

					string[] tokens = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

					if (tokens.Length == 0) continue;

					if (tokens[0].ToUpper() == "NTIME")
					{
						meta.ntime = Convert.ToInt32(tokens[1]);
						break;
					}
				}
			}

			int sequenceCounter = 0;

			// Second pass to parse everything
			using (StringReader reader = new StringReader(text))
			{
				string line;

				while ((line = reader.ReadLine()) != null)
				{
					if (line.TrimStart().StartsWith("#")) continue;

					string[] tokens = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

					if (tokens.Length == 0) continue;

					string keyword = tokens[0].ToUpper();

					if (keyword == "N1")
					{
						meta.dims[0] = Convert.ToInt32(tokens[1]);
					}
					else if (keyword == "N2")
					{
						meta.dims[1] = Convert.ToInt32(tokens[1]);
					}
					else if (keyword == "N3")
					{
						meta.dims[2] = Convert.ToInt32(tokens[1]);
					}
					else if (keyword == "NOSKIP4")
					{
						if (Convert.ToInt32(tokens[1]) == 1)
						{
							skipHeader = false;
						}
					}
					else if (keyword == "XFILE")
					{
						meta.coordFiles[0] = GetAdjustedPath(filePath, tokens[1]);
					}
					else if (keyword == "YFILE")
					{
						meta.coordFiles[1] = GetAdjustedPath(filePath, tokens[1]);
					}
					else if (keyword == "ZFILE")
					{
						meta.coordFiles[2] = GetAdjustedPath(filePath, tokens[1]);
					}

					// Parse labels like SCAL0_LABEL or VECT0_LABEL
					Match mLabel = Regex.Match(keyword, @"^(SCAL|VECT)(\d+)_LABEL$");
					if (mLabel.Success)
					{
						string type = mLabel.Groups[1].Value;
						int idx = int.Parse(mLabel.Groups[2].Value);

						if (type == "SCAL")
						{
							if (!meta.scalars.ContainsKey(idx))
							{
								meta.scalars[idx] = new LogicalFieldDef(meta.ntime, false) { sequence = sequenceCounter++ };
							}
							meta.scalars[idx].label = tokens[1];
						}
						else if (type == "VECT")
						{
							if (!meta.vectors.ContainsKey(idx))
							{
								meta.vectors[idx] = new LogicalFieldDef(meta.ntime, true) { sequence = sequenceCounter++ };
							}
							meta.vectors[idx].label = tokens[1];
						}
						continue;
					}

					// Parse data files like SCAL0, SCAL0T1, VECT0X, VECT0XT1
					Match mData = Regex.Match(keyword, @"^(SCAL|VECT)(\d+)(X|Y|Z)?(?:T(\d+))?$");

					if (mData.Success && keyword != "SCALE" && keyword != "NSCAL" && keyword != "NVEC")
					{
						string type = mData.Groups[1].Value;
						int idx = int.Parse(mData.Groups[2].Value);
						string comp = mData.Groups[3].Value;
						int tStep = mData.Groups[4].Success ? int.Parse(mData.Groups[4].Value) : 0;

						if (tStep >= meta.ntime) continue;

						if (type == "SCAL")
						{
							if (!meta.scalars.ContainsKey(idx))
							{
								meta.scalars[idx] = new LogicalFieldDef(meta.ntime, false) { sequence = sequenceCounter++ };
							}

							meta.scalars[idx].files[tStep][0] = GetAdjustedPath(filePath, tokens[1]);
						}
						else if (type == "VECT")
						{
							if (!meta.vectors.ContainsKey(idx))
							{
								meta.vectors[idx] = new LogicalFieldDef(meta.ntime, true) { sequence = sequenceCounter++ };
							}

							int compIdx = 0;

							if (comp == "Y")
							{
								compIdx = 1;
							}
							else if (comp == "Z")
							{
								compIdx = 2;
							}

							meta.vectors[idx].files[tStep][compIdx] = GetAdjustedPath(filePath, tokens[1]);
						}
					}
				}
			}

			return meta;
		}

		/// <summary>
		/// Adjusts relative file paths defined in the metadata to absolute paths.
		/// </summary>
		private string GetAdjustedPath(string basePath, string subPath)
		{
			string p = subPath.StartsWith("./") ? subPath.Remove(0, 2) : subPath;

			return Path.Combine(basePath, p).Replace('\\', '/');
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
	}
}