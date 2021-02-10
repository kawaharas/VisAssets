using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using SimpleFileBrowser;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif
using UnityEngine.UI;

namespace VIS
{
#if UNITY_EDITOR
	[CustomEditor(typeof(ReadV5))]
	public class ReadV5Editor : Editor
	{
		SerializedProperty filename;
		SerializedProperty precision;
		SerializedProperty byteswap;
		SerializedProperty header;
		SerializedProperty loadAtStartup;
		SerializedProperty useEmbeddedData;

		public void OnEnable()
		{
			filename = serializedObject.FindProperty("filename");
			precision = serializedObject.FindProperty("precision");
			byteswap = serializedObject.FindProperty("byteswap");
			header = serializedObject.FindProperty("header");
			loadAtStartup = serializedObject.FindProperty("loadAtStartup");
			useEmbeddedData = serializedObject.FindProperty("useEmbeddedData");
		}

		public override void OnInspectorGUI()
		{
			var readField = target as ReadField;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Filename:");
			filename.stringValue = EditorGUILayout.TextField(filename.stringValue);
			GUILayout.Space(10f);
			var label = new GUIContent("Precision:");
			EditorGUILayout.PropertyField(precision, label, true);
			GUILayout.Space(5f);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(10.0f);
			byteswap.boolValue = EditorGUILayout.ToggleLeft("Byteswap", byteswap.boolValue, GUILayout.MaxWidth(100.0f));
			header.boolValue = EditorGUILayout.ToggleLeft("Header", header.boolValue, GUILayout.MaxWidth(100.0f));
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

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

	public class ReadV5 : ReadModuleTemplate
	{
		public enum PRECISION
		{
			SINGLE,
			DOUBLE
		};

		public string filename = string.Empty;
		public PRECISION precision = PRECISION.DOUBLE;
		public bool byteswap = false;
		public bool header = true; // true: with header, false: without header
		public bool useEmbeddedData;

		string v5file;
		byte[] bytedata;

		string debugString = string.Empty;

		public override void InitModule()
		{
			// upper direction is defined as z-axis in VFIVE.
			transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));
		}

