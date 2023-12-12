using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
//using UnityEngine.InputSystem;
//using Unity.XR.CoreUtils;
//using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.Experimental.XR.Interaction;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.Interaction.Toolkit;
using VisAssets.SciVis.Structured.StreamLines;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets
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
//	public class UIManager : XRBaseController
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
		public ButtonState ButtonTrigger = ButtonState.RELEASED;
		RaycastHit hitInfo;

		Vector3 initialCameraPosition;
		public GameObject moduleSelector;
		public GameObject paramChanger;
		public GameObject laserPointer;
		public Vector3    tip;

		public Vector3 origin;
		public Vector3 originController;

		public GameObject currentModule;

		void Awake()
		{
			currentModule = null;
			moduleNum = Enum.GetNames(typeof(ModuleName)).Length;
			moduleCounter = new int[moduleNum];
			hitInfo = new RaycastHit();
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
//			XRState = XRSettings.enabled;

			if (XRSettings.enabled)
			{
				var canvas = this.transform.Find("Canvas");
				canvas.gameObject.SetActive(false);

				var mainCamera = Camera.main;
				var obj = new GameObject("UI Camera");
				obj.AddComponent<Camera>();
				obj.transform.parent = mainCamera.transform;
				var uiCamera = obj.GetComponent<Camera>();
				uiCamera.clearFlags = CameraClearFlags.Depth;
				uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
				uiCamera.transform.localPosition = Vector3.zero;
				uiCamera.depth = mainCamera.GetComponent<Camera>().depth + 1;
				mainCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));

				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				canvas.GetComponent<Canvas>().worldCamera = uiCamera;

				SetupPointer();
			}
/*
			supportedDevices = XRSettings.supportedDevices;
			Debug.Log("XRSettings.supportedDevices = " + supportedDevices.Length);
			for (int i = 0; i < supportedDevices.Length; i++)
			{
				Debug.Log("XRSettings.supportedDevices[" + i + "] = " + supportedDevices[i]);
			}
*/
		}

		void Update()
		{
			if (XRSettings.enabled)
			{
				var inputDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);
				foreach (var device in inputDevices)
				{
/*
					// joystick
					if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 position))
					{
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
*/
					// primary button (show VRUI)
					if (device.TryGetFeatureValue(CommonUsages.primaryButton, out inputValue) && inputValue)
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
					}
					else
					{
						var canvas = this.transform.Find("Canvas");
						canvas.gameObject.SetActive(false);
						ButtonA = ButtonState.RELEASED;
					}

					// trigger button (operate VRUI)
					var _canvas = this.transform.Find("Canvas");
					if (!_canvas.gameObject.activeSelf)
					{
						if (device.TryGetFeatureValue(CommonUsages.triggerButton, out inputValue) && inputValue)
						{
							if (ButtonTrigger == ButtonState.RELEASED)
							{
/*
//								Debug.Log("AddSeed");
								var gos = GameObject.FindGameObjectsWithTag("VisModule");
								foreach (var go in gos)
								{
									if (go.name.StartsWith("StreamLines"))
									{
										var streamLines = go.GetComponent<StreamLines>();
										if (streamLines != null)
										{
											if (tip != null)
											{
//												var _tip = Vector3(4.95049524,4.95049524,-4.95049524)
												var _tip = new Vector3(tip[0] / 4.95049524f, tip[2] / -4.95049524f, tip[1] / -4.95049524f);
												streamLines.AddSeed(_tip);
											}
										}
									}
								}
*/
								if (currentModule.name.StartsWith("StreamLines"))
								{
									var streamLines = currentModule.GetComponent<StreamLines>();
									if (streamLines != null)
									{
										if (tip != null)
										{
//											var _tip = Vector3(4.95049524,4.95049524,-4.95049524)
//											var _tip = new Vector3(tip[0] / 4.95049524f, tip[2] / -4.95049524f, tip[1] / 4.95049524f);
//											streamLines.AddSeed(_tip);
											streamLines.AddSeed2(tip);
										}
									}
								}

//								var position = tip;
								laserPointer.SetActive(true);
								ButtonTrigger = ButtonState.PRESSED;
							}
							else if (ButtonA == ButtonState.PRESSED)
							{
								ButtonTrigger = ButtonState.KEEP_PRESSING;
							}
						}
						else
						{
							laserPointer.SetActive(false);
							ButtonTrigger = ButtonState.RELEASED;
						}
					}
				}

				if (ButtonA == ButtonState.PRESSED)
				{
					laserPointer.SetActive(true);
				}
				else if (ButtonA == ButtonState.RELEASED)
				{
					laserPointer.SetActive(false);
				}

				// for streamline module
				var __canvas = transform.Find("Canvas");
				if (!__canvas.gameObject.activeSelf)
				{
					if (currentModule != null)
					{
						if (currentModule.name.StartsWith("StreamLines"))
						{
							if (ButtonTrigger == ButtonState.PRESSED)
							{
								laserPointer.SetActive(true);
							}
							else if (ButtonTrigger == ButtonState.RELEASED)
							{
								laserPointer.SetActive(false);
							}
						}
					}
				}

				DrawPointer();
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
//				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				var camera = Camera.main;
				var rotation = camera.transform.rotation.eulerAngles;
				var direction = Quaternion.AngleAxis(rotation.y, Vector3.up);

				initialCameraPosition = Camera.main.transform.position;

				rectTransform.localPosition = initialCameraPosition + direction * new Vector3(0f, 0f, 1f);
