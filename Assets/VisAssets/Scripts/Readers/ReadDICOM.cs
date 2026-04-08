using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.DataLoader
{
	using FieldType = DataElement.FieldType;

	/// <summary>
	/// Represents a single metadata entry (Tag and Value) extracted from a DICOM file.
	/// </summary>
	[System.Serializable]
	public struct DicomMetaEntry
	{
		public string Tag;
		public string Value;
	}

	/// <summary>
	/// Handles the parsing and storage of DICOM header metadata and 3D pixel data.
	/// </summary>
	public class DicomData
	{
		public Dictionary<string, string> MetaData { get; private set; } = new Dictionary<string, string>();
		public int Width { get; private set; } = 0;
		public int Height { get; private set; } = 0;
		public int Depth { get; private set; } = 1;
		public int BitsAllocated { get; private set; } = 16;
		public float[,,] ImageData3D { get; private set; }
		public int PixelRepresentation { get; private set; } = 0;
		public float PixelSpacingX { get; private set; } = 1.0f;
		public float PixelSpacingY { get; private set; } = 1.0f;
		public float SliceThickness { get; private set; } = 1.0f;
		public int InstanceNumber { get; private set; } = 0;

		private Encoding currentEncoding;

		/// <summary>
		/// Parses the raw byte array of a DICOM file, extracting metadata and pixel data.
		/// </summary>
		public bool Parse(byte[] rawBytes, string defaultEncodingName = "shift_jis")
		{
			if (rawBytes == null || rawBytes.Length < 132) return false;

			try
			{
				currentEncoding = Encoding.GetEncoding(defaultEncodingName);
			}
			catch
			{
				currentEncoding = Encoding.UTF8;
			}

			using (MemoryStream ms = new MemoryStream(rawBytes))
			using (BinaryReader reader = new BinaryReader(ms))
			{
				ms.Seek(128, SeekOrigin.Begin);
				if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "DICM") return false;

				while (ms.Position < ms.Length)
				{
					ushort group = reader.ReadUInt16();
					ushort element = reader.ReadUInt16();
					string tagName = GetReadableTagName(group, element);

					long length = 0;
					long currentPos = ms.Position;
					string vrObj = Encoding.ASCII.GetString(reader.ReadBytes(2));
					
					if (char.IsUpper(vrObj[0]) && char.IsUpper(vrObj[1]))
					{
						if (vrObj == "OB" || vrObj == "OW" || vrObj == "SQ" || vrObj == "UN")
						{
							reader.ReadUInt16();
							length = reader.ReadUInt32();
						}
						else
						{
							length = reader.ReadUInt16();
						}
					}
					else
					{
						ms.Position = currentPos;
						length = reader.ReadUInt32();
					}

					if (group == 0x0002 && element == 0x0000) 
					{
						byte[] valBytes = reader.ReadBytes((int)length);
						if (length == 4)
						{
							MetaData[tagName] = BitConverter.ToUInt32(valBytes, 0).ToString();
						}
						else
						{
							MetaData[tagName] = $"[Data Size: {length}]";
						}
					}
					else if (group == 0x0002 && element == 0x0001) 
					{
						byte[] valBytes = reader.ReadBytes((int)length);
						MetaData[tagName] = BitConverter.ToString(valBytes);
					}
					else if (group == 0x0008 && element == 0x0005) 
					{
						byte[] valBytes = reader.ReadBytes((int)length);
						string charSet = Encoding.ASCII.GetString(valBytes).Trim('\0', ' ');
						MetaData[tagName] = charSet;
						UpdateEncodingFromDicomTag(charSet);
					}
					else if (group == 0x0018 && element == 0x0050) 
					{
						byte[] valBytes = reader.ReadBytes((int)length);
						string thicknessStr = Encoding.ASCII.GetString(valBytes).Trim('\0', ' ');
						MetaData[tagName] = thicknessStr;
						if (float.TryParse(thicknessStr, System.Globalization.NumberStyles.Any,
							System.Globalization.CultureInfo.InvariantCulture, out float t))
						{
							SliceThickness = t;
						}
					}
					else if (group == 0x0020 && element == 0x0013) 
					{
						byte[] valBytes = reader.ReadBytes((int)length);
						string instStr = Encoding.ASCII.GetString(valBytes).Trim('\0', ' ');
						MetaData[tagName] = instStr;
						if (int.TryParse(instStr, out int inst))
						{
							InstanceNumber = inst;
						}
					}
					else if (group == 0x0028 && element == 0x0008)
					{
						string depthStr = Encoding.ASCII.GetString(reader.ReadBytes((int)length)).Trim('\0', ' ');
						int.TryParse(depthStr, out int depth);
						Depth = depth > 0 ? depth : 1;
					}
					else if (group == 0x0028 && element == 0x0010)
					{
						Height = ReadAsInt(reader, length);
					}
					else if (group == 0x0028 && element == 0x0011)
					{
						Width = ReadAsInt(reader, length);
					}
					else if (group == 0x0028 && element == 0x0030) 
					{
						byte[] valBytes = reader.ReadBytes((int)length);
						string spacingStr = Encoding.ASCII.GetString(valBytes).Trim('\0', ' ');
						MetaData[tagName] = spacingStr;
						string[] parts = spacingStr.Split('\\');
						if (parts.Length >= 2)
						{
							if (float.TryParse(parts[0], System.Globalization.NumberStyles.Any,
								System.Globalization.CultureInfo.InvariantCulture, out float y))
							{
								PixelSpacingY = y;
							}

							if (float.TryParse(parts[1], System.Globalization.NumberStyles.Any,
								System.Globalization.CultureInfo.InvariantCulture, out float x))
							{
								PixelSpacingX = x;
							}
						}
					}
					else if (group == 0x0028 && element == 0x0100)
					{
						BitsAllocated = ReadAsInt(reader, length);
					}
					else if (group == 0x0028 && element == 0x0103)
					{
						PixelRepresentation = ReadAsInt(reader, length);
					}
					else if (group == 0x7FE0 && element == 0x0010)
					{
						MetaData[tagName] = $"[Pixel Data] {Width}x{Height}x{Depth}";
						ExtractImageData(reader, length);
						break;
					}
					else
					{
						if (length > 0 && length < 10000)
						{
							byte[] valBytes = reader.ReadBytes((int)length);
							MetaData[tagName] = ParseStringData(valBytes, tagName);
						}
						else
						{
							ms.Seek(length, SeekOrigin.Current);
							if (!MetaData.ContainsKey(tagName))
							{
								MetaData[tagName] = $"[Data Size: {length}]";
							}
						}
					}
				}
			}
			return ImageData3D != null;
		}

		/// <summary>
		/// Updates the string encoding format based on the DICOM Specific Character Set tag.
		/// </summary>
		private void UpdateEncodingFromDicomTag(string charSetStr)
		{
			if (string.IsNullOrEmpty(charSetStr)) return;

			string upper = charSetStr.ToUpper();

			try
			{
				if (upper.Contains("ISO_IR 192"))
				{
					currentEncoding = Encoding.UTF8;
				}
				else if (upper.Contains("ISO 2022 IR 87") || upper.Contains("ISO_IR 87"))
				{
					currentEncoding = Encoding.GetEncoding("iso-2022-jp");
				}
				else if (upper.Contains("ISO_IR 13") || upper.Contains("SHIFT_JIS"))
				{
					currentEncoding = Encoding.GetEncoding("shift_jis");
				}
				else if (upper.Contains("ISO_IR 100"))
				{
					currentEncoding = Encoding.GetEncoding("iso-8859-1");
				}
			}
			catch (Exception) { }
		}

		/// <summary>
		/// Parses byte arrays into strings according to the defined encoding, handling escape characters if necessary.
		/// </summary>
		private string ParseStringData(byte[] bytes, string tagName)
		{
			if (bytes == null || bytes.Length == 0) return "";

			bool hasEscape = false;
			foreach (byte b in bytes) if (b == 0x1B) hasEscape = true; 

			Encoding decodeEncoding = currentEncoding;
			if (hasEscape)
			{
				try
				{
					decodeEncoding = Encoding.GetEncoding("iso-2022-jp");
				}
				catch
				{
				}
			}

			string text = decodeEncoding.GetString(bytes).Trim('\0', ' ');
			text = text.Replace("\u001b", ""); 

			return FormatDicomString(text, tagName);
		}

		/// <summary>
		/// Formats specific DICOM strings (e.g., patient names) for better readability.
		/// </summary>
		private string FormatDicomString(string input, string tagName)
		{
			if (string.IsNullOrEmpty(input)) return input;

			input = input.Replace("\\", " / ");

			if (tagName.Contains("Name") || tagName.Contains("Physician"))
			{
				string[] groups = input.Split('=');

				for (int i = 0; i < groups.Length; i++)
				{
					groups[i] = groups[i].Replace("^", " ").Trim();
				}

				if (groups.Length == 3)
				{
					return $"{groups[1]} ({groups[0]} / {groups[2]})";
				}
				else
				{
					if (groups.Length == 2)
					{
						return $"{groups[1]} ({groups[0]})";
					}
				}

				return input.Replace("^", " ");
			}

			return input;
		}

		/// <summary>
		/// Returns a hexadecimal string representation of a DICOM Group and Element tag.
		/// </summary>
		private string GetReadableTagName(ushort group, ushort element)
		{
			return $"{group:X4},{element:X4}"; 
		}

		/// <summary>
		/// Safely reads an integer from the binary stream based on the specified byte length.
		/// </summary>
		private int ReadAsInt(BinaryReader reader, long length)
		{
			if (length == 2) return reader.ReadUInt16();
			if (length == 4) return reader.ReadInt32();
			reader.BaseStream.Seek(length, SeekOrigin.Current);

			return 0;
		}

		/// <summary>
		/// Extracts the raw pixel data from the DICOM binary stream into a 3D float array.
		/// </summary>
		private void ExtractImageData(BinaryReader reader, long length)
		{
			if (Width == 0 || Height == 0) return;

			ImageData3D = new float[Width, Height, Depth];

			for (int z = 0; z < Depth; z++)
			{
				for (int y = 0; y < Height; y++)
				{
					for (int x = 0; x < Width; x++)
					{
						if (BitsAllocated == 16)
						{
							if (PixelRepresentation == 1)
							{
								ImageData3D[x, y, z] = reader.ReadInt16();
							}
							else
							{
								ImageData3D[x, y, z] = reader.ReadUInt16();
							}
						}
						else
						{
							if (BitsAllocated == 8)
							{
								ImageData3D[x, y, z] = reader.ReadByte();
							}
						}
					}
				}
			}
		}
	}

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(ReadDICOM))]
	public class ReadDICOMEditor : ReadModuleTemplateEditor
	{
		SerializedProperty fallbackEncodingProp;

		/// <summary>
		/// Overrides the base class method. Initializes serialized properties specific to DICOM.
		/// </summary>
		protected override void OnEnable()
		{
			base.OnEnable();

			fallbackEncodingProp = serializedObject.FindProperty("fallbackEncoding");
		}

		/// <summary>
		/// Overrides the base class method. Appends the fallback encoding option to the data format settings UI.
		/// </summary>
		protected override void DrawDataFormatSettingsExtension()
		{
			GUILayout.Space(5f);
			EditorGUILayout.PropertyField(fallbackEncodingProp, new GUIContent("Fallback Encoding"));
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

	// =========================================================================
	// Main Class
	// =========================================================================
	public class ReadDICOM : ReadModuleTemplate
	{
		public string fallbackEncoding = "shift_jis";
		public DataField.UpAxis upAxis = DataField.UpAxis.Z;

		public List<DicomMetaEntry> metaDataList = new List<DicomMetaEntry>();

		/// <summary>
		/// Resets the component to its default values and applies component reordering.
		/// </summary>
		protected override void Reset()
		{
#if UNITY_EDITOR
			// Execute the base class component reordering logic.
			base.Reset();
#endif
			sourceType = DataSourceType.FOLDER; // Lock standard data source type to Folder
			dataSource = "";

			useUndefMenu      = true;
			useUndef          = true;
			undef             = -2000;
			usePrecisionMenu  = false;
			useByteswapMenu   = false;
			useHeaderSkipMenu = false;
			upAxis = DataField.UpAxis.Z;
		}

		/// <summary>
		/// Overrides the base class method. Initializes module-specific settings.
		/// </summary>
		public override void InitModule()
		{
		}

		/// <summary>
		/// Overrides the base class method. The main execution function of the module that triggers the folder loading.
		/// </summary>
		public override int BodyFunc()
		{
			if (string.IsNullOrEmpty(dataSource)) return 0;

			StartCoroutine(LoadFolderRoutine());
			
			return 1; 
		}

		/// <summary>
		/// Coroutine that orchestrates the directory scanning, parallel DICOM parsing, and volume construction.
		/// </summary>
		private IEnumerator LoadFolderRoutine()
		{
			List<DicomData> loadedDicomList = new List<DicomData>();

			// Dynamically fetch all files in the target directory (supports both standard IO and Android JNI)
			List<string> files = GetFilesInDirectory(dataSource);

			if (files.Count == 0)
			{
				Debug.LogWarning($"[ReadDICOM] No files found in the specified directory: {dataSource}");
				yield break;
			}

			bool useWebRequest = Application.platform == RuntimePlatform.Android || 
								 Application.platform == RuntimePlatform.WebGLPlayer || 
								 dataSource.Contains("://");

			if (useWebRequest)
			{
				// Sequentially download and parse files via WebRequest for web and mobile architectures
				foreach (var file in files)
				{
					byte[] rawBytes = null;

					yield return StartCoroutine(FetchBinaryRoutine(file, (data) => { rawBytes = data; }));

					if (rawBytes != null)
					{
						DicomData dicom = new DicomData();
						if (dicom.Parse(rawBytes, fallbackEncoding))
						{
							loadedDicomList.Add(dicom);
						}
					}
				}
			}
			else
			{
				// High-performance parallel parsing for PC/Mac utilizing Task.Run and ConcurrentBag
				Task loadTask = Task.Run(() =>
				{
					var tempBag = new ConcurrentBag<DicomData>();

					Parallel.ForEach(files, filePath =>
					{
						try
						{
							// filePath is already an absolute path returned from GetFilesInDirectory
							byte[] rawBytes = File.ReadAllBytes(filePath);
							DicomData dicom = new DicomData();
							if (dicom.Parse(rawBytes, fallbackEncoding))
							{
								tempBag.Add(dicom);
							}
						}
						catch (Exception ex) 
						{
							Debug.LogError($"[ReadDICOM] Failed to load file ({filePath}): {ex.Message}");
						}
					});

					loadedDicomList = tempBag.ToList();
				});

				yield return new WaitUntil(() => loadTask.IsCompleted);

				if (loadTask.IsFaulted)
				{
					Debug.LogError($"[ReadDICOM] An error occurred while loading files: {loadTask.Exception}");
					yield break;
				}
			}

			if (loadedDicomList == null || loadedDicomList.Count == 0)
			{
				Debug.LogWarning("[ReadDICOM] No valid DICOM files could be loaded or parsed.");
				yield break;
			}

			// Sort slices accurately by DICOM Instance Number
			loadedDicomList.Sort((a, b) => a.InstanceNumber.CompareTo(b.InstanceNumber));

			// Extract common metadata for the inspector
			metaDataList.Clear();
			if (loadedDicomList[0].MetaData != null)
			{
				foreach (var kvp in loadedDicomList[0].MetaData)
				{
					metaDataList.Add(new DicomMetaEntry { Tag = kvp.Key, Value = kvp.Value });
				}
			}

			SetupDataFieldElements(loadedDicomList);

			yield return StartCoroutine(CalcStatsForCurrentElementsRoutine());

			ApplyLoadedData();
		}

		/// <summary>
		/// Flattens the 2D slices into a unified 3D volume, generates coordinates, and assigns them to the DataField.
		/// </summary>
		private void SetupDataFieldElements(List<DicomData> loadedDicomList)
		{
			int width  = loadedDicomList[0].Width;
			int height = loadedDicomList[0].Height;
			int depth  = loadedDicomList.Count;

			float spacingX = loadedDicomList[0].PixelSpacingX  > 0 ? loadedDicomList[0].PixelSpacingX  : 1.0f;
			float spacingY = loadedDicomList[0].PixelSpacingY  > 0 ? loadedDicomList[0].PixelSpacingY  : 1.0f;
			float spacingZ = loadedDicomList[0].SliceThickness > 0 ? loadedDicomList[0].SliceThickness : 1.0f;

			float[] volumeValues = new float[width * height * depth];
			int index = 0;

			for (int z = 0; z < depth; z++)
			{
				var sliceData = loadedDicomList[z];

				for (int y = height - 1; y >= 0; y--)
				{
					for (int x = 0; x < width; x++)
					{
						volumeValues[index] = sliceData.ImageData3D[x, y, 0];
						index++;
					}
				}
			}

			int vlen = 1; 
			df.CreateElements(vlen);
			df.upAxis = this.upAxis;

			List<int> dims = new List<int> { width, height, depth };
			
			List<float>[] coords = new List<float>[4];

			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}

			for (int i = 0; i < width; i++)
			{
				coords[0].Add(i * spacingX);
			}

			for (int j = 0; j < height; j++)
			{
				coords[1].Add(j * spacingY);
			}

			for (int k = 0; k < depth; k++)
			{
				coords[2].Add(k * spacingZ);
			}

			for (int k = 0; k < depth; k++)
			{
				for (int j = 0; j < height; j++)
				{
					for (int i = 0; i < width; i++)
					{
						coords[3].Add(coords[0][i]);
						coords[3].Add(coords[1][j]);
						coords[3].Add(coords[2][k]);
					}
				}
			}

			for (int i = 0; i < vlen; i++)
			{
				df.elements[i].SetDims(dims);
				df.elements[i].SetCoords(coords);
				
				if (useUndef) df.elements[i].SetUndef(undef);
				
				df.elements[i].SetValues(volumeValues); 
				
				df.elements[i].SetFieldType(FieldType.RECTILINEAR);
				df.elements[i].SetVarName("CT_Density");
				df.elements[i].SetActive(true);
			}
		}
	}
}