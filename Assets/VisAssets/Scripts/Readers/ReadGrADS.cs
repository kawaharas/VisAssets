using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using SimpleFileBrowser;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets
{
#if UNITY_EDITOR
	[CustomEditor(typeof(ReadGrADS))]
	public class ReadGrADSEditor : Editor
	{
		SerializedProperty filename;
		SerializedProperty loadAtStartup;
		SerializedProperty useEmbeddedData;

		public void OnEnable()
		{
			filename = serializedObject.FindProperty("filename");
			loadAtStartup = serializedObject.FindProperty("loadAtStartup");
			useEmbeddedData = serializedObject.FindProperty("useEmbeddedData");
		}

		public override void OnInspectorGUI()
		{
			var readField = target as ReadField;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.LabelField("Filename:");
			filename.stringValue = EditorGUILayout.TextField(filename.stringValue);
			GUILayout.Space(10f);
			loadAtStartup.boolValue = EditorGUILayout.ToggleLeft("Load At Startup", loadAtStartup.boolValue);
			GUILayout.Space(5f);
			useEmbeddedData.boolValue = EditorGUILayout.ToggleLeft("Use Embedded Data", useEmbeddedData.boolValue);
			GUILayout.Space(5f);

			var message = new GUIContent("If you use an embedded data, it must be placed in \"Assets/StreamingAssets\".");
			EditorGUILayout.BeginHorizontal(GUI.skin.box);
			GUILayout.Space(5f);
			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.alignment = TextAnchor.MiddleLeft;
			style.wordWrap = true;
			style.fontSize = 10;
			style.CalcSize(message);
			GUI.backgroundColor = Color.white;
			EditorGUILayout.LabelField(message, style);
			GUILayout.Space(5f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class ReadGrADS : ReadModuleTemplate
	{
		[SerializeField]
		public float[] offsets;
		float scale;
		float[] axis_width;
		[SerializeField]
		float minx, maxx;
		[SerializeField]
		float miny, maxy;
		[SerializeField]
		float minz, maxz;

		public string filename = string.Empty;
		public string ctlfile  = string.Empty;
		public bool useEmbeddedData;
		byte[] bytedata;

		int[]  dims;
		int    varnum;
		string datafile;
		List<VarInfo> varInfo;
		float  undef;
		bool   useUndef;
		bool[] options;
		int header_size;

//		int m_CurrentStep = 0;

		List<float>[] coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
		public string debugString = string.Empty;

		public enum Options
		{
			PASCALS = 0,
			YREV,
			ZREV,
			TEMPLATE,
			SEQUENTIAL,
			DAY_CALENDAR, // 365_DAY_CALANDAR
			BYTESWAPPED,
			BIG_ENDIAN,
			LITTLE_ENDIAN,
			CRAY_32BIT_IEEE,
			UNDEFINED
		}

		struct VarInfo
		{
			public string varName;
			public int    levs;
//			public int    additional_codes; // GrADS version 2.0.2 or later
			public string units; // not implemented yet
			public string description;
		}

		public override void InitModule()
		{
			offsets = new float[3];
			axis_width = new float[3];
		}

		public override int BodyFunc()
		{
			StartCoroutine(Load());
			return 1;
		}

		public void Exec()
		{
			if (filename != "")
			{
				activation.SetParameterChanged(1);
			}
		}

		private IEnumerator ReadCtlFile(string filename, Action<string> callback = null)
		{
			string url;

			if (Application.platform == RuntimePlatform.Android)
			{
				if (useEmbeddedData)
				{
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
					if (url.Contains("://"))
					{
						var www = new UnityWebRequest(url);
						www.downloadHandler = new DownloadHandlerBuffer();
						yield return www.SendWebRequest();
						ctlfile = www.downloadHandler.text;
					}
					else
					{
						ctlfile = FileBrowserHelpers.ReadTextFromFile(url);
					}
				}
				else
				{
					url = filename;
					ctlfile = FileBrowserHelpers.ReadTextFromFile(url);
				}
				debugString += url + '\n';

//				UIPanel.transform.Find("DebugText").GetComponent<Text>().text = debugString;
			}
			else
			{
				if (useEmbeddedData)
				{
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
				}
				else
				{
					url = "file://" + filename;
				}
				var www = UnityWebRequest.Get(url);
				yield return www.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
				if (www.result == UnityWebRequest.Result.ProtocolError ||
					www.result == UnityWebRequest.Result.ConnectionError)
#else
				if (www.isHttpError || www.isNetworkError)
#endif
				{
					Debug.Log(www.error);
				}
				else
				{
					ctlfile = www.downloadHandler.text;
					if (callback != null)
					{
						callback(www.downloadHandler.text);
					}
				}
			}
		}

		private IEnumerator ReadBinary(string filename, Action<byte[]> callback = null)
		{
			string url;

			if (Application.platform == RuntimePlatform.Android)
			{
				if (useEmbeddedData)
				{
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
					if (url.Contains("://"))
					{
						var www = new UnityWebRequest(url);
						www.downloadHandler = new DownloadHandlerBuffer();
						yield return www.SendWebRequest();
						bytedata = www.downloadHandler.data;
					}
					else
					{
						bytedata = FileBrowserHelpers.ReadBytesFromFile(url);
					}
				}
				else
				{
					url = filename;
					bytedata = FileBrowserHelpers.ReadBytesFromFile(url);
				}
				debugString += url + '\n';

//				UIPanel.transform.Find("DebugText").GetComponent<Text>().text = debugString;
			}
			else
			{
				if (useEmbeddedData)
				{
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
				}
				else
				{
					url = "file://" + filename;
				}
				var www = new UnityWebRequest(url);
				www.downloadHandler = new DownloadHandlerBuffer();
				yield return www.SendWebRequest();
				debugString += url + '\n';

//				UIPanel.transform.Find("DebugText").GetComponent<Text>().text = debugString;

#if UNITY_2020_2_OR_NEWER
				if (www.result == UnityWebRequest.Result.ProtocolError ||
					www.result == UnityWebRequest.Result.ConnectionError)
#else
				if (www.isHttpError || www.isNetworkError)
#endif
				{
					Debug.Log(www.error);
				}
				else
				{
					bytedata = www.downloadHandler.data;
					if (callback != null)
					{
						callback(www.downloadHandler.data);
					}
				}
			}
		}

		private void ResponseCallbackText(string inputdata)
		{
			ctlfile = inputdata;
		}

		private void ResponseCallbackBinary(byte[] inputdata)
		{
			bytedata = inputdata;
		}

		private IEnumerator Load()
		{
			yield return StartCoroutine(ReadCtlFile(filename, ResponseCallbackText));

			dims = new int[4];
			varnum = 0;
			varInfo = new List<VarInfo>();
			useUndef = false;
			coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
			header_size = 0;

			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}
			var option_num = Enum.GetNames(typeof(Options)).Length;
			options = new bool[option_num];
			for (int i = 0; i < option_num; i++)
			{
				options[i] = false;
			}
			string filePath = System.IO.Path.GetDirectoryName(filename);

//			MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(ctlfile));
			MemoryStream memoryStream = new MemoryStream();
			StreamWriter writer = new StreamWriter(memoryStream);
			writer.Write(ctlfile);
			writer.Flush();
			memoryStream.Position = 0;
//			StreamReader streamReader = new StreamReader(memoryStream, Encoding.GetEncoding("Shift_JIS"));
			StreamReader streamReader = new StreamReader(memoryStream);

			while (streamReader.Peek() != -1)
			{
				string line = streamReader.ReadLine();
				string[] stringList = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
				switch (stringList[0].ToUpper())
				{
					case "XDEF":
						SetCoord(0, stringList, streamReader);
						break;
					case "YDEF":
						SetCoord(1, stringList, streamReader);
						break;
					case "ZDEF":
						SetCoord(2, stringList, streamReader);
						break;
					case "TDEF":
						dims[3] = Convert.ToInt32(stringList[1]);
						// not implemented yet
						break;
					case "UNDEF":
						useUndef = true;
						undef = Convert.ToSingle(stringList[1]);
						break;
					case "DSET":
						string tmpString = System.IO.Path.Combine(filePath, stringList[1]);
						datafile = tmpString.Replace('\\', '/').Replace("^", "");
						Debug.Log("DSET : " + datafile);
						break;
					case "VARS":
						varnum = Convert.ToInt32(stringList[1]);
						for (int n = 0; n < varnum; n++)
						{
							line = streamReader.ReadLine();
							stringList = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
							VarInfo _varInfo;
							_varInfo.varName = stringList[0];
							_varInfo.levs    = Convert.ToInt32(stringList[1]);
							_varInfo.units   = stringList[2];
							_varInfo.description = string.Join(" ", stringList, 3, stringList.Length - 3);
							varInfo.Add(_varInfo);
							Debug.Log(line);
						}
						line = streamReader.ReadLine();
						stringList = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
						if (stringList[0].ToUpper() != "ENDVARS")
						{
							Debug.Log("error!!");
						}
						break;
					case "OPTIONS":
						for (int n = 1; n < stringList.Length; n++)
						{
							switch (stringList[n].ToUpper())
							{
								case "PASCALS":
									options[(int)Options.PASCALS] = true;
									break;
								case "YREV":
									options[(int)Options.YREV] = true;
									break;
								case "ZREV":
									options[(int)Options.ZREV] = true;
									break;
								case "TEMPLATE":
									options[(int)Options.TEMPLATE] = true;
									break;
								case "SEQUENTIAL":
									options[(int)Options.SEQUENTIAL] = true;
									break;
								case "365_DAY_CALENDAR":
									options[(int)Options.DAY_CALENDAR] = true;
									break;
								case "BYTESWAPPED":
									options[(int)Options.BYTESWAPPED] = true;
									break;
								case "BIG_ENDIAN":
									options[(int)Options.BIG_ENDIAN] = true;
									break;
								case "LITTLE_ENDIAN":
									options[(int)Options.LITTLE_ENDIAN] = true;
									break;
								case "CRAY_32BIT_IEEE":
									options[(int)Options.CRAY_32BIT_IEEE] = true;
									break;
								default:
									break;
							}
						}
						break;
					case "FILEHEADER":
						header_size = Convert.ToInt32(stringList[1]);
						break;
					default:
						break;
				}
			}
			streamReader.Close();
			memoryStream.Close();

			// merge coordinates to 4th list
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

			df.CreateElements(varnum);

			SetParamsToDataElements();

			yield return StartCoroutine(ReadBinary(datafile, ResponseCallbackBinary));

			int step = 0;
			SetData(step);

			CalcOffsets();

			// turn on flag when data loading is complete
			df.dataLoaded = true;

			// this function must be called at this location
			SetCoordinateSystem();
		}

		private void SetParamsToDataElements()
		{
			for (int i = 0; i < varnum; i++)
			{
				if (varInfo[i].levs == dims[2])
				{
					df.elements[i].SetDims(dims[0], dims[1], dims[2]);
					df.elements[i].SetSteps(dims[3]);
					df.elements[i].SetCoords(coords);
				}
				else
				{
					int levels = varInfo[i].levs;
					if (levels == 0)
					{
						levels = 1;
					}
					List<float>[] coords_tmp = new List<float>[4];
					coords_tmp[0] = new List<float>(coords[0]);
					coords_tmp[1] = new List<float>(coords[1]);
					coords_tmp[2] = new List<float>();
					for (int n = 0; n < levels; n++)
					{
						coords_tmp[2].Add(coords[2][n]);
					}
					coords_tmp[3] = new List<float>();
					// merge coordinates to 4th list
					for (int z = 0; z < levels; z++)
					{
						for (int y = 0; y < dims[1]; y++)
						{
							for (int x = 0; x < dims[0]; x++)
							{
								coords_tmp[3].Add(coords[0][x]);
								coords_tmp[3].Add(coords[1][y]);
								coords_tmp[3].Add(coords_tmp[2][z]);
							}
						}
					}
					df.elements[i].SetDims(dims[0], dims[1], levels);
					df.elements[i].SetSteps(dims[3]);
					df.elements[i].SetCoords(coords_tmp);
				}
				df.elements[i].SetFieldType(DataElement.FieldType.RECTILINEAR);
				df.elements[i].varName = varInfo[i].description;
				if (useUndef)
				{
					df.elements[i].SetUndef(undef);
				}
				df.elements[i].SetActive(true);
			}

			InitAnimator();
		}

		private void CalcOffsets()
		{
			// calculate offsets and the normalized scale
			float[] axis_min   = new float[3];
			float[] axis_max   = new float[3];
			float[] axis_width = new float[3];
			float max_width = float.MinValue;

			for (int i = 0; i < 3; i++)
			{
				float v0 = coords[i][0];
				float v1 = coords[i][dims[i] - 1];
				axis_width[i] = Mathf.Abs(v1 - v0);
				max_width = Math.Max(max_width, axis_width[i]);
				offsets[i] = v0 + (v1 - v0) / 2f;
				axis_min[i] = coords[i].Min();
				axis_max[i] = coords[i].Max();
			}

			double EARTH_RADIUS_NS_WGS84 = 6356752.3142;
			double EARTH_RADIUS_WE_WGS84 = 6378137.0;
			double distX = 2.0 * Math.PI * EARTH_RADIUS_WE_WGS84
					* Math.Cos((axis_min[1] + (axis_max[1] - axis_min[1]) / 2.0) / 180.0 * Math.PI)
					/ 360.0 * (axis_max[0] - axis_min[0]);
			double distY = 2.0 * Math.PI * EARTH_RADIUS_NS_WGS84 / 360.0 * (axis_max[1] - axis_min[1]);
			double distZ = axis_max[2] - axis_min[2];
			double maxDist = Math.Max(Math.Max(distX, distY), distZ);
			float  ScaleX  = 10f / axis_width[0] * (float)(distX / maxDist);
			float  ScaleY  = 10f / axis_width[1] * (float)(distY / maxDist);
			float  ScaleZ  = 10f / axis_width[2] * (float)(distZ / maxDist);
			float  ScaleZ_SingleLayer = (float)((double)minz / maxDist);

			if (scale == 0)
			{
				scale = 1f / max_width * 6f;
			}
			foreach (Transform child in transform)
			{
				child.gameObject.transform.localPosition = new Vector3(-offsets[0], -offsets[1], -offsets[2]);
			}
			transform.localScale = new Vector3(ScaleX, ScaleY, -ScaleZ * 10000f); // if z-axis is pressure coordinates
		}

		public override void SetData(int step)
		{
			try
			{
				using (MemoryStream memoryStream = new MemoryStream(bytedata))
				using (BinaryReader reader = new BinaryReader(memoryStream))
				{
					if (reader != null)
					{
						int skipbytes = 0;
						for (int i = 0; i < df.elements.Length; i++)
						{
							skipbytes += df.elements[i].size * sizeof(float);
						}
						if (reader.BaseStream.CanSeek)
						{
							reader.BaseStream.Position = header_size + (skipbytes * step);
						}

						for (int i = 0; i < varnum; i++)
						{
							var values = new List<float>();
							var size = df.elements[i].size;
							var datasize = size * sizeof(float);
							var buffer = new byte[datasize];
							var length = reader.Read(buffer, 0, datasize);
							if (length != datasize)
							{
								Debug.Log("ERROR: Failed to load data.");
							}
							for (int n = 0; n < size; n++)
							{
								if (options[(int)Options.BIG_ENDIAN])
								{
									Array.Reverse(buffer, n * sizeof(float), sizeof(float));
								}
								var value = BitConverter.ToSingle(buffer, n * sizeof(float));
								values.Add(value);
							}
							df.elements[i].SetValues(values);
						}
					}
				}
			}
			catch (IOException exception)
			{
				Debug.Log("Exception : " + exception.Message);
			}
		}

		private void SetCoord(int i, string[] stringList, StreamReader streamReader)
		{
			dims[i] = Convert.ToInt32(stringList[1]);
			string mapping = stringList[2].ToUpper();
			if (mapping == "LINEAR")
			{
				double start = Convert.ToDouble(stringList[3]);
				double delta = Convert.ToDouble(stringList[4]);
				for (int n = 0; n < dims[i]; n++)
				{
					coords[i].Add((float)(start + (double)n * delta));
				}
			}
			else if (mapping == "LEVELS")
			{
				int count = 0;
				if (stringList.Length > 3)
				{
					for (int n = 3; n < stringList.Length; n++)
					{
						coords[i].Add(Convert.ToSingle(stringList[n]));
						count++;
					}
				}
				if (count < dims[i])
				{
					while (true)
					{
						string line = streamReader.ReadLine();
						stringList = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
						for (int n = 0; n < stringList.Length; n++)
						{
							coords[i].Add(Convert.ToSingle(stringList[n]));
							count++;
						}
						if (count >= dims[i])
						{
							break;
						}
					}
				}

				// TEST
				if (coords[i].First() > coords[i].Last())
				{
//					coords[i].Reverse();
					Debug.Log("coordinate was reversed.");
				}
			}
			else
			{
				// return error_code
			}
		}
/*
		private unsafe void ByteSwap4(byte[] data, int datasize)
		{
			fixed (byte* bytePtr = data)
			{
				var number_of_data = datasize / sizeof(float);
				var fdata = (float*)bytePtr;
				for (int i = 0; i < number_of_data; i++)
				{
					byte bit0, bit1, bit2, bit3;
					float* data_ptr = &fdata[i];
					byte* bit0p = (byte*)data_ptr;
					byte* bit1p = bit0p + 1;
					byte* bit2p = bit0p + 2;
					byte* bit3p = bit0p + 3;

					bit0 = *bit0p;
					bit1 = *bit1p;
					bit2 = *bit2p;
					bit3 = *bit3p;

					*bit0p = bit3;
					*bit1p = bit2;
					*bit2p = bit1;
					*bit3p = bit0;
				}
			}
		}
*/
	}
}