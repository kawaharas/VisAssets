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
	public class ReadV5 : ReadModuleTemplate
	{
		public enum PRECISION
		{
			SINGLE,
			DOUBLE
		};

		[SerializeField]
		public PRECISION precision = PRECISION.SINGLE;
		[SerializeField]
		public bool byteswap = false;
		[SerializeField]
		public bool header = true; // true: with header, false: without header
		[SerializeField]
		public float[] offsets;
		float scale;
		float[] axis_width;
		float minx, maxx;
		float miny, maxy;
		float minz, maxz;

		string debugURL;

		public string filename = "C:/Temp/sample_little/dynamo.v5";
		public string v5file   = string.Empty;
		byte[] bytedata;
		string datafile;

		public override void InitModule()
		{
			offsets = new float[3];
			axis_width  = new float[3];

			// upper direction is defined as z-axis in VFIVE.
//			transform.rotation = Quaternion.AngleAxis(-90, new Vector3(1, 0, 0));
		}

		public override int BodyFunc()
		{
//			int ret = Read();
//			return ret;
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

//			if (filename == "") return 0;
/*
			WWW www = new WWW("file://" + filename);
			yield return www;
			string filePath = System.IO.Path.GetDirectoryName(filename);
			MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(www.text));
			StreamReader streamReader = new StreamReader(memoryStream, Encoding.GetEncoding("Shift_JIS"));
*/
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
						//							string indexString = keyword.Replace("SCAL", "").Replace("_LABEL", "");
						int index = Convert.ToInt32(indexString);
						if (keyword.Contains("_LABEL"))
						{
							label.Add(stringList[1]);
						}
						else
						{
							datafile.Add((filePath + '/' + stringList[1]).Replace('\\', '/'));
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
						else
						{
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
								coordfile[0] = (filePath + '/' + stringList[1]).Replace('\\', '/');
							break;
							case "YFILE":
								coordfile[1] = (filePath + '/' + stringList[1]).Replace('\\', '/');
								break;
							case "ZFILE":
								coordfile[2] = (filePath + '/' + stringList[1]).Replace('\\', '/');
								break;
							default:
								break;
						}
					}
				}
			}
			streamReader.Close();

			string debugString = "";
			debugString += "N1: " + dims[0] + '\n';
			debugString += "N2: " + dims[1] + '\n';
			debugString += "N3: " + dims[2] + '\n';
			debugString += "XFILE: " + coordfile[0] + '\n';
			debugString += "YFILE: " + coordfile[1] + '\n';
			debugString += "ZFILE: " + coordfile[2] + '\n';
			debugString += "LABEL: " + label.Count + '\n';
//			Debug.Log(debugString);

			List<float>[] coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
			List<Vector3> coords3d = new List<Vector3>();
			for (int i = 0; i < 4; i++)
			{
				coords[i] = new List<float>();
			}
			for (int i = 0; i < 3; i++)
			{
				yield return StartCoroutine(ReadBinData2(coordfile[i], coords[i], dims[i], header));
				/*
								StartCoroutine(ReadBinary(coordfile[i], ResponseCallbackBinary));
								yield return StartCoroutine(ReadBinary(coordfile[i]));
								try
								{
				//					using (MemoryStream memoryStream = new MemoryStream(bytedata))
									using (MemoryStream ms = new MemoryStream(bytedata))
									using (BinaryReader reader = new BinaryReader(ms))
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
										var buffer = new byte[dims[i] * dataLength];
										var length = reader.Read(buffer, 0, dims[i] * dataLength);
										if (length != dims[i] * dataLength)
										{
											Debug.Log("ERROR: Failed to load data.");
										}
										for (int n = 0; n < dims[i]; n++)
										{
											if (byteswap)
											{
												Array.Reverse(buffer, n * dataLength, dataLength);
											}
											float value = 0f;
											if (precision == PRECISION.SINGLE)
											{
												value = BitConverter.ToSingle(buffer, n * dataLength);
											}
											else
											{
												value = (float)BitConverter.ToDouble(buffer, n * dataLength);
											}
											coords[i].Add(value);
										}

										reader.Close();
									}
								}
								catch (IOException exception)
								{
									Debug.Log("Exception : " + exception.Message);
								}
				*/
				/*

				//				ReadData(coordfile[i], coords[i], dims[i], header);
								yield return StartCoroutine(ReadDataWWW(coordfile[i], coords[i], dims[i], header));
								Debug.Log("coordfile[" + i + "] = " + coordfile[i]);
				//				ReadDataWWW(coordfile[i], coords[i], dims[i], header);
				*/
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
			df.CreateElements(datafile.Count);
			for (int i = 0; i < datafile.Count; i++)
			{
				df.elements[i].SetDims(dims[0], dims[1], dims[2]);

				List<float> values = new List<float>();
/*
//				ReadData(datafile[i], values, dims[0] * dims[1] * dims[2], header);
//				StartCoroutine(ReadDataWWW(datafile[i], values, dims[0] * dims[1] * dims[2], header));
				yield return StartCoroutine(ReadDataWWW(datafile[i], values, dims[0] * dims[1] * dims[2], header));
				Debug.Log("datafile[" + i + "] = " + datafile[i]);
				//				ReadDataWWW(datafile[i], values, dims[0] * dims[1] * dims[2], header);
*/
				yield return StartCoroutine(ReadBinData2(datafile[i], values, dims[0] * dims[1] * dims[2], header));

				df.elements[i].SetCoords(coords);
				df.elements[i].SetValues(values);
				df.elements[i].SetFieldType(DataElement.FieldType.RECTILINEAR);
				df.elements[i].varName = label[i].Replace("\\n", " ");
				df.elements[i].SetActive(true);
			}

			// calculate offsets and the normalized scale
			float max = float.MinValue;
			for (int i = 0; i < 3; i++)
			{
				axis_width[i] = coords[i][dims[i] - 1] - coords[i][0];
				if (max < axis_width[i])
				{
					max = axis_width[i];
				}
				offsets[i] = coords[i][0] + (coords[i][dims[i] - 1] - coords[i][0]) / 2f;
			}
			minx = coords[0][0];
			miny = coords[1][0];
			minz = coords[2][0];
			maxx = coords[0][dims[0] - 1];
			maxy = coords[1][dims[1] - 1];
			maxz = coords[2][dims[2] - 1];
			if (scale == 0)
			{
				scale = 1f / max * 6f;
			}
			foreach (Transform child in transform)
			{
				child.gameObject.transform.localPosition = new Vector3(-offsets[0], -offsets[1], offsets[2]);
			}
			transform.localScale = new Vector3(scale, scale, -scale);
			df.dataLoaded = true;
		}
