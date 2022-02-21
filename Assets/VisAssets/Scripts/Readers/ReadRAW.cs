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

namespace VisAssets.SciVis.Structured.DataLoader
{
#if UNITY_EDITOR
	[CustomEditor(typeof(ReadRAW))]
	public class ReadRAWEditor : Editor
	{
		#region custom inspector
		SerializedProperty filename;
		SerializedProperty dims;
		SerializedProperty varname;
		SerializedProperty precision;
		SerializedProperty byteswap;
		SerializedProperty header; // record marker of fortran
		SerializedProperty loadAtStartup;
		SerializedProperty useEmbeddedData;
		SerializedProperty centering;
		SerializedProperty autoResize;

		public void OnEnable()
		{
			filename        = serializedObject.FindProperty("filename");
			dims            = serializedObject.FindProperty("dims");
			varname         = serializedObject.FindProperty("varname");
			precision       = serializedObject.FindProperty("precision");
			byteswap        = serializedObject.FindProperty("byteswap");
			header          = serializedObject.FindProperty("header");
			loadAtStartup   = serializedObject.FindProperty("loadAtStartup");
			useEmbeddedData = serializedObject.FindProperty("useEmbeddedData");
			centering       = serializedObject.FindProperty("centering");
			autoResize      = serializedObject.FindProperty("autoResize");
		}

		public override void OnInspectorGUI()
		{
			var readRAW = target as ReadRAW;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Filename:");
			filename.stringValue = EditorGUILayout.TextField(filename.stringValue);
			GUILayout.Space(10f);
			dims.vector3IntValue = EditorGUILayout.Vector3IntField("Size", dims.vector3IntValue);
			GUILayout.Space(10f);
			varname.stringValue = EditorGUILayout.TextField("Variable Name", varname.stringValue);
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

			GUILayout.Space(5f);
			centering.boolValue = EditorGUILayout.ToggleLeft("Centering", centering.boolValue);
			GUILayout.Space(5f);
			EditorGUI.BeginDisabledGroup(!centering.boolValue);
			autoResize.boolValue = EditorGUILayout.ToggleLeft("Auto Resize", autoResize.boolValue);
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(10f);
			if (GUILayout.Button("Load Default Values"))
			{
				filename.stringValue = string.Empty;
				dims.vector3IntValue = Vector3Int.zero;
				varname.stringValue = string.Empty;
				precision.enumValueIndex = 0;
				byteswap.boolValue = false;
				header.boolValue = false;
				loadAtStartup.boolValue = false;
				useEmbeddedData.boolValue = false;
				centering.boolValue = false;
				autoResize.boolValue = false;
			}
			EditorGUILayout.Space();
//			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
			}
			serializedObject.ApplyModifiedProperties();

//			base.OnInspectorGUI();
		}
		#endregion // custom inspector
	}
#endif

	public class ReadRAW : ReadModuleTemplate
	{
		public enum PRECISION
		{
			SINGLE,
			DOUBLE
		};

		[SerializeField]
		public Vector3Int dims;
		public string filename = string.Empty;
		public string varname  = string.Empty;
		public PRECISION precision = PRECISION.DOUBLE;
		public bool byteswap = false;
		public bool header = true; // true: with header, false: without header
		public bool useEmbeddedData;

		byte[] bytedata;

		public bool useDebugString = false;
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
/*
		public void Exec()
		{
			if (filename != "")
			{
				activation.SetParameterChanged(1);
			}
		}
*/
		IEnumerator Load()
		{
			debugString = string.Empty;

			var dimension = dims[0] * dims[1] * dims[2];

			var coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}

			// set coordinate of each axis as uniform data
			for (int i = 0; i < 3; i++)
			{
				for (int n = 0; n < dims[i]; n++)
				{
					coords[i].Add((float)n);
				}
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
					}
				}
			}

			df.CreateElements(1);

			List<float> values = new List<float>();

			yield return StartCoroutine(LoadBinaryData(
				filename, values, dimension, header, byteswap));

			df.elements[0].SetDims(dims);
//			df.elements[0].SetDims(dims[0], dims[1], dims[2]);
			df.elements[0].SetCoords(coords);
			df.elements[0].SetValues(values);
//			df.elements[0].varName = varname.Replace("\\n", " ");
			df.elements[0].SetVarName(varname);
			df.elements[0].SetFieldType(DataElement.FieldType.UNIFORM);
			df.elements[0].SetActive(true);

			// turn on flag when data loading is complete
			df.dataLoaded = true;

			if (centering)
			{
				Centering(autoResize);
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
					url = url.Replace('\\', '/');

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
//				debugString += url + '\n';

				ShowDebugString();
			}
			else
			{
				if (useEmbeddedData)
				{
					Debug.Log("filename in LoadBinaryData = " + filename);
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
					url = "file://" + url.Replace('\\', '/');
					Debug.Log("url in LoadBinaryData = " + url);
//					url = filename;
#if UNITY_EDITOR
//					url = "file://" + Application.streamingAssetsPath + '/' + filename;
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
//				debugString += url + '\n';

				ShowDebugString();

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
								value = BitConverter.ToSingle(tmp, 0);
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

		int GetIndex(int i, int j, int k)
		{
			int index = dims[1] * dims[0] * k + dims[0] * j + i;

			return index;
		}

		void ShowDebugString()
		{
			if (!useDebugString) return;

			if (UIPanel == null) return;

			var debugText = UIPanel.transform.Find("DebugText");
			if (debugText == null) return;

			var text = debugText.GetComponent<Text>();
			if (text == null) return;

			text.text = debugString;
		}
	}
}
