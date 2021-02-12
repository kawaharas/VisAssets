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
		public enum ButtonState
		{
			RELEASED,
			PRESSED,
			KEEP_PRESSING
		}

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
		public bool inputValue;
		public ButtonState ButtonA = ButtonState.RELEASED;

		public GameObject moduleSelector;
		public GameObject paramChanger;

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

			if (XRSettings.enabled)
			{
				var canvas = this.transform.Find("Canvas");
				canvas.gameObject.SetActive(false);
			}

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
				var inputDevices = new List<UnityEngine.XR.InputDevice>();
				UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, inputDevices);
				foreach (var device in inputDevices)
				{
					// joystick
					if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 position))
					{
						Debug.Log("Joystick : " + position.x + "," + position.y);

						GameObject[] gos = GameObject.FindGameObjectsWithTag("VisModule");
						for (int i = 0; i < gos.Length; i++)
						{
							var activation = gos[i].GetComponent<Activation>();
							if (activation != null)
							{
								if (activation.moduleType == ModuleTemplate.ModuleType.READING)
								{
									float rotX = 0;
									float rotY = 0;
									if (Mathf.Abs(position.x) >= 0.5)
									{
										rotX = position.x;
									}
									if (Mathf.Abs(position.y) >= 0.5)
									{
										rotY = position.y;
									}
									gos[i].transform.Rotate(new Vector3(rotY, -rotX, 0), Space.World);
								}
							}
						}
					}

					// button
					if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out inputValue) && inputValue)
					{
						if (ButtonA == ButtonState.RELEASED)
						{
							ShowUIManager();
							ButtonA = ButtonState.PRESSED;

						}
						else if (ButtonA == ButtonState.PRESSED)
						{
							ButtonA = ButtonState.KEEP_PRESSING;
						}
						Debug.Log("Trigger button is pressed.");
					}
					else
					{
						var canvas = this.transform.Find("Canvas");
						canvas.gameObject.SetActive(false);
						ButtonA = ButtonState.RELEASED;
					}
				}
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
				var canvas = this.transform.Find("Canvas");
				canvas.gameObject.SetActive(true);
				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				var camera = Camera.main;
				var position = camera.transform.position;
				var rotation = camera.transform.rotation.eulerAngles;
				var direction = Quaternion.AngleAxis(rotation.y, Vector3.up);
				rectTransform.localPosition = position + direction * new Vector3(0f, 0f, 10f);
				rectTransform.localRotation = direction;
				rectTransform.sizeDelta = new Vector2(100, 100);
				rectTransform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
			}
		}
	}
}