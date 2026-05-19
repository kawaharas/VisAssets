using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets
{
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(ReadModuleTemplate), true)]
	public class ReadModuleTemplateEditor : ModuleTemplateEditor
	{
		// DataSourceSettings
		protected SerializedProperty useStreamingAssets;
		protected SerializedProperty sourceType;
		protected SerializedProperty dataSource;

		// DataFormatSettings
		protected SerializedProperty usePrecisionMenu;
		protected SerializedProperty useByteswapMenu;
		protected SerializedProperty useHeaderSkipMenu;
		protected SerializedProperty precision;
		protected SerializedProperty byteswap;
		protected SerializedProperty skipHeader;
		protected SerializedProperty headerBytes;

		// CommonSettings
		protected SerializedProperty loadAtStartup;
		protected SerializedProperty useUndefMenu;
		protected SerializedProperty useUndef;
		protected SerializedProperty undef;
		protected SerializedProperty centering;
		protected SerializedProperty autoResize;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			useStreamingAssets = serializedObject.FindProperty("useStreamingAssets");
			sourceType         = serializedObject.FindProperty("sourceType");
			dataSource         = serializedObject.FindProperty("dataSource");

			usePrecisionMenu   = serializedObject.FindProperty("usePrecisionMenu");
			useByteswapMenu    = serializedObject.FindProperty("useByteswapMenu");
			useHeaderSkipMenu  = serializedObject.FindProperty("useHeaderSkipMenu");
			precision          = serializedObject.FindProperty("precision");
			byteswap           = serializedObject.FindProperty("byteswap");
			skipHeader         = serializedObject.FindProperty("skipHeader");
			headerBytes        = serializedObject.FindProperty("headerBytes");

			loadAtStartup      = serializedObject.FindProperty("loadAtStartup");
			useUndefMenu       = serializedObject.FindProperty("useUndefMenu");
			useUndef           = serializedObject.FindProperty("useUndef");
			undef              = serializedObject.FindProperty("undef");
			centering          = serializedObject.FindProperty("centering");
			autoResize         = serializedObject.FindProperty("autoResize");
		}

		/// <summary>
		/// Overrides the base class method. Renders the custom inspector GUI.
		/// </summary>
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			DrawCustomSettingsTop();
			DrawDataSourceSettings();
			DrawDataSourceSettingsExtension();
			DrawDataFormatSettings();
			DrawDataFormatSettingsExtension();
			DrawCommonSettings();
			DrawCommonSettingsExtension();
			DrawCustomSettingsBottom();

			DrawProp(uiPrefab, "UI Prefab", 5f);

			if (EditorGUI.EndChangeCheck())
			{
				EditorUtility.SetDirty(target);
			}
			serializedObject.ApplyModifiedProperties();
		}

		/// <summary>
		/// Virtual method to draw custom settings at the top of the inspector.
		/// </summary>
		protected virtual void DrawCustomSettingsTop()
		{
		}

		/// <summary>
		/// Virtual method to draw data source settings.
		/// </summary>
		protected virtual void DrawDataSourceSettings()
		{
			GUILayout.Space(5f);

			EditorGUILayout.LabelField("[Data Source Settings]", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			DrawToggle(useStreamingAssets, "Use StreamingAssets", 5f);

			string typeStr = sourceType.enumValueIndex == (int)ReadModuleTemplate.DataSourceType.FOLDER ? "Folder" : "File";
			string locationStr = useStreamingAssets.boolValue ? "Relative to StreamingAssets" : "Absolute Path / URL";

			EditorGUILayout.LabelField($"{typeStr} Name ({locationStr}) :");

			GUILayout.Space(5f);

			EditorGUI.indentLevel++;
			dataSource.stringValue = EditorGUILayout.TextField(dataSource.stringValue);
			EditorGUI.indentLevel--;

			GUILayout.Space(5f);

		}

		/// <summary>
		/// Virtual method to draw additional data source settings.
		/// </summary>
		protected virtual void DrawDataSourceSettingsExtension()
		{
		}

		/// <summary>
		/// Virtual method to draw data format settings.
		/// </summary>
		protected virtual void DrawDataFormatSettings()
		{
			// Show this section if there is at least one visible element
			bool showSection = (usePrecisionMenu  != null &&  usePrecisionMenu.boolValue) ||
							   (useByteswapMenu   != null &&   useByteswapMenu.boolValue) ||
							   (useHeaderSkipMenu != null && useHeaderSkipMenu.boolValue);

			if (!showSection) return;

			GUILayout.Space(5f);

			EditorGUILayout.LabelField("[Data Format Settings]", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			if (usePrecisionMenu.boolValue)
			{
				DrawProp(precision, "Precision", 5f);
			}

			if (useByteswapMenu.boolValue)
			{
				DrawToggle(byteswap, "Byte Swap (Endianness)", 5f);
			}

			if (useHeaderSkipMenu.boolValue)
			{
				DrawToggle(skipHeader, "Skip Header / Record Length", 5f);
				bool isSkipDisabled = !skipHeader.boolValue;
				DrawProp(headerBytes, "Header Bytes to Skip", 5f, 1, isSkipDisabled);
			}
		}

		/// <summary>
		/// Virtual method to draw additional data format settings.
		/// </summary>
		protected virtual void DrawDataFormatSettingsExtension()
		{
		}

		/// <summary>
		/// Virtual method to draw common settings.
		/// </summary>
		protected virtual void DrawCommonSettings()
		{
			GUILayout.Space(5f);

			EditorGUILayout.LabelField("[Common Settings]", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			DrawToggle(loadAtStartup, "Load At Startup", 5f);

			if (useUndefMenu != null && useUndefMenu.boolValue && useUndef != null)
			{
				DrawToggle(useUndef, "Use Undef (Exclude specific values)", 5f);
				bool isUndefDisabled = !useUndef.boolValue;
				DrawProp(undef, "Undef Value", 5f, 1, isUndefDisabled);
			}

			if (centering != null)
			{
				DrawToggle(centering, "Centering", 5f);
				bool isAutoResizeDisabled = !centering.boolValue;
				if (isAutoResizeDisabled && autoResize != null)
				{
					autoResize.boolValue = false;
				}
				DrawToggle(autoResize, "Auto Resize", 5f, 1, isAutoResizeDisabled);
			}
		}

		/// <summary>
		/// Virtual method to draw additional common settings.
		/// </summary>
		protected virtual void DrawCommonSettingsExtension()
		{
		}

		/// <summary>
		/// Virtual method to draw custom settings at the bottom of the inspector.
		/// </summary>
		protected virtual void DrawCustomSettingsBottom()
		{
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	[RequireComponent(typeof(Activation))]
	[RequireComponent(typeof(DataField))]
	[RequireComponent(typeof(CtrlOBJ))]

	public class ReadModuleTemplate : ModuleTemplate
	{
		public DataField.UpAxis upAxis = DataField.UpAxis.Z;

		public enum Precision
		{
			SINGLE,
			DOUBLE
		}

		public enum DataSourceType
		{
			FILE,
			FOLDER
		}

		[HideInInspector]
		public Activation activation;

		[HideInInspector]
		public DataField  df;

		[HideInInspector]
		public GameObject animator;

		public bool HasAnimator => animator != null;

		public string DataSource
		{
			get { return dataSource; }
			set { dataSource = value; }
		}

		[SerializeField]
		protected string dataSource = "";

		[HideInInspector] [SerializeField]
		protected DataSourceType sourceType = DataSourceType.FILE;

		// Data format settings
		[SerializeField]
		protected Precision precision = Precision.SINGLE;

		[SerializeField]
		protected bool byteswap = true;

		[SerializeField]
		protected bool skipHeader = true;

		[SerializeField]
		protected int  headerBytes = 4; // Record marker size for Fortran unformatted data

		// Common settings
		[SerializeField]
		protected bool useStreamingAssets = true;

		[SerializeField]
		private   bool loadAtStartup = true;

		[SerializeField]
		protected bool centering = true;

		[SerializeField]
		protected bool autoResize = true;

		[SerializeField]
		protected bool useUndef = false;

		[SerializeField]
		protected float undef = 0f;

		// Inspector menu visibility
		[HideInInspector] [SerializeField]
		protected bool useUndefMenu = true;

		[HideInInspector] [SerializeField]
		protected bool usePrecisionMenu = true;

		[HideInInspector] [SerializeField]
		protected bool useByteswapMenu = true;

		[HideInInspector] [SerializeField]
		protected bool useHeaderSkipMenu = true;

		[HideInInspector]
		public int currentStep;

#if UNITY_EDITOR
		/// <summary>
		/// Called when the component is attached or reset in the Inspector.
		/// Automatically reorders components so this script sits directly below DataField.
		/// </summary>
		protected override void Reset()
		{
			// Execute the base class Reset logic (Tag assignment).
			base.Reset();

			// --- Component Reordering Logic ---
			Component[] components = GetComponents<Component>();

			int myIndex = System.Array.IndexOf(components, this);
			int dataFieldIndex = System.Array.IndexOf(components, GetComponent<DataField>());

			if (dataFieldIndex != -1 && myIndex > dataFieldIndex + 1)
			{
				int moveCount = myIndex - (dataFieldIndex + 1);
				for (int i = 0; i < moveCount; i++)
				{
					UnityEditorInternal.ComponentUtility.MoveComponentUp(this);
				}
			}
		}
#endif

		/// <summary>
		/// Initializes core components (Activation, DataField) and checks for the existence of an Animator module in the scene.
		/// </summary>
		void Awake()
		{
			activation = this.GetComponent<Activation>();
			if (activation == null)
			{
				activation = this.gameObject.AddComponent<Activation>();
			}
			activation.SetModuleType(ModuleType.READING);

			df = this.GetComponent<DataField>();
			if (df == null)
			{
				df = this.gameObject.AddComponent<DataField>();
			}
			df.dataType = DataField.DataType.RAW;

			currentStep = 0;
			var modules = GameObject.FindGameObjectsWithTag("VisModule");
			for (int i = 0; i < modules.Length; i++)
			{
				if (modules[i].name == "Animator")
				{
					animator = modules[i];
					break;
				}
			}

			if (animator != null)
			{
				currentStep = animator.GetComponent<Animator>().currentStep;
			}
		}

		/// <summary>
		/// Retrieves parameters, initializes the module, and sets up UI on startup.
		/// </summary>
		void Start()
		{
			// Enable depth texture for all cameras at startup
			EnableDepthTextureGlobal();

			GetParameters();
			InitModule();
			SetupUI();

			if (loadAtStartup)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Monitors parameter changes and triggers the module execution when necessary.
		/// </summary>
		void Update()
		{
			CheckCurrentStep();

			if (activation.GetParameterChanged() == ModuleState.PARAMETER_CHANGED)
			{
				// Turn off the flag until data loading is complete
				df.dataLoaded = false;

				GetParameters();

				if (BodyFunc() == 1)
				{
					SetParentChangedIntoAllChildren();
				}
				else
				{
					Debug.Log("ERROR: in read module func");
				}

				activation.SetParameterChanged(ModuleState.UNCHANGED);
			}
		}

		/// <summary>
		/// Virtual method to be overridden by derived classes. Initializes module-specific settings.
		/// </summary>
		public virtual void InitModule()
		{
		}

		/// <summary>
		/// Virtual method to be overridden by derived classes. The main execution function of the module.
		/// </summary>
		public virtual int BodyFunc()
		{
			return 0;
		}

		/// <summary>
		/// Virtual method to be overridden by derived classes. Retrieves parameters from the UI or other sources.
		/// </summary>
		public virtual void GetParameters()
		{
		}

		/// <summary>
		/// Virtual method to be overridden by derived classes. Prepares the module to load and apply data for a specific time step.
		/// </summary>
		public virtual void SetData(int step)
		{
		}

		/// <summary>
		/// Sets a new time step and forces an update if the step has changed.
		/// </summary>
		public void SetStep(int step)
		{
			if (step != currentStep)
			{
				currentStep = step;
				SetData(step);
				SetParentChangedIntoAllChildren();
			}
		}

		/// <summary>
		/// Checks the current time step from the Animator module and updates if necessary.
		/// </summary>
		public void CheckCurrentStep()
		{
			if (animator != null)
			{
				var step = animator.GetComponent<Animator>().currentStep;
				SetStep(step);
			}
		}

		/// <summary>
		/// Calculates offsets and scale to center and optionally normalize the loaded data within the scene.
		/// </summary>
		public void Centering(bool normalize = false)
		{
			if (!df.dataLoaded) return;

			float[] offset = new float[3];
			float[] min = new float[3];
			float[] max = new float[3];
			float maxDist = float.MinValue;

			for (int i = 0; i < 3; i++)
			{
				min[i] = float.MaxValue;
				max[i] = float.MinValue;
			}

			for (int n = 0; n < df.elements.Length; n++)
			{
				DataElement element = df.elements[n];

				if (element.fieldType == DataElement.FieldType.IRREGULAR)
				{
					if (element.coords.Length > 3 && element.coords[3] != null && element.coords[3].Length > 0)
					{
						int numVerts = element.coords[3].Length / 3;

						for (int m = 0; m < numVerts; m++)
						{
							for (int i = 0; i < 3; i++)
							{
								float v = element.coords[3][m * 3 + i];

								min[i] = Mathf.Min(min[i], v);
								max[i] = Mathf.Max(max[i], v);
							}
						}
					}
				}
				else
				{
					for (int i = 0; i < 3; i++)
					{
						if (element.coords[i] != null && element.coords[i].Length > 0)
						{
							float startVal = element.coords[i][0];
							float endVal = element.coords[i][element.coords[i].Length - 1];

							min[i] = Mathf.Min(min[i], Mathf.Min(startVal, endVal));
							max[i] = Mathf.Max(max[i], Mathf.Max(startVal, endVal));
						}
						else if (element.fieldType == DataElement.FieldType.UNIFORM)
						{
							min[i] = Mathf.Min(min[i], 0);
							max[i] = Mathf.Max(max[i], element.dims[i] - 1);
						}
					}
				}
			}

			for (int i = 0; i < 3; i++)
			{
				maxDist = Mathf.Max(maxDist, max[i] - min[i]);
				offset[i] = min[i] + (max[i] - min[i]) / 2f;
				df.offset[i] = offset[i];
			}

			if (centering)
			{
				foreach (Transform child in transform)
				{
					child.gameObject.transform.localPosition = new Vector3(-offset[0], -offset[1], -offset[2]);
				}
			}
			else
			{
				foreach (Transform child in transform)
				{
					child.gameObject.transform.localPosition = Vector3.zero;
				}
			}

			if (normalize && maxDist > 0)
			{
				float scale = 1f / maxDist * 10f;
				transform.localScale = new Vector3(scale, scale, scale);
			}
			else
			{
				transform.localScale = Vector3.one;
			}
		}

		/// <summary>
		/// Initializes the Animator module and checks the maximum available steps.
		/// </summary>
		public void InitAnimator()
		{
			var animator = GameObject.Find("Animator");

			if (animator != null && animator.tag.Equals("VisModule"))
			{
				animator.GetComponent<Animator>().CheckMaximumSteps();
			}
		}

		/// <summary>
		/// Virtual method to be overridden by derived classes. Performs common post-loading tasks (setting flags, adjusting coordinates, and notifying downstream modules).
		/// </summary>
		protected virtual void ApplyLoadedData()
		{
			df.dataLoaded = true;

			if (centering)
			{
				Centering(autoResize);
			}

			SetCoordinateSystem();

			SetParentChangedIntoAllChildren();
		}

		/// <summary>
		/// Notifies all child modules that the parent parameters have changed.
		/// </summary>
		public void SetParentChangedIntoAllChildren()
		{
			int child_num = this.gameObject.transform.childCount;

			for (int i = 0; i < child_num; i++)
			{
				Transform child = this.gameObject.transform.GetChild(i);

				if (child.GetComponent<Activation>())
				{
					Activation c = child.GetComponent<Activation>();
					c.SetParentChanged(ModuleState.PARAMETER_CHANGED);
				}
			}
		}

		/// <summary>
		/// Manually triggers a parameter change notification.
		/// </summary>
		public void ParameterChanged()
		{
			activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

		/// <summary>
		/// Adjusts the rotation and scale of the transform based on the defined coordinate system and up-axis.
		/// </summary>
		public void SetCoordinateSystem()
		{
			if (df.upAxis == DataField.UpAxis.Z)
			{
				transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));
			}

			if (df.coordinateSystem == DataField.CoordinateSystem.RIGHT_HANDED)
			{
				transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1, 1, -1));
			}
		}

		/// <summary>
		/// Loads text data, including support for Android StreamingAssets.
		/// </summary>
		protected IEnumerator FetchTextRoutine(string relativeOrAbsolutePath, Action<string> onSuccess, Action<string> onError = null)
		{
			string targetPath = useStreamingAssets
				? Path.Combine(Application.streamingAssetsPath, relativeOrAbsolutePath)
				: relativeOrAbsolutePath;

			// Use UnityWebRequest for Web URLs or Android StreamingAssets (jar:file://).
			bool useWebRequest = targetPath.Contains("://") || targetPath.Contains("jar:file://");

			if (useWebRequest)
			{
				using (UnityWebRequest www = UnityWebRequest.Get(targetPath))
				{
					yield return www.SendWebRequest();
					if (www.result == UnityWebRequest.Result.Success)
					{
						onSuccess?.Invoke(www.downloadHandler.text);
					}
					else
					{
						string errorMsg = $"[FetchText] Failed to load {targetPath}: {www.error}";
						Debug.LogError(errorMsg);
						onError?.Invoke(errorMsg);
					}
				}
			}
			else
			{
				// Use standard File API for local storage and external files.
				if (File.Exists(targetPath))
				{
					string text = File.ReadAllText(targetPath);
					onSuccess?.Invoke(text);
				}
				else
				{
					string errorMsg = $"[FetchText] File not found: {targetPath}";
					Debug.LogError(errorMsg);
					onError?.Invoke(errorMsg);
				}
			}
		}

		/// <summary>
		/// Loads binary data, including support for Android StreamingAssets.
		/// </summary>
		protected IEnumerator FetchBinaryRoutine(string relativeOrAbsolutePath, Action<byte[]> onSuccess, Action<string> onError = null)
		{
			string targetPath = useStreamingAssets
				? Path.Combine(Application.streamingAssetsPath, relativeOrAbsolutePath)
				: relativeOrAbsolutePath;

			bool useWebRequest = targetPath.Contains("://") || targetPath.Contains("jar:file://");

			if (useWebRequest)
			{
				using (UnityWebRequest www = UnityWebRequest.Get(targetPath))
				{
					yield return www.SendWebRequest();
					if (www.result == UnityWebRequest.Result.Success)
					{
						onSuccess?.Invoke(www.downloadHandler.data);
					}
					else
					{
						string errorMsg = $"[FetchBinary] Failed to load {targetPath}: {www.error}";
						Debug.LogError(errorMsg);
						onError?.Invoke(errorMsg);
					}
				}
			}
			else
			{
				if (File.Exists(targetPath))
				{
					byte[] data = File.ReadAllBytes(targetPath);
					onSuccess?.Invoke(data);
				}
				else
				{
					string errorMsg = $"[FetchBinary] File not found: {targetPath}";
					Debug.LogError(errorMsg);
					onError?.Invoke(errorMsg);
				}
			}
		}

		/// <summary>
		/// Asynchronously fetches, parses, and calculates statistics for multiple binary files in parallel.
		/// This shared routine handles both local file I/O (via background threads) and web requests (via coroutines),
		/// maximizing multi-core CPU usage.
		/// </summary>
		protected IEnumerator FetchParseAndCalcStatsParallelRoutine(
			string[] absolutePaths,
			int totalGridSize,
			DataElement[] targetElements,
			float[][] outputValues,
			Precision precision,
			bool byteswap,
			bool skipHeader,
			int headerBytes,
			bool useWebRequest)
		{
			int numFiles = absolutePaths.Length;
			byte[][] allFileBytes = new byte[numFiles][];

			// 1. Fetch data via WebRequest if needed (Coroutines must run on the main thread)
			if (useWebRequest)
			{
				Coroutine[] loadCoroutines = new Coroutine[numFiles];
				for (int i = 0; i < numFiles; i++)
				{
					if (!string.IsNullOrEmpty(absolutePaths[i]))
					{
						int captureIndex = i;
						loadCoroutines[i] = StartCoroutine(FetchBinaryRoutine(absolutePaths[i], (data) => { allFileBytes[captureIndex] = data; }));
					}
				}

				for (int i = 0; i < loadCoroutines.Length; i++)
				{
					if (loadCoroutines[i] != null) yield return loadCoroutines[i];
				}
			}

			// 2. Parallel Processing: Local I/O (if applicable), Parsing, and Statistics Calculation
			System.Threading.Tasks.Task processTask = System.Threading.Tasks.Task.Run(() =>
			{
				System.Threading.Tasks.Parallel.For(0, numFiles, i =>
				{
					string path = absolutePaths[i];

					if (string.IsNullOrEmpty(path)) return;

					byte[] fileBytes = null;

					// Background thread direct disk I/O (Fastest for local files)
					if (!useWebRequest)
					{
						try
						{
							if (File.Exists(path))
							{
								fileBytes = File.ReadAllBytes(path);
							}
						}
						catch (Exception e)
						{
							Debug.LogError($"[FetchParseAndCalcStats] IO Error: {e.Message}");
						}
					}
					else
					{
						fileBytes = allFileBytes[i];
					}

					if (fileBytes != null && fileBytes.Length > 0)
					{
						// Parse binary to float list
						var parsedList = ParseBinaryToFloatList(fileBytes, totalGridSize, precision, byteswap, skipHeader, headerBytes);

						if (parsedList != null && parsedList.Count > 0)
						{
							float min = float.MaxValue;
							float max = float.MinValue;
							double sum = 0;
							double sumSq = 0;
							int count = 0;

							bool checkUndef = targetElements[i].useUndef;
							float undefVal = targetElements[i].undef;

							float[] valArray = new float[parsedList.Count];
							for (int n = 0; n < parsedList.Count; n++)
							{
								float v = parsedList[n];
								valArray[n] = v;

								if (checkUndef && v == undefVal) continue;
								if (v < min) min = v;
								if (v > max) max = v;
								sum += v;
								sumSq += v * v;
								count++;
							}

							// Store parsed array
							outputValues[i] = valArray;

							// Calculate and store stats directly into the DataElement
							if (count > 0)
							{
								targetElements[i].min = min;
								targetElements[i].max = max;
								targetElements[i].average = (float)(sum / count);
								targetElements[i].variance = (float)((sumSq / count) - (targetElements[i].average * targetElements[i].average));
							}
						}
					}
				});
			});

			// Wait for all background tasks to complete without freezing the main thread
			yield return new WaitUntil(() => processTask.IsCompleted);

			if (processTask.Exception != null)
			{
				Debug.LogError($"[FetchParseAndCalcStats] Parallel Process Error: {processTask.Exception.InnerException?.Message}");
			}
		}

		/// <summary>
		/// Asynchronously loads binary data via platform-specific methods and parses it into a list of floats.
		/// </summary>
		protected IEnumerator FetchAndParseBinaryRoutine(
			string path, int dataCount, Precision precision, bool doByteSwap, bool doSkipHeader, int headerSize,
			Action<List<float>> onSuccess, Action<string> onError = null)
		{
			byte[] rawData = null;
			bool hasError = false;

			bool useWebRequest = Application.platform == RuntimePlatform.Android ||
								 Application.platform == RuntimePlatform.WebGLPlayer ||
								 path.Contains("://");

			if (useWebRequest)
			{
				yield return StartCoroutine(FetchBinaryRoutine(path, (data) => { rawData = data; }, (err) => {
					hasError = true;
					onError?.Invoke(err);
				}));
			}
			else
			{
				string absolutePath = path;
				if (!Path.IsPathRooted(absolutePath))
				{
					absolutePath = Path.Combine(Application.streamingAssetsPath, absolutePath);
				}

				Task<byte[]> loadTask = Task.Run(() => File.ReadAllBytes(absolutePath));
				yield return new WaitUntil(() => loadTask.IsCompleted);

				if (loadTask.Exception != null)
				{
					hasError = true;
					onError?.Invoke(loadTask.Exception.InnerException.Message);
				}
				else
				{
					rawData = loadTask.Result;
				}
			}

			if (hasError || rawData == null) yield break;

			Task<List<float>> parseTask = Task.Run(() =>
				ParseBinaryToFloatList(rawData, dataCount, precision, doByteSwap, doSkipHeader, headerSize)
			);

			yield return new WaitUntil(() => parseTask.IsCompleted);

			if (parseTask.Exception != null)
			{
				onError?.Invoke(parseTask.Exception.InnerException.Message);
				yield break;
			}

			onSuccess?.Invoke(parseTask.Result);
		}

		/// <summary>
		/// Converts the retrieved byte array into a list of floats using Span and MemoryMarshal.
		/// </summary>
		protected List<float> ParseBinaryToFloatList(byte[] rawData, int dataCount, Precision precision, bool doByteSwap, bool doSkipHeader, int headerSize)
		{
#if ENABLE_PROFILING
			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
#endif

			List<float> result = new List<float>(dataCount);
			int offset = doSkipHeader ? headerSize : 0;
			int dataLength = (precision == Precision.DOUBLE) ? 8 : 4;

			if (rawData == null || rawData.Length < offset + dataCount * dataLength)
			{
				UnityEngine.Debug.LogError("[ParseBinary] Data size is smaller than expected.");
				return result;
			}

			ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte>(rawData, offset, dataCount * dataLength);

			if (!doByteSwap)
			{
				if (precision == Precision.SINGLE)
				{
					ReadOnlySpan<float> floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(dataSpan);
					for (int i = 0; i < dataCount; i++)
					{
						result.Add(floatSpan[i]);
					}
				}
				else
				{
					ReadOnlySpan<double> doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, double>(dataSpan);
					for (int i = 0; i < dataCount; i++)
					{
						result.Add((float)doubleSpan[i]);
					}
				}

#if ENABLE_PROFILING
				sw.Stop();
				UnityEngine.Debug.Log($"[Performance] MemoryMarshal Fast Path: {sw.Elapsed.TotalMilliseconds:F3} ms (DataCount: {dataCount})");
#endif

				return result;
			}

			for (int n = 0; n < dataCount; n++)
			{
				float value = 0f;
				int idx = offset + n * dataLength;

				if (precision == Precision.SINGLE)
				{
					int intVal = (rawData[idx] << 24) | (rawData[idx + 1] << 16) | (rawData[idx + 2] << 8) | rawData[idx + 3];
					value = BitConverter.Int32BitsToSingle(intVal);
				}
				else
				{
					long longVal = ((long)rawData[idx] << 56) | ((long)rawData[idx + 1] << 48) |
								   ((long)rawData[idx + 2] << 40) | ((long)rawData[idx + 3] << 32) |
								   ((long)rawData[idx + 4] << 24) | ((long)rawData[idx + 5] << 16) |
								   ((long)rawData[idx + 6] << 8)  | rawData[idx + 7];
					value = (float)BitConverter.Int64BitsToDouble(longVal);
				}

				result.Add(value);
			}

#if ENABLE_PROFILING
			sw.Stop();
			UnityEngine.Debug.Log($"[Performance] BitShift Swap Path: {sw.Elapsed.TotalMilliseconds:F3} ms (DataCount: {dataCount})");
#endif

			return result;
		}

		/// <summary>
		/// Gets the list of files in a folder, supporting JNI access to Android StreamingAssets.
		/// </summary>
		protected List<string> GetFilesInDirectory(string folderPath)
		{
			List<string> fileList = new List<string>();

			if (useStreamingAssets)
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				// On Android devices, calls AssetManager via JNI for StreamingAssets.
				try
				{
					using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
					using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
					using (AndroidJavaObject assetManager = currentActivity.Call<AndroidJavaObject>("getAssets"))
					{
						string[] files = assetManager.Call<string[]>("list", folderPath);

						if (files != null)
						{
							foreach (var file in files)
							{
								if (file.EndsWith(".meta")) continue;
								fileList.Add(Path.Combine(folderPath, file).Replace("\\", "/"));
							}
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"[GetFilesInDirectory] JNI Error: {e.Message}");
				}
#else
				string fullPath = Path.Combine(Application.streamingAssetsPath, folderPath);

				if (Directory.Exists(fullPath))
				{
					var files = Directory.GetFiles(fullPath)
						.Where(p => !p.EndsWith(".meta"))
						.Select(p => p.Replace("\\", "/"));

					fileList.AddRange(files);
				}
				else
				{
					Debug.LogWarning($"[GetFilesInDirectory] Directory not found: {fullPath}");
				}
#endif
			}
			else
			{
				if (Directory.Exists(folderPath))
				{
					var files = Directory.GetFiles(folderPath)
						.Where(p => !p.EndsWith(".meta"))
						.Select(p => p.Replace("\\", "/"));

					fileList.AddRange(files);
				}
				else
				{
					Debug.LogWarning($"[GetFilesInDirectory] Directory not found: {folderPath}");
				}
			}

			fileList.Sort();

			return fileList;
		}

		/// <summary>
		/// Coroutine to calculate statistics for the values set in the DataElement.
		/// </summary>
		protected IEnumerator CalcStatsForCurrentElementsRoutine()
		{
			if (df == null || df.elements == null) yield break;

			DataElement[] elements = df.elements;

			Task statsTask = Task.Run(() =>
			{
				for (int i = 0; i < elements.Length; i++)
				{
					DataElement e = elements[i];

					if (e.values == null || e.values.Length == 0)
					{
						continue;
					}

					float min = float.MaxValue;
					float max = float.MinValue;
					double sum = 0;
					double sumSq = 0;
					long count = 0;

					AccumulateStats(e.values, e.useUndef, e.undef, ref min, ref max, ref sum, ref sumSq, ref count);

					if (count > 0)
					{
						e.min = min;
						e.max = max;
						e.average = (float)(sum / count);
						e.variance = (float)((sumSq / count) - (e.average * e.average));
					}
				}
			});

			yield return new WaitUntil(() => statsTask.IsCompleted);

			if (statsTask.Exception != null)
			{
				Debug.LogError($"[CalcStats] Error: {statsTask.Exception.InnerException?.Message}");
			}
		}

		/// <summary>
		/// Calculates four statistics (max, min, average, and variance) for a list of floats.
		/// </summary>
		protected void AccumulateStats(List<float> values, bool useUndef, float undefVal, ref float min, ref float max, ref double sum, ref double sumSq, ref long count)
		{
			for (int i = 0; i < values.Count; i++)
			{
				float val = values[i];

				if (useUndef && System.Math.Abs(val - undefVal) < 1e-4f) continue;

				if (val < min) min = val;
				if (val > max) max = val;

				sum += val;
				sumSq += (double)val * val;
				count++;
			}
		}

		/// <summary>
		/// Calculates the maximum, minimum, average, and variance of a float array.
		/// </summary>
		protected void AccumulateStats(float[] values, bool useUndef, float undefVal, ref float min, ref float max, ref double sum, ref double sumSq, ref long count)
		{
			for (int i = 0; i < values.Length; i++)
			{
				float val = values[i];

				if (useUndef && System.Math.Abs(val - undefVal) < 1e-4f) continue;

				if (val < min) min = val;
				if (val > max) max = val;

				sum += val;
				sumSq += (double)val * val;
				count++;
			}
		}

		// ==========================================================
		// Public methods for external UI events (e.g., Unity UI Dropdown, Toggle)
		// ==========================================================

		public void SetPrecision(int mode)
		{
			precision = (Precision)mode;
		}

		public void SetByteSwap(bool flag)
		{
			byteswap = flag;
		}

		public void SetSkipHeader(bool flag)
		{
			skipHeader = flag;
		}

		public void SetUseUndef(bool flag)
		{
			useUndef = flag;
		}

		public void SetUndefValue(string valueStr)
		{
			if (float.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val))
			{
				undef = val;
			}
		}

		/// <summary>
		/// Automatically enables depth texture generation for all cameras in the scene,
		/// including Scene View and VR cameras.
		/// This is essential for Early Ray Termination in VolumeRenderer and proper depth sorting.
		/// </summary>
		protected void EnableDepthTextureGlobal()
		{
			foreach (Camera cam in Camera.allCameras)
			{
				if (cam != null)
				{
					cam.depthTextureMode |= DepthTextureMode.Depth;
				}
			}

#if UNITY_EDITOR
			foreach (UnityEditor.SceneView sceneView in UnityEditor.SceneView.sceneViews)
			{
				if (sceneView != null && sceneView.camera != null)
				{
					sceneView.camera.depthTextureMode |= DepthTextureMode.Depth;
				}
			}
#endif
		}
	}
}