		public override int BodyFunc()
		{
			if (filename == "") return 0;

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

		IEnumerator Load()
		{
			yield return StartCoroutine(ReadV5File(filename, ResponseCallbackText));

			header = true;
			int[] dims = new int[3];
			int scalars;
			int vectors;
			float scale = 0f;
			List<string> label = new List<string>();
			List<string> datafile = new List<string>();
			string[] coordfile = new string[3];
			string filePath = System.IO.Path.GetDirectoryName(filename);

			MemoryStream memoryStream = new MemoryStream();
			StreamWriter writer = new StreamWriter(memoryStream);
			writer.Write(v5file);
			writer.Flush();
			memoryStream.Position = 0;
			StreamReader streamReader = new StreamReader(memoryStream);

			while (streamReader.Peek() != -1)
			{
				string line = streamReader.ReadLine();
				string[] stringList = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
				if (stringList.Length != 0)
				{
					string keyword = stringList[0].ToUpper();
					if (keyword.Contains("SCALE"))
					{
						scale = Convert.ToSingle(stringList[1]);
					}
					else if (keyword.Contains("SCAL") && keyword.IndexOf("SCAL") == 0)
					{
						string indexString = Regex.Replace(keyword, @"[^0-9]", "");
						int index = Convert.ToInt32(indexString);
						if (keyword.Contains("_LABEL"))
						{
							label.Add(stringList[1]);
						}
						else if (keyword.Contains("SCALMIN"))
						{
							// not implemented yet
						}
						else if (keyword.Contains("SCALMAX"))
						{
							// not implemented yet
						}
						else
						{
							var tmpStr = string.Empty;
							if (stringList[1].StartsWith("./"))
							{
								tmpStr = stringList[1].Remove(0, 2);
								stringList[1] = tmpStr;
							}
							tmpStr = (filePath + '/' + stringList[1]).Replace('\\', '/');
							datafile.Add(tmpStr);
						}
					}
					else if (keyword.Contains("VECT") && keyword.IndexOf("VECT") == 0)
					{
						string indexString = keyword.Replace("VECT", "").Replace("_LABEL", "");
						char axisString = indexString[indexString.Length - 1];
						int index = Convert.ToInt32(Regex.Replace(indexString, @"[^0-9]", ""));
						switch (axisString)
						{
							case 'X':
								index *= 3;
								break;
							case 'Y':
								index *= 3;
								index += 1;
								break;
							case 'Z':
								index *= 3;
								index += 2;
								break;
							default:
								break;
						}
						if (keyword.Contains("_LABEL"))
						{
							label.Add(stringList[1] + " (U)");
							label.Add(stringList[1] + " (V)");
							label.Add(stringList[1] + " (W)");
						}
						else if (keyword.Contains("VECTMIN"))
						{
							// not implemented yet
						}
						else if (keyword.Contains("VECTMAX"))
						{
							// not implemented yet
						}
						else
						{
							var tmpStr = string.Empty;
							if (stringList[1].StartsWith("./"))
							{
								tmpStr = stringList[1].Remove(0, 2);
								stringList[1] = tmpStr;
							}
							datafile.Add((filePath + '/' + stringList[1]).Replace('\\', '/'));
						}
					}
					else
					{
						switch (stringList[0].ToUpper())
						{
							case "NOSKIP4":
								int flag = Convert.ToInt32(stringList[1]);
								if (flag == 1)
								{
									header = false;
								}
								break;
							case "SCALE":
								scale = Convert.ToInt32(stringList[1]);
								break;
							case "N1":
								dims[0] = Convert.ToInt32(stringList[1]);
								break;
							case "N2":
								dims[1] = Convert.ToInt32(stringList[1]);
								break;
							case "N3":
								dims[2] = Convert.ToInt32(stringList[1]);
								break;
							case "NSCAL":
								scalars = Convert.ToInt32(stringList[1]);
								break;
							case "NVEC":
								vectors = Convert.ToInt32(stringList[1]);
								break;
							case "XFILE":
								if (stringList[1].StartsWith("./"))
								{
									var tmpStr = stringList[1].Remove(0, 2);
									stringList[1] = tmpStr;
								}
								coordfile[0] = (filePath + '/' + stringList[1]).Replace('\\', '/');
								break;
							case "YFILE":
								if (stringList[1].StartsWith("./"))
								{
									var tmpStr = stringList[1].Remove(0, 2);
									stringList[1] = tmpStr;
								}
								coordfile[1] = (filePath + '/' + stringList[1]).Replace('\\', '/');
								break;
							case "ZFILE":
								if (stringList[1].StartsWith("./"))
								{
									var tmpStr = stringList[1].Remove(0, 2);
									stringList[1] = tmpStr;
								}
								coordfile[2] = (filePath + '/' + stringList[1]).Replace('\\', '/');
								break;
							default:
								break;
						}
					}
				}
			}
			streamReader.Close();
			memoryStream.Close();

			List<float>[] coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
			List<Vector3> coords3d = new List<Vector3>();
			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}
			for (int i = 0; i < 3; i++)
			{
				yield return StartCoroutine(LoadBinaryData(coordfile[i], coords[i], dims[i], header, byteswap));
			}

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
						coords3d.Add(new Vector3(coords[0][i], coords[1][j], coords[2][k]));
					}
				}
			}

			// set data to datafield
			df.CreateElements(datafile.Count);
			for (int i = 0; i < datafile.Count; i++)
			{
				df.elements[i].SetDims(dims[0], dims[1], dims[2]);

				List<float> values = new List<float>();

				yield return StartCoroutine(LoadBinaryData(datafile[i], values, dims[0] * dims[1] * dims[2], header, byteswap));

				df.elements[i].SetCoords(coords);
				df.elements[i].SetValues(values);
				df.elements[i].SetFieldType(DataElement.FieldType.RECTILINEAR);
				df.elements[i].varName = label[i].Replace("\\n", " ");
				df.elements[i].SetActive(true);
			}

			// turn on flag when data loading is complete
			df.dataLoaded = true;

			Centering(true);
		}

		private IEnumerator ReadV5File(string filename, Action<string> callback = null)
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
						v5file = www.downloadHandler.text;
					}
					else
					{
						v5file = FileBrowserHelpers.ReadTextFromFile(url);
					}
				}
				else
				{
					url = filename;
					v5file = FileBrowserHelpers.ReadTextFromFile(url);
				}
				debugString += url + '\n';

				UIPanel.transform.Find("DebugText").GetComponent<Text>().text = debugString;
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

				if (www.isHttpError || www.isNetworkError)
				{
					Debug.Log(www.error);
				}
				else
				{
					v5file = www.downloadHandler.text;
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

				UIPanel.transform.Find("DebugText").GetComponent<Text>().text = debugString;
			}
			else
			{
				if (useEmbeddedData)
				{
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename).Replace('\\', '/');
//				Debug.Log(Application.streamingAssetsPath + filename);
					url = Application.streamingAssetsPath + filename;
#if UNITY_EDITOR
					url = "file://" + Application.streamingAssetsPath + filename;
#endif
				}
				else
				{
					url = "file://" + filename;
				}
				Debug.Log(url);
				var www = new UnityWebRequest(url);
				www.downloadHandler = new DownloadHandlerBuffer();
				yield return www.SendWebRequest();
				debugString += url + '\n';

				UIPanel.transform.Find("DebugText").GetComponent<Text>().text = debugString;

				if (www.isHttpError || www.isNetworkError)
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
			v5file = inputdata;
		}

		private void ResponseCallbackBinary(byte[] inputdata)
		{
			bytedata = inputdata;
		}

		private IEnumerator LoadBinaryData(string filename, List<float> v, int size, bool header = true, bool byteswap = false)
		{
			yield return StartCoroutine(ReadBinary(filename, ResponseCallbackBinary));

			using (MemoryStream memoryStream = new MemoryStream(bytedata))
			using (BinaryReader reader = new BinaryReader(memoryStream))
			{
				if (reader != null)
				{
					if (header)
					{
						// read record length of fortran file (unformatted-sequential).
						var bytes = reader.ReadBytes(4);
						// byteswap is necessary because the record length of VFIVE sample data
						// (little-endian ver.) is big-endian.
						Array.Reverse(bytes);
						uint record = BitConverter.ToUInt32(bytes, 0);
					}

					int dataLength = sizeof(float);
					if (precision == PRECISION.DOUBLE)
					{
						dataLength = sizeof(double);
					}
					var buffer = new byte[size * dataLength];
					var length = reader.Read(buffer, 0, size * dataLength);
					if (length != size * dataLength)
					{
						Debug.Log("ERROR: Failed to load data.");
					}

					for (int n = 0; n < size; n++)
					{
						float value = 0f;
						if (precision == PRECISION.SINGLE)
						{
							if (byteswap)
							{
								byte[] tmp = new byte[4];
								tmp[3] = buffer[n * dataLength + 0];
								tmp[2] = buffer[n * dataLength + 1];
								tmp[1] = buffer[n * dataLength + 2];
								tmp[0] = buffer[n * dataLength + 3];
								value = (float)BitConverter.ToDouble(tmp, 0);
							}
							else
							{
								value = BitConverter.ToSingle(buffer, n * dataLength);
							}
						}
						else
						{
							if (byteswap)
							{
								byte[] tmp = new byte[8];
								tmp[7] = buffer[n * dataLength + 0];
								tmp[6] = buffer[n * dataLength + 1];
								tmp[5] = buffer[n * dataLength + 2];
								tmp[4] = buffer[n * dataLength + 3];
								tmp[3] = buffer[n * dataLength + 4];
								tmp[2] = buffer[n * dataLength + 5];
								tmp[1] = buffer[n * dataLength + 6];
								tmp[0] = buffer[n * dataLength + 7];
								value = (float)BitConverter.ToDouble(tmp, 0);
							}
							else
							{
								value = (float)BitConverter.ToDouble(buffer, n * dataLength);
							}
						}
						v.Add(value);
					}
				}
			}
		}

		public void SetPrecision(int mode)
		{
			precision = (PRECISION)mode;

			ParameterChanged();
		}
	}
}
