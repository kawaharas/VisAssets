using System;
using System.Collections;
using System.Collections.Generic;
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
	[CustomEditor(typeof(ReadRAW))]
	public class ReadRAWEditor : ReadModuleTemplateEditor
	{
		SerializedProperty dims;
		SerializedProperty varname;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			dims = serializedObject.FindProperty("dims");
			varname = serializedObject.FindProperty("varname");
		}

		/// <summary>
		/// Overrides the base class method. Draws additional data format settings specific to this module.
		/// </summary>
		protected override void DrawDataFormatSettingsExtension()
		{
			GUILayout.Space(5f);
			
			DrawProp(dims, "Size (X, Y, Z)");
			DrawProp(varname, "Variable Name", 5f);
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	public class ReadRAW : ReadModuleTemplate
	{
		public Vector3Int dims;
		public string varname = string.Empty;

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

			useUndefMenu      = true;
			undef             = 0f;
			usePrecisionMenu  = true;
			useByteswapMenu   = true;
			useHeaderSkipMenu = true;
		}

		/// <summary>
		/// Overrides the base class method. Initializes module-specific settings (e.g., rotation for Z-axis upward).
		/// </summary>
		public override void InitModule()
		{
			// The upper direction is defined as the Z-axis in VFIVE.
			transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));
		}

		/// <summary>
		/// Overrides the base class method. The main execution function of the module that triggers the data loading.
		/// </summary>
		public override int BodyFunc()
		{
			if (string.IsNullOrEmpty(dataSource)) return 0;

			StartCoroutine(LoadData());
			return 1;
		}

		/// <summary>
		/// Coroutine that orchestrates the raw binary data loading, parsing, and application processes.
		/// </summary>
		IEnumerator LoadData()
		{
			int dimension = dims.x * dims.y * dims.z;
			List<float> loadedValues = null;

			// Call the base class's hybrid I/O and background parsing routine
			yield return StartCoroutine(FetchAndParseBinaryRoutine(
				dataSource,
				dimension,
				precision,
				byteswap,
				skipHeader,
				headerBytes,
				onSuccess: (values) => { loadedValues = values; },
				onError: (err) => { Debug.LogError($"[ReadRAW] Load Error: {err}"); }
			));

			// Set the data to DataField on the main thread after background processing is complete
			if (loadedValues != null)
			{
				GenerateAndSetDataField(loadedValues);

				yield return StartCoroutine(CalcStatsForCurrentElementsRoutine());

				ApplyLoadedData();
			}
		}

		/// <summary>
		/// Generates coordinates based on the specified dimensions and sets the loaded values to the DataField.
		/// </summary>
		private void GenerateAndSetDataField(List<float> values)
		{
			var coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)

			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}

			// Set the coordinate of each axis as uniform data
			for (int i = 0; i < 3; i++)
			{
				for (int n = 0; n < dims[i]; n++)
				{
					coords[i].Add((float)n);
				}
			}

			// Merge coordinates into the 4th list
			for (int k = 0; k < dims.z; k++)
			{
				for (int j = 0; j < dims.y; j++)
				{
					for (int i = 0; i < dims.x; i++)
					{
						coords[3].Add(coords[0][i]);
						coords[3].Add(coords[1][j]);
						coords[3].Add(coords[2][k]);
					}
				}
			}

			df.CreateElements(1);
			df.elements[0].SetDims(dims); 
			df.elements[0].SetCoords(coords);
			if (useUndef)
			{
				df.elements[0].SetUndef(undef);
			}
			df.elements[0].SetValues(values);
			df.elements[0].SetVarName(varname);
			df.elements[0].SetFieldType(FieldType.UNIFORM);
			df.elements[0].SetActive(true);
		}
	}
}