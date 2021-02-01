using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SimpleFileBrowser;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
	using FieldType = DataElement.FieldType;

#if UNITY_EDITOR
	[CustomEditor(typeof(ReadField))]
	public class ReadFieldEditor : Editor
	{
		SerializedProperty filename;
		SerializedProperty loadAtStartup;
		SerializedProperty useEmbeddedData;
		SerializedProperty useDummyData;

		public void OnEnable()
		{
			filename        = serializedObject.FindProperty("filename");
			loadAtStartup   = serializedObject.FindProperty("loadAtStartup");
			useEmbeddedData = serializedObject.FindProperty("useEmbeddedData");
			useDummyData    = serializedObject.FindProperty("useDummyData");
		}

		public override void OnInspectorGUI()
		{
			var readField = target as ReadField;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.Space();
			filename.stringValue = EditorGUILayout.TextField("Filename:", filename.stringValue);
			GUILayout.Space(5f);
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

			EditorGUI.BeginDisabledGroup(useEmbeddedData.boolValue);
			useDummyData.boolValue = EditorGUILayout.ToggleLeft("Use Dummy Data", useDummyData.boolValue);
			EditorGUI.EndDisabledGroup();
			GUILayout.Space(5f);
			readField.currentStep = EditorGUILayout.IntField("Current Step:", readField.currentStep);
			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class ReadField : ReadModuleTemplate
	{
		public string filename = string.Empty;
		public bool useDummyData;
		public bool useEmbeddedData;
		string textString = string.Empty;

		public override void InitModule()
		{
		}

		public override int BodyFunc()
		{
			int ret;

			if (useEmbeddedData)
			{
				ret = ReadDataFile();
			}
			else
			{
				if (useDummyData)
				{
					ret = SetDummyData();
				}
				else
				{
					ret = ReadDataFile();
				}
			}

			return ret;
		}

		public override void GetParameters()
		{
		}

		public void Exec()
		{
			if ((filename != "") || (useDummyData == true))
			{
				activation.SetParameterChanged(1);
			}
		}

		private IEnumerator ReadFile(string filename, Action<string> callback = null)
		{
			string url;

			if (Application.platform == RuntimePlatform.Android)
			{
				if (useEmbeddedData)
				{
					url = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
				}
				else
				{
					url = filename;
				}
				textString = FileBrowserHelpers.ReadTextFromFile(url);

//				url = filename;
//				textString = FileBrowserHelpers.ReadTextFromFile(filename);
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
//				url = "file://" + filename;
				var www = UnityWebRequest.Get(url);
				yield return www.SendWebRequest();

				if (www.isHttpError || www.isNetworkError)
				{
					Debug.Log(www.error);
				}
				else
				{
					textString = www.downloadHandler.text;
					if (callback != null)
					{
						callback(www.downloadHandler.text);
					}
				}
			}
		}

		private void ResponseCallback(string inputdata)
		{
			textString = inputdata;
		}

		IEnumerator LoadData()
		{
			yield return StartCoroutine(ReadFile(filename, ResponseCallback));

			List<int> dims = new List<int>();
			List<float>[] coords = null;
			List<float>[] values = null;

			int tmp, ndim, column;
			int vlen = 0;
			FieldType fieldType = FieldType.UNDEFINED;
			float tmpf;

			MemoryStream memoryStream = new MemoryStream();
			StreamWriter writer = new StreamWriter(memoryStream);
			writer.Write(textString);
			writer.Flush();
			memoryStream.Position = 0;

			coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}
			try
			{
				using (StreamReader streamReader = new StreamReader(memoryStream))
				{
					string line = streamReader.ReadLine();
					Debug.Log(line);
					string[] stringList = line.Split(',');
					ndim = stringList.Length;
					int size = 1;
					for (int i = 0; i < ndim; i++)
					{
						tmp = int.Parse(stringList[i]);
						dims.Add(tmp);
						size *= tmp;
					}

					for (int i = 0; i < size; i++)
					{
						line = streamReader.ReadLine();
						Debug.Log(line);
						stringList = line.Split(',');
						column = stringList.Length;
						if (i == 0)
						{
							if (column == 1)
							{
								vlen = 1;
								fieldType = FieldType.UNIFORM;
							}
							else if (column == 2)
							{
								vlen = 2;
								fieldType = FieldType.UNIFORM;
							}
							else if (column == 3)
							{
								vlen = 3;
								fieldType = FieldType.UNIFORM;
							}
							else if (column == 4)
							{
								vlen = 1;
								fieldType = FieldType.IRREGULAR;
							}
							else if (column > 4)
							{
								vlen = column - 3;
								fieldType = FieldType.IRREGULAR;
							}

							values = new List<float>[vlen];
							for (int j = 0; j < vlen; j++)
							{
								values[j] = new List<float>();
							}
						}

						if (fieldType == FieldType.UNIFORM)
						{
							for (int j = 0; j < vlen; j++)
							{
								tmpf = float.Parse(stringList[j]);
								values[j].Add(tmpf);
							}
						}
						else if (fieldType == FieldType.IRREGULAR)
						{
							for (int j = 0; j < 3; j++)
							{
								tmpf = float.Parse(stringList[j]);
								coords[3].Add(tmpf);
							}
							for (int j = 0; j < vlen; j++)
							{
								tmpf = float.Parse(stringList[j + 3]);
								values[j].Add(tmpf);
							}
						}
						else
						{
							Debug.Log(" Now this system support to type 1 or 2\n");
						}
					}
				}
			}
			catch (IOException e)
			{
				Debug.Log("Exception : " + e);
			}

			df.CreateElements(vlen);
			for (int i = 0; i < vlen; i++)
			{
				df.elements[i].SetDims(dims);
				df.elements[i].SetCoords(coords);
				df.elements[i].SetValues(values[i]);
				df.elements[i].SetFieldType(fieldType);
				df.elements[i].SetActive(true);
			}
			df.dataLoaded = true;
		}

		private int ReadDataFile()
		{
			if (filename == "") return 0;

			StartCoroutine(LoadData());
			return 1;
		}

		private int SetDummyData()
		{
			List<int> dims = new List<int>();
			List<float>[] coords;
			List<float>[] values;
			float x, y, z, p1, p2;
			int mx, my, mz;
			mx = my = mz = 11;
			int vlen = 2;

			dims.Add(mx);
			dims.Add(my);
			dims.Add(mz);

			values = new List<float>[vlen];
			for (int i = 0; i < vlen; i++)
			{
				values[i] = new List<float>();
			}
			coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
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

			df.CreateElements(vlen);
			for (int i = 0; i < vlen; i++)
			{
				df.elements[i].SetDims(dims);
				df.elements[i].SetCoords(coords);
				df.elements[i].SetValues(values[i]);
				df.elements[i].SetFieldType(FieldType.RECTILINEAR);
				df.elements[i].SetActive(true);
			}
			df.dataLoaded = true;

			return 1;
		}
	}
}