//				rectTransform.localPosition = initialCameraPosition + direction * new Vector3(0f, 0f, 20f);
				rectTransform.localRotation = direction;
				rectTransform.sizeDelta  = new Vector2(100, 100);
//				rectTransform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
				rectTransform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);
			}
		}

		void SetupPointer()
		{
			if (laserPointer != null)
			{
				laserPointer.SetActive(false);
				var renderer = laserPointer.GetComponent<LineRenderer>();
				var material = renderer.material;
				material.SetOverrideTag("RenderType", "Transparent");
				material.SetColor("_Color", Color.red);
				material.SetFloat("_Alpha", 200f);
				material.SetFloat("_Emmision", 0.5f);
			}
		}

		void DrawPointer()
		{
			if (laserPointer != null)
			{
				Vector3 centerEyePosition = new Vector3();
				var centerEyeDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, centerEyeDevices);
				foreach (var device in centerEyeDevices)
				{
					device.TryGetFeatureValue(CommonUsages.devicePosition, out centerEyePosition);
				}

				var inputDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);
				foreach (var device in inputDevices)
				{
/*
//					Vector3 origin;
					Quaternion quaternion;
					device.TryGetFeatureValue(CommonUsages.devicePosition, out originController);
					device.TryGetFeatureValue(CommonUsages.deviceRotation, out quaternion);

					origin = originController - centerEyePosition + Camera.main.transform.position;
*/
					Vector3 origin;
					Quaternion quaternion;
					device.TryGetFeatureValue(CommonUsages.devicePosition, out origin);
					device.TryGetFeatureValue(CommonUsages.deviceRotation, out quaternion);

					origin -= centerEyePosition - Camera.main.transform.position;

//					tip = origin + quaternion * new Vector3(0f, 0f, 10f);
					tip = origin + quaternion * new Vector3(0f, 0f, 1.2f);
					var renderer = laserPointer.GetComponent<LineRenderer>();
					renderer.useWorldSpace = true;
					renderer.SetPosition(0, origin);
					renderer.SetPosition(1, tip);
//					renderer.startWidth = 0.01f;
//					renderer.endWidth = 0.01f;
					renderer.startWidth = 0.002f;
					renderer.endWidth = 0.002f;

//					float maxRayDistance = 1000.0f;
					float maxRayDistance = 1.2f;
					var ray = new Ray(origin, (tip - origin).normalized);
					if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity))
					{
						renderer.SetPosition(0, origin);
						renderer.SetPosition(1, hitInfo.point);

//						laserPointer.GetComponent<Pointer>().ShowPointer(hitInfo.point);
					}
					else
					{
						renderer.SetPosition(0, origin);
						renderer.SetPosition(1, origin + (tip - origin).normalized * maxRayDistance);

//						laserPointer.GetComponent<Pointer>().HidePointer();
					}

//					RaycastHit hitInfo;
//					float distance = 10f;
//					Physics.Raycast(origin, quaternion.eulerAngles, out hitInfo);
//					GameObject pointedObject = hitInfo.collider.gameObject;
//					pointedObject.transform.SendMessage("OnPointerEnter");
				}
			}
		}
	}
}