/*
		public int Read()
		{
			if (filename == "") return 0;

			StartCoroutine(Load());
			return 1;
		}
*/
		private IEnumerator ReadV5File(string filename, Action<string> callback = null)
		{
			string url;

			if (Application.platform == RuntimePlatform.Android)
			{
				url = filename;
				v5file = FileBrowserHelpers.ReadTextFromFile(filename);
			}
			else
			{
				url = "file://" + filename;
				UnityWebRequest www = UnityWebRequest.Get(url);
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

		private void ResponseCallbackText(string inputdata)
		{
			v5file = inputdata;
		}

		private void ResponseCallbackBinary(byte[] inputdata)
		{
			bytedata = inputdata;
		}

		private IEnumerator ReadBinary(string filename, Action<byte[]> callback = null)
		{
			if (Application.platform == RuntimePlatform.Android)
			{
				bytedata = FileBrowserHelpers.ReadBytesFromFile(filename);
			}
			else
			{
				string url = "file://" + filename;
				UnityWebRequest www = new UnityWebRequest(url);
				www.downloadHandler = new DownloadHandlerBuffer();
				yield return www.SendWebRequest();

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

/*
//		public void ReadDataWWW(string filename, List<float> v, int size, bool header = true, bool byteswap = false)
		IEnumerator ReadDataWWW(string filename, List<float> v, int size, bool header = true, bool byteswap = false)
		{
			string url;

			url = "file://" + filename;
			WWW www = new WWW(url);
//			WWW www = new WWW("file://" + filename);
//			yield return www;
			MemoryStream memoryStream = new MemoryStream(www.bytes);

//			var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
//			var reader = new BinaryReader(stream);
			var reader = new BinaryReader(memoryStream);
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
					if (byteswap)
					{
						Array.Reverse(buffer, n * dataLength, dataLength);
					}
					float value = 0f;
					if (precision == PRECISION.SINGLE)
					{
						value = BitConverter.ToSingle(buffer, n * dataLength);
					}
					else
					{
						value = (float)BitConverter.ToDouble(buffer, n * dataLength);
					}
					v.Add(value);
				}

				reader.Close();
			}
			yield return null;
		}
*/

		private void ReadData(string filename, List<float> v, int size, bool header = true, bool byteswap = false)
		{
			if (File.Exists(filename))
			{
				var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
				var reader = new BinaryReader(stream);
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
						if (byteswap)
						{
							Array.Reverse(buffer, n * dataLength, dataLength);
						}
						float value = 0f;
						if (precision == PRECISION.SINGLE)
						{
							value = BitConverter.ToSingle(buffer, n * dataLength);
						}
						else
						{
							value = (float)BitConverter.ToDouble(buffer, n * dataLength);
						}
						v.Add(value);
					}

					reader.Close();
				}
			}
		}

		private IEnumerator ReadBinData2(string filename, List<float> v, int size, bool header = true, bool byteswap = false)
		{
			StartCoroutine(ReadBinary(filename, ResponseCallbackBinary));
			yield return StartCoroutine(ReadBinary(filename));

			var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			var reader = new BinaryReader(stream);
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
					if (byteswap)
					{
						Array.Reverse(buffer, n * dataLength, dataLength);
					}
					float value = 0f;
					if (precision == PRECISION.SINGLE)
					{
						value = BitConverter.ToSingle(buffer, n * dataLength);
					}
					else
					{
						value = (float)BitConverter.ToDouble(buffer, n * dataLength);
					}
					v.Add(value);
				}

				reader.Close();
			}
		}
	}
}
