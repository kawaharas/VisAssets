using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SimpleFileBrowser;

namespace VIS
{
	public class ReadAVS : ReadModuleTemplate
	{
		public string filename = string.Empty;
		string textString  = string.Empty;
		string debugString = string.Empty;

		public override void InitModule()
		{
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

		public override void GetParameters()
		{
			base.GetParameters();
		}

		public override void ResetUI()
		{
			base.ResetUI();
		}

		IEnumerator Load()
		{
			yield return StartCoroutine(ReadFieldFile(filename, ResponseCallbackText));

			string filePath = System.IO.Path.GetDirectoryName(filename);
			MemoryStream memoryStream = new MemoryStream();
			StreamWriter writer = new StreamWriter(memoryStream);
			writer.Write(textString);
			writer.Flush();
			memoryStream.Position = 0;
			StreamReader streamReader = new StreamReader(memoryStream);

			int[] dims = new int[3];
			int nspace;
			int veclen;
			int scalars;
			int vectors;
			float scale = 0f;
			List<string> label = new List<string>();
			List<string> datafile = new List<string>();
			string[] coordfile = new string[3];

			int lineCount = 0;
			while (streamReader.Peek() != -1)
			{
				string line = streamReader.ReadLine();
				if (line[0] == '#')
				{
					debugString += "line " + lineCount.ToString() + " is Comment.\n";
				}
				string[] stringList = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
				if (stringList.Length != 0)
				{
					switch (stringList[0].ToLower())
					{
						case "ndim":
							break;
						case "dim1":
							dims[0] = Convert.ToInt32(stringList[2]);
							break;
						case "dim2":
							dims[1] = Convert.ToInt32(stringList[2]);
							break;
						case "dim3":
							dims[2] = Convert.ToInt32(stringList[2]);
							break;
						case "nspace":
							nspace = Convert.ToInt32(stringList[2]);
							break;
						case "veclen":
							veclen = Convert.ToInt32(stringList[2]);
							break;
						case "data":
							// short byte integer float double
							switch (stringList[2].ToLower())
							{
								case "short":
									break;
								case "byte":
									break;
								case "integer":
									break;
								case "float":
									break;
								case "double":
									break;
								default:
									break;
							}
							break;
						case "field":
							// uniform rectilinear irregular
							switch (stringList[2].ToLower())
							{
								case "uniform":
									break;
								case "rectilinear":
									break;
								case "irregular":
									break;
								default:
									break;
							}
							break;
						case "label":
							for (int i = 2; i < stringList.Length; i++)
							{
								label.Add(stringList[2]);
							}
							break;
						case "variable":
							break;
						default:
							break;
					}
				}

				lineCount++;
			}
			streamReader.Close();

			Debug.Log(debugString);
		}

		private IEnumerator ReadFieldFile(string filename, Action<string> callback = null)
		{
			string url;

			if (Application.platform == RuntimePlatform.Android)
			{
				url = filename;
				textString = FileBrowserHelpers.ReadTextFromFile(filename);
			}
			else
			{
				url = "file://" + filename;
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

		private void ResponseCallbackText(string data)
		{
			textString = data;
		}
	}
}