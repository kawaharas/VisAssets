using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VIS
{
	public class UIManager : MonoBehaviour
	{
		GameObject[] go;
		public int[] moduleCounter;
		int moduleNum;
		public string debugString = "";
		public GameObject cardboardButton;

		enum ModuleName
		{
			Arrows = 0,
			Animator,
			ReadField,
			ReadGrADS,
			ReadV5,
			ExtractScalar,
			ExtractVector,
			Interpolator,
			Slicer,
			Isosurface,
			Bounds
		}

		void Awake()
		{
			moduleNum = Enum.GetNames(typeof(ModuleName)).Length;
			moduleCounter = new int[moduleNum];
		}

		void Start()
		{
			if (Application.platform != RuntimePlatform.Android)
			{
				if (cardboardButton != null)
				{
					cardboardButton.SetActive(false);
				}
			}
			XRSettings.enabled = false;
		}

		void Update()
		{
			if (Application.platform == RuntimePlatform.Android)
			{
			}
			else
			{
				if (Input.GetKeyDown(KeyCode.F3))
				{
					Debug.Log("XRDevice.model = " + XRDevice.model); // 2018.4ではQuest接続時の戻り値が空
					Debug.Log("XRSettings.loadedDeviceName = " + XRSettings.loadedDeviceName);
					//					Debug.Log("InputDevice.name = " + InputDevice.name); // 2020.1ではOculus Linkの検出に使える?
					var joysticks = Input.GetJoystickNames();
					for (int i = 0; i < joysticks.Length; i++)
					{
						Debug.Log("Input.GetJoystickNames() = " + joysticks[i]);
					}
					StartCoroutine(LoadDevice("OpenVR"));
					if (XRDevice.isPresent)
					{
						Debug.Log("XRDevice.model = " + XRDevice.model);
						Debug.Log("XRSettings.loadedDeviceName = " + XRSettings.loadedDeviceName);
						if (XRDevice.model.StartsWith("Oculus"))
						{
							StartCoroutine(LoadDevice("Oculus"));
						}
						else
						{
							StartCoroutine(LoadDevice("OpenVR"));
						}
					}
				}
			}
		}

		IEnumerator LoadDevice(string newDevice)
		{
			if (String.Compare(XRSettings.loadedDeviceName, newDevice, true) != 0)
			{
				XRSettings.LoadDeviceByName(newDevice);
				yield return null;
				XRSettings.enabled = true;
/*
				var canvas = this.transform.Find("Canvas");
				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				rectTransform.localPosition = new Vector3(10f, 5f, 0f);
				rectTransform.sizeDelta = new Vector2(100, 100);
				rectTransform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
*/
			}
		}

		public void EnableXR()
		{
			XRSettings.enabled = true;
		}

		public void DisableXR()
		{
			XRSettings.enabled = false;
		}
	}
}