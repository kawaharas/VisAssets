using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CustomEditor(typeof(UIManager))]
	public class UIManagerEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var uiManager = target as UIManager;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.Space();

			var label = new GUIContent("XR API");
			GUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.Width(80f));
			var selectedDeviceID = EditorGUILayout.Popup("", uiManager.selectedDeviceID, uiManager.supportedDevices);
			GUILayout.EndHorizontal();
			selectedDeviceID = Mathf.Clamp(selectedDeviceID, 0, uiManager.supportedDevices.Length);

			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck())
			{
				uiManager.SetXRDevice(selectedDeviceID);
			}
			serializedObject.ApplyModifiedProperties();

			base.OnInspectorGUI();
		}
	}
#endif

	public class UIManager : MonoBehaviour
	{
		GameObject[] go;
		public int[] moduleCounter;
		int moduleNum;
		public string debugString = "";
		public GameObject cardboardButton;

		[ReadOnly]
		public string[] supportedDevices;
		[ReadOnly]
		public int selectedDeviceID = 0;
		[ReadOnly]
		public bool XRState;

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
//			XRSettings.enabled = false;
			XRState = XRSettings.enabled;
/*
			if (XRSettings.enabled)
			{
				var canvas = this.transform.Find("Canvas");
				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				rectTransform.localPosition = new Vector3(10f, 5f, 0f);
				rectTransform.sizeDelta = new Vector2(100, 100);
				rectTransform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
			}
*/
			ShowUIManager();

			supportedDevices = XRSettings.supportedDevices;
			Debug.Log("XRSettings.supportedDevices = " + supportedDevices.Length);
			for (int i = 0; i < supportedDevices.Length; i++)
			{
				Debug.Log("XRSettings.supportedDevices[" + i + "] = " + supportedDevices[i]);
			}
		}

		void Update()
		{
			if (XRSettings.enabled)
			{
				Vector3 headPosition;
				Quaternion headRotation;
				List<XRNodeState> nodeStates = new List<XRNodeState>();
				InputTracking.GetNodeStates(nodeStates);
				var headState = nodeStates.FirstOrDefault(node => node.nodeType == XRNode.Head);
				headState.TryGetPosition(out headPosition);
				headState.TryGetRotation(out headRotation);

				var canvas = this.transform.Find("Canvas");
				var rectTransform = canvas.GetComponent<RectTransform>();
				var tmp = Quaternion.AngleAxis(headRotation.eulerAngles.y, -transform.forward);
				rectTransform.localPosition = headPosition + headRotation * new Vector3(0f, 0f, 10f);
				rectTransform.localRotation = headRotation;
			}


			var inputDevices = new List<UnityEngine.XR.InputDevice>();
//			InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices);
			UnityEngine.XR.InputDevices.GetDevices(inputDevices);
//			Debug.Log("inputDevices.Count = " + inputDevices.Count);
			foreach (var device in inputDevices)
			{
				var name = device.name;
				var roll = device.characteristics.ToString();
				Debug.Log(string.Format("name: '{0}', role: '{1}'", name, roll));
			}
			if (Application.platform == RuntimePlatform.Android)
			{
			}
			else
			{
				if (Input.GetKeyDown(KeyCode.F3))
				{
					if (XRSettings.enabled)
					{
						Debug.Log("Switch XR Mode to None.");
						StartCoroutine(LoadDevice("None"));
					}
					else
					{
						StartCoroutine(LoadDevice("Oculus"));
					}
					/*
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
					*/
				}
			}
		}

		IEnumerator LoadDevice(string device)
		{
			if (String.Compare(XRSettings.loadedDeviceName, device, true) != 0)
			{
				XRSettings.LoadDeviceByName(device);
				yield return null;

				if (device == "None")
				{
					XRSettings.enabled = false;
					XRState = XRSettings.enabled;
				}
				else
				{
					if (XRSettings.loadedDeviceName != "None")
					{
						XRSettings.enabled = true;
						XRState = XRSettings.enabled;
					}
				}

				var canvas = this.transform.Find("Canvas");
				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				rectTransform.localPosition = new Vector3(10f, 5f, 0f);
				rectTransform.sizeDelta = new Vector2(100, 100);
				rectTransform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
			}
		}

		public void SetXRDevice(int deviceID)
		{
			var deviceName = supportedDevices[deviceID];
			StartCoroutine(LoadDevice(deviceName));
			selectedDeviceID = deviceID;
		}

		public void EnableXR()
		{
			XRSettings.enabled = true;
			XRState = XRSettings.enabled;
		}

		public void DisableXR()
		{
			XRSettings.enabled = false;
			XRState = XRSettings.enabled;
		}

		public void ShowUIManager()
		{
			if (XRSettings.enabled)
			{
				Vector3 headPosition;
				Quaternion headRotation;
				List<XRNodeState> nodeStates = new List<XRNodeState>();
				InputTracking.GetNodeStates(nodeStates);
				var headState = nodeStates.FirstOrDefault(node => node.nodeType == XRNode.Head);
				headState.TryGetPosition(out headPosition);
				headState.TryGetRotation(out headRotation);

				var canvas = this.transform.Find("Canvas");
				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				rectTransform.localPosition = new Vector3(10f, 5f, 0f);
				rectTransform.sizeDelta = new Vector2(100, 100);
				rectTransform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
			}
		}
	}
}