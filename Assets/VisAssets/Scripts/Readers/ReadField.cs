using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text;

namespace VIS
{
	using FieldType = DataElement.FieldType;

	public class ReadField : ReadModuleTemplate
	{
		public string filename = "C:/Temp/Sample3D3.txt";
		public bool dummy_data = false;
		string textString = string.Empty;

		public override void InitModule()
		{
		}

		public override int BodyFunc()
		{
			int ret;

			if (dummy_data)
			{
				ret = SetDummyData();
			}
			else
			{
				ret = ReadDataFile();
			}

			return ret;
		}

		public override void GetParameters()
		{
		}

		public void Exec()
		{
			if ((filename != "") || (dummy_data == true))
			{
				activation.SetParameterChanged(1);
			}
		}

//		private IEnumerator ReadFile(string filename)
		private IEnumerator ReadFile(string filename, Action<string> callback = null)
		{
			string url = "file://" + filename;
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

		private void ResponseCallback(string data)
		{
			textString = data;
		}

		IEnumerator LoadData()
		{
			yield return StartCoroutine(ReadFile(filename, ResponseCallback));

			List<int> dims = new List<int>();
//			List<float>[] coords = null;
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

			//			Debug.Log("init loaddata()");
			//			var www = new WWW("file://" + filename);
			//			yield return www;
			//			MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(www.text));

			/*
						StreamReader rs = new StreamReader(ms);
						while (rs.Peek() > -1)
						{
							Debug.Log(rs.ReadLine());
						}
			*/
			List<float>[] coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
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
//								coords[j].Add(tmpf);
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
					} // for loop 
				} // using
			}
			catch (IOException e)
			{
				//	Debug.Log("Exception : ");
//				return (0);
			}
/*
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
*/
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
			List<int> dims = new List<int>();
			List<float>[] coords = null;
			List<float>[] values = null;

			int tmp, ndim, column;
			int vlen = 0;
			FieldType fieldType = FieldType.UNDEFINED;
			float tmpf;

			if (filename == "") return 0;

			StartCoroutine(LoadData());
			return 1;

			if (File.Exists(filename))
			{
				Debug.Log(" In ReadDataFile func : filename = " + filename);
				FileInfo fileInfo = new FileInfo(filename);
				coords = new List<float>[4]; // 0:x, 1:y, 2:z, 3:(x, y, z)
				for (int i = 0; i < 4; i++)
				{
					coords[i] = new List<float>();
				}
				try
				{
					using (StreamReader streamReader = new StreamReader(fileInfo.OpenRead(), Encoding.UTF8))
					{
						string line = streamReader.ReadLine();
						Debug.Log(line);
						string[] stringList = line.Split(',');
//						string[] stringList = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
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
									fieldType = FieldType.IRREGULAR;				}

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
//									coords[j].Add(tmpf);
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
						} // for loop
						streamReader.Close();
					} // using
				}
				catch (IOException e)
				{
				//	Debug.Log("Exception : ");
					return (0);
				}
/*
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
*/
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
				return 1;
			}
			return 0;
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