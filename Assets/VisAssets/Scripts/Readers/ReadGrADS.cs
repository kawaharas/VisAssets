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

		string debugURL;

		public string filename = string.Empty;
		public string ctlfile  = string.Empty;
		byte[] bytedata;

		int[]  dims;
		int    varnum;
		string datafile;
		List<VarInfo> varInfo;
		float  undef;
		bool   useUndef;
		bool[] options;
		int header_size;

		List<float>[] coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)

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
//			filename = "C:/Users/kawahara/Downloads/example/model.ctl";
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

//		private IEnumerator ReadCtlFile(string filename)
		private IEnumerator ReadCtlFile(string filename, Action<string> callback = null)
		{
			string url;

			if (Application.platform == RuntimePlatform.Android)
			{
				url = filename;
//				public static byte[] FileBrowserHelpers.ReadBytesFromFile(string sourcePath);
				ctlfile = FileBrowserHelpers.ReadTextFromFile(filename);
/*
				// Android
				string asset_name = System.IO.Path.GetFileName(filename);
				string bundleUrl = Path.Combine(Application.streamingAssetsPath, asset_name);

				UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(bundleUrl);
				yield return request.SendWebRequest();

				AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(request);
				assetBundle.Unload(false);
*/
			}
			else
			{
//				string url = "file://" + filename;
				url = "file://" + filename;
				UnityWebRequest www = UnityWebRequest.Get(url);
				yield return www.SendWebRequest();

				if (www.isHttpError || www.isNetworkError)
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
			if (Application.platform == RuntimePlatform.Android)
			{
				bytedata = FileBrowserHelpers.ReadBytesFromFile(filename);
			}
			else
			{
				string url = "file://" + filename;
				debugURL = url;

//				UnityWebRequest www = UnityWebRequest.Get(url);
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
			for (int z = 0; z < dims[2]; z++)
			{
				for (int y = 0; y < dims[1]; y++)
				{
					for (int x = 0; x < dims[0]; x++)
					{
						coords[3].Add(coords[0][x]);
						coords[3].Add(coords[1][y]);
						coords[3].Add(coords[2][z]);
					}
				}
			}

			df.CreateElements(varnum);

			SetParamsToDataElements();

			StartCoroutine(ReadBinary(datafile, ResponseCallbackBinary));
			yield return StartCoroutine(ReadBinary(datafile));

			int step = 1;
			SetData(step);
/*
			memoryStream = new MemoryStream(bytedata);

			try
			{
				using (BinaryReader reader = new BinaryReader(memoryStream))
				{
					// skip a header record of "header_size" bytes
//					if (header_size != 0)
//					{
//						var header = new byte[header_size];
//						reader.Read(header, 0, header_size);
//					}
					if ((header_size != 0) && (reader.BaseStream.CanSeek))
					{
						reader.BaseStream.Position = header_size;
					}

					for (int i = 0; i < varnum; i++)
					{
						var values = new List<float>();
						var size = df.elements[i].dims[0] * df.elements[i].dims[1] * df.elements[i].dims[2];
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
								Array.Reverse(buffer, n * datasize, datasize);
							}
							var value = BitConverter.ToSingle(buffer, n * sizeof(float));
							values.Add(value);
						}
						df.elements[i].SetValues(values);






						if (useUndef)
						{
							df.elements[i].SetUndef(undef);
						}

						if (varInfo[i].levs == 0)
						{
							df.elements[i].SetDims(dims[0], dims[1], 1);
							List<float>[] coords_tmp = new List<float>[4];
							coords_tmp[0] = new List<float>(coords[0]);
							coords_tmp[1] = new List<float>(coords[1]);
							coords_tmp[2] = new List<float>();
							coords_tmp[2].Add(coords[2][0]);
							coords_tmp[3] = new List<float>();
							// merge coordinates to 4th list
							for (int z = 0; z < 1; z++)
							{
								for (int y = 0; y < dims[1]; y++)
								{
									for (int x = 0; x < dims[0]; x++)
									{
										coords_tmp[3].Add(coords_tmp[0][x]);
										coords_tmp[3].Add(coords_tmp[1][y]);
										coords_tmp[3].Add(coords_tmp[2][z]);
									}
								}
							}
							df.elements[i].SetCoords(coords_tmp);

							var values = new List<float>();
							var size = dims[0] * dims[1] * 1;
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
									Array.Reverse(buffer, n * datasize, datasize);
								}
								var value = BitConverter.ToSingle(buffer, n * sizeof(float));
								values.Add(value);
							}
							df.elements[i].SetValues(values);
						}
						else if (varInfo[i].levs == dims[2])
						{
							df.elements[i].SetDims(dims[0], dims[1], dims[2]);
							df.elements[i].SetCoords(coords);

							var values = new List<float>();
							var size = dims[0] * dims[1] * dims[2];
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
									Array.Reverse(buffer, n * datasize, datasize);
								}
								var value = BitConverter.ToSingle(buffer, n * sizeof(float));
								values.Add(value);
							}
							df.elements[i].SetValues(values);
						}
						else
						{
							df.elements[i].SetDims(dims[0], dims[1], varInfo[i].levs);
							List<float>[] coords_tmp = new List<float>[4];
							coords_tmp[0] = new List<float>(coords[0]);
							coords_tmp[1] = new List<float>(coords[1]);
							coords_tmp[2] = new List<float>();
							for (int n = 0; n < varInfo[i].levs; n++)
							{
								coords_tmp[2].Add(coords[2][n]);
							}
							coords_tmp[3] = new List<float>();
							// merge coordinates to 4th list
							for (int z = 0; z < varInfo[i].levs; z++)
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
							df.elements[i].SetCoords(coords_tmp);

							var values = new List<float>();
							var size = dims[0] * dims[1] * varInfo[i].levs;
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
									Array.Reverse(buffer, n * datasize, datasize);
								}
								var value = BitConverter.ToSingle(buffer, n * sizeof(float));
								values.Add(value);
							}
							df.elements[i].SetValues(values);
						}
						df.elements[i].SetFieldType(DataElement.FieldType.RECTILINEAR);
						df.elements[i].varName = varInfo[i].description;
						df.elements[i].SetActive(true);

					}
				}
			}
			catch (IOException exception)
			{
				Debug.Log("Exception : " + exception.Message);
			}
			memoryStream.Close();
*/
			CalcOffsets();

			// turn on flag when data loading is complete
			df.dataLoaded = true;

			yield return null;
		}

		private void SetParamsToDataElements()
		{
			for (int i = 0; i < varnum; i++)
			{
				if (varInfo[i].levs == dims[2])
				{
					df.elements[i].SetDims(dims[0], dims[1], dims[2]);
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
//				child.gameObject.transform.localScale = new Vector3(ScaleX, ScaleY, ScaleZ);
			}
//			transform.localScale = new Vector3(scale, scale, -scale);
//			transform.localScale = new Vector3(ScaleX, ScaleY, ScaleZ * 20f);
			transform.localScale = new Vector3(ScaleX, ScaleY, -ScaleZ * 10000f); // if z-axis is pressure coordinates
		}

		public override void SetData(int step)
		{
			try
			{
				using (MemoryStream memoryStream = new MemoryStream(bytedata))
				using (BinaryReader reader = new BinaryReader(memoryStream))
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


				// test
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
		void OnGUI()
		{
			string debugString = "";
			debugString += ctlfile + '\n';
			debugString += debugURL + '\n';
//			if (df.elements != null)
			if (df.dataLoaded)
			{
					for (int i = 0; i < df.elements.Length; i++)
				{
					debugString += df.elements[i].varName + ", ";
					debugString += "min: " + df.elements[i].min + ", ";
					debugString += "max: " + df.elements[i].max + '\n';
				}
			}
			GUI.TextArea(new Rect(0, 0, 500, 500), debugString);
		}
*/
	}
}