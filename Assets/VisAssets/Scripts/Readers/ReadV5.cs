using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	[CustomEditor(typeof(ReadV5))]
	public class ReadV5Editor : ReadModuleTemplateEditor
	{
		SerializedProperty logicalFields;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			logicalFields = serializedObject.FindProperty("logicalFields");
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

		/// <summary>
		/// Resets the component to its default values and applies component reordering.
		/// </summary>
		protected override void Reset()
		{
#if UNITY_EDITOR
			// Execute the base class component reordering logic.
			base.Reset();
#endif
			sourceType = DataSourceType.FILE;

			useUndefMenu      = false;
			usePrecisionMenu  = true;
			precision         = Precision.DOUBLE;
			useByteswapMenu   = true;
			useHeaderSkipMenu = true;
		}

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

			V5Metadata meta = ParseV5Metadata(v5Text);

			if (meta.dims[0] * meta.dims[1] * meta.dims[2] == 0) yield break;

			List<float>[] coords = new List<float>[4];

			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}
			
			for (int i = 0; i < 3; i++)
			{
				if (string.IsNullOrEmpty(meta.coordFiles[i])) continue;
				
				Debug.Log($"[ReadV5] Loading Coordinate Data ({i}): {meta.coordFiles[i]}");
				
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

			int totalGridSize = meta.dims[0] * meta.dims[1] * meta.dims[2];
			int elementCounter = 0;

			List<LogicalFieldDef> allFields = new List<LogicalFieldDef>();
			allFields.AddRange(meta.scalars.Values);
			allFields.AddRange(meta.vectors.Values);
			allFields.Sort((a, b) => a.sequence.CompareTo(b.sequence));

			foreach (var def in allFields)
			{
				var fieldInfo = new LogicalFieldInfo { FieldName = def.label, IsVector = def.isVector, ElementIndices = new int[def.isVector ? 3 : 1] };

				if (!def.isVector)
				{
					Debug.Log($"[ReadV5] Loading Scalar Data [{def.label}]: {def.files[0]}");

					yield return StartCoroutine(FetchAndParseBinaryRoutine(
						def.files[0], totalGridSize, precision, byteswap, skipHeader, headerBytes,
						onSuccess: (values) => { SetElementData(elementCounter, meta.dims, coords, values, def.label); },
						onError: (err) => { Debug.LogError($"[ReadV5] Error: {err}"); }
					));

					fieldInfo.ElementIndices[0] = elementCounter;
					elementCounter++;
				}
				else
				{
					string[] axisNames = { " (U)", " (V)", " (W)" };

					for (int i = 0; i < 3; i++)
					{
						if (string.IsNullOrEmpty(def.files[i])) continue;

						Debug.Log($"[ReadV5] Loading Vector Data [{def.label}{axisNames[i]}]: {def.files[i]}");

						yield return StartCoroutine(FetchAndParseBinaryRoutine(
							def.files[i], totalGridSize, precision, byteswap, skipHeader, headerBytes,
							onSuccess: (values) => { SetElementData(elementCounter, meta.dims, coords, values, def.label + axisNames[i]); },
							onError: (err) => { Debug.LogError($"[ReadV5] Error: {err}"); }
						));

						fieldInfo.ElementIndices[i] = elementCounter;
						elementCounter++;
					}
				}

				logicalFields.Add(fieldInfo);
			}

			yield return StartCoroutine(CalcStatsForCurrentElementsRoutine());

			Debug.Log("[ReadV5] All data successfully loaded!");

			ApplyLoadedData();
		}

		/// <summary>
		/// Sets the parsed dimensions, coordinates, and values to a specific DataElement.
		/// </summary>
		private void SetElementData(int index, int[] dims, List<float>[] coords, List<float> values, string varName)
		{
			df.elements[index].SetDims(new List<int>(dims));
			df.elements[index].SetCoords(coords);
			df.elements[index].SetValues(values);
			df.elements[index].SetFieldType(FieldType.RECTILINEAR);
			df.elements[index].varName = varName.Replace("\\n", " ");
			df.elements[index].SetActive(true);
		}

		// --- Helper Structures and Parsing Logic ---

		private class LogicalFieldDef
		{
			public int sequence;
			public bool isVector;
			public string label = "Unknown";
			public string[] files;

			public LogicalFieldDef(int size, bool vector)
			{
				files = new string[size];
				isVector = vector;
			}
		}

		private struct V5Metadata
		{
			public int[] dims;
			public string[] coordFiles;
			public SortedDictionary<int, LogicalFieldDef> scalars;
			public SortedDictionary<int, LogicalFieldDef> vectors;

			public V5Metadata(int dummy)
			{
				dims = new int[3];
				coordFiles = new string[3];
				scalars = new SortedDictionary<int, LogicalFieldDef>();
				vectors = new SortedDictionary<int, LogicalFieldDef>();
			}
		}

		/// <summary>
		/// Parses the VFIVE metadata text to extract grid dimensions, coordinate files, and variable fields.
		/// </summary>
		private V5Metadata ParseV5Metadata(string text)
		{
			V5Metadata meta = new V5Metadata(0);
			string filePath = Path.GetDirectoryName(dataSource);
			int sequenceCounter = 0;

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
					else if (keyword.StartsWith("SCAL") && keyword != "SCALE" && keyword != "NSCAL")
					{
						int idx = int.Parse(Regex.Match(keyword, @"\d+").Value);

						if (!meta.scalars.ContainsKey(idx))
						{
							meta.scalars[idx] = new LogicalFieldDef(1, false) { sequence = sequenceCounter++ };
						}

						if (keyword.Contains("_LABEL"))
						{
							meta.scalars[idx].label = tokens[1];
						}
						else if (!keyword.Contains("MIN") && !keyword.Contains("MAX"))
						{
							meta.scalars[idx].files[0] = GetAdjustedPath(filePath, tokens[1]);
						}
					}
					else if (keyword.StartsWith("VECT") && keyword != "NVEC")
					{
						int idx = int.Parse(Regex.Match(keyword, @"\d+").Value);

						if (!meta.vectors.ContainsKey(idx))
						{
							meta.vectors[idx] = new LogicalFieldDef(3, true) { sequence = sequenceCounter++ };
						}

						if (keyword.Contains("_LABEL"))
						{
							meta.vectors[idx].label = tokens[1];
						}
						else if (!keyword.Contains("MIN") && !keyword.Contains("MAX"))
						{
							if (keyword.EndsWith("X"))
							{
								meta.vectors[idx].files[0] = GetAdjustedPath(filePath, tokens[1]);
							}
							else if (keyword.EndsWith("Y"))
							{
								meta.vectors[idx].files[1] = GetAdjustedPath(filePath, tokens[1]);
							}
							else if (keyword.EndsWith("Z"))
							{
								meta.vectors[idx].files[2] = GetAdjustedPath(filePath, tokens[1]);
							}
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