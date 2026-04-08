using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	[CustomEditor(typeof(ReadField))]
	public class ReadFieldEditor : ReadModuleTemplateEditor
	{
		SerializedProperty useDummyData;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			useDummyData = serializedObject.FindProperty("useDummyData");
		}

		/// <summary>
		/// Overrides the base class method. Draws data source settings, disabling them if dummy data is used.
		/// </summary>
		protected override void DrawDataSourceSettings()
		{
			EditorGUI.BeginDisabledGroup(useDummyData.boolValue);

			base.DrawDataSourceSettings();

			EditorGUI.EndDisabledGroup();
		}

		/// <summary>
		/// Overrides the base class method. Draws additional data format settings specific to this module.
		/// </summary>
		protected override void DrawDataFormatSettingsExtension()
		{
			GUILayout.Space(5f);

			useDummyData.boolValue = EditorGUILayout.ToggleLeft("Use Dummy Data (for Test)", useDummyData.boolValue);

			GUILayout.Space(5f);
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	public class ReadField : ReadModuleTemplate
	{
		public bool useDummyData;
		public string debugString = string.Empty;

		// Data structure for passing data between the background and main threads
		private class ParsedTextData
		{
			public List<int> dims = new List<int>();
			public List<float>[] coords = new List<float>[4];
			public List<float>[] values = null;
			public int vlen = 0;
			public FieldType fieldType = FieldType.UNDEFINED;
		}

		/// <summary>
		/// Resets the component to its default values. Called automatically when attaching the script or selecting 'Reset' in the Inspector.
		/// </summary>
		private void Reset()
		{
			sourceType = DataSourceType.FILE;

			useUndefMenu      = false;
			usePrecisionMenu  = false;
			useByteswapMenu   = false;
			useHeaderSkipMenu = false;
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
			if (string.IsNullOrEmpty(dataSource) && !useDummyData) return 0;

			StartCoroutine(LoadData());

			return 1;
		}

		/// <summary>
		/// Manually triggers a parameter change notification to force an update.
		/// </summary>
		public void Exec()
		{
			if (!string.IsNullOrEmpty(dataSource) || useDummyData)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Coroutine that orchestrates the text data loading, parsing, and application processes.
		/// </summary>
		IEnumerator LoadData()
		{
			if (useDummyData)
			{
				SetDummyData();

				yield return StartCoroutine(CalcStatsForCurrentElementsRoutine());

				ApplyLoadedData();

				yield break;
			}

			string loadedText = null;
			bool hasError = false;

			// Load text file
			yield return StartCoroutine(FetchTextRoutine(
				dataSource,
				onSuccess: (text) => { loadedText = text; },
				onError: (err) => { hasError = true; }
			));

			if (hasError || string.IsNullOrEmpty(loadedText)) yield break;

			// Run text parsing in the background
			Task<ParsedTextData> parseTask = Task.Run(() => ParseTextDataInBackground(loadedText));

			// Wait for parsing to complete without blocking the main thread
			yield return new WaitUntil(() => parseTask.IsCompleted);

			if (parseTask.Exception != null)
			{
				Debug.LogError($"[ReadField] Parse Exception: {parseTask.Exception.InnerException.Message}");
				yield break;
			}

			// Store the parsing result in DataField on the main thread
			ApplyParsedData(parseTask.Result);

			yield return StartCoroutine(CalcStatsForCurrentElementsRoutine());

			ApplyLoadedData();
		}

		/// <summary>
		/// Parses comma-separated text data into a structured format on a background thread.
		/// </summary>
		private ParsedTextData ParseTextDataInBackground(string textString)
		{
			ParsedTextData result = new ParsedTextData();

			for (int i = 0; i < 4; i++)
			{
				result.coords[i] = new List<float>();
			}

			try
			{
				using (StringReader streamReader = new StringReader(textString))
				{
					string line = streamReader.ReadLine();
					string[] stringList = line.Split(',');
					int ndim = stringList.Length;
					int size = 1;

					for (int i = 0; i < ndim; i++)
					{
						int tmp = int.Parse(stringList[i]);
						result.dims.Add(tmp);
						size *= tmp;
					}

					for (int i = 0; i < size; i++)
					{
						line = streamReader.ReadLine();

						if (string.IsNullOrEmpty(line)) continue;

						stringList = line.Split(',');
						int column = stringList.Length;

						if (i == 0)
						{
							if (column <= 3)
							{
								result.vlen = column;
								result.fieldType = FieldType.UNIFORM;
							}
							else
							{
								result.vlen = column == 4 ? 1 : column - 3;
								result.fieldType = FieldType.IRREGULAR;
							}

							result.values = new List<float>[result.vlen];
							for (int j = 0; j < result.vlen; j++)
							{
								result.values[j] = new List<float>();
							}
						}

						if (result.fieldType == FieldType.UNIFORM)
						{
							for (int j = 0; j < result.vlen; j++)
							{
								result.values[j].Add(float.Parse(stringList[j]));
							}
						}
						else if (result.fieldType == FieldType.IRREGULAR)
						{
							for (int j = 0; j < 3; j++)
							{
								result.coords[3].Add(float.Parse(stringList[j]));
							}
							for (int j = 0; j < result.vlen; j++)
							{
								result.values[j].Add(float.Parse(stringList[j + 3]));
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Exception parsing data: " + e);
				return null;
			}

			// For safety, recheck if the field type is irregular or rectilinear
			if (result.fieldType == FieldType.IRREGULAR)
			{
				if (RecheckCoordinate(result.coords, result.dims))
				{
					result.fieldType = FieldType.RECTILINEAR;
				}
			}

			return result;
		}

		/// <summary>
		/// Applies the successfully parsed text data into the DataField elements on the main thread.
		/// </summary>
		private void ApplyParsedData(ParsedTextData data)
		{
			if (data == null || data.vlen == 0) return;

			df.CreateElements(data.vlen);
			for (int i = 0; i < data.vlen; i++)
			{
				df.elements[i].SetDims(data.dims);
				df.elements[i].SetCoords(data.coords);
				if (useUndef)
				{
					df.elements[i].SetUndef(undef);
				}
				df.elements[i].SetValues(data.values[i]);
				df.elements[i].SetFieldType(data.fieldType);
				df.elements[i].SetActive(true);
			}
		}

		/// <summary>
		/// Re-evaluates irregular coordinate data to determine if it can be represented as a rectilinear grid.
		/// </summary>
		private bool RecheckCoordinate(List<float>[] coords, List<int> dims)
		{
			var check = new bool[3] {true, true, true};

			for (int i = 0; i < dims[0]; i++)
			{
				coords[0].Add(coords[3][i * 3]);
			}
			for (int j = 0; j < dims[1]; j++)
			{
				coords[1].Add(coords[3][dims[0] * j * 3 + 1]);
			}
			for (int k = 0; k < dims[2]; k++)
			{
				coords[2].Add(coords[3][dims[0] * dims[1] * k * 3 + 2]);
			}

			// Check axes
			for (int k = 0; k < dims[2]; k++)
			{
				for (int j = 0; j < dims[1]; j++)
				{
					for (int i = 0; i < dims[0]; i++)
					{
						int idx = (dims[0] * dims[1] * k + dims[0] * j + i) * 3;

						if (coords[0][i] != coords[3][idx])
						{
							check[0] = false;
							break;
						}

						if (coords[1][j] != coords[3][idx + 1])
						{
							check[1] = false;
							break;
						}

						if (coords[2][k] != coords[3][idx + 2])
						{
							check[2] = false;
							break;
						}
					}
				}
			}

			if (check[0] && check[1] && check[2]) return true;

			for (int i = 0; i < 3; i++)
			{
				coords[i].Clear();
			}

			return false;
		}

		/// <summary>
		/// Generates and assigns dummy scalar data for testing purposes when no external file is provided.
		/// </summary>
		private void SetDummyData()
		{
			List<int> dims = new List<int>();
			List<float>[] coords;
			List<float>[] values;
			float x, y, z, p1, p2;
			int mx = 11, my = 11, mz = 11;
			int vlen = 2;

			dims.Add(mx);
			dims.Add(my);
			dims.Add(mz);

			values = new List<float>[vlen];
			for (int i = 0; i < vlen; i++)
			{
				values[i] = new List<float>();
			}

			coords = new List<float>[4];
			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}

			for (int i = 0; i < mx; i++)
			{
				coords[0].Add(i - mx / 2);
			}

			for (int i = 0; i < my; i++)
			{
				coords[1].Add(i - my / 2);
			}

			for (int i = 0; i < mz; i++)
			{
				coords[2].Add(i - mz / 2);
			}

			for (int k = 0; k < mz; k++)
			{
				for (int j = 0; j < my; j++)
				{
					for (int i = 0; i < mx; i++)
					{
						x = i - mx / 2;
						y = j - my / 2;
						z = k - mz / 2;
						p1 = Mathf.Sqrt(x * x + y * y + z * z);
						p2 = i + j + k;
						values[0].Add(p1);
						values[1].Add(p2);
					}
				}
			}

			for (int k = 0; k < dims[2]; k++)
			{
				for (int j = 0; j < dims[1]; j++)
				{
					for (int i = 0; i < dims[0]; i++)
					{
						coords[3].Add(coords[0][i]);
						coords[3].Add(coords[1][j]);
						coords[3].Add(coords[2][k]);
					}
				}
			}

			df.CreateElements(vlen);
			for (int i = 0; i < vlen; i++)
			{
				df.elements[i].SetDims(dims);
				df.elements[i].SetCoords(coords);
				df.elements[i].SetValues(values[i]);
				df.elements[i].SetFieldType(FieldType.RECTILINEAR);
				df.elements[i].SetActive(true);
			}
		}
	}
}