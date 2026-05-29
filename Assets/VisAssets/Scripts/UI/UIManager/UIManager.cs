using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.SpatialTracking;
using VisAssets.SciVis.Structured.StreamLines;
#endif

#if UNITY_EDITOR
using UnityEditor;
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
	{
		public enum ButtonState
		{
			RELEASED,
			PRESSED,
			KEEP_PRESSING
		}

		public Dictionary<string, int> moduleCounter = new Dictionary<string, int>();
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

		public GameObject moduleSelector;
		public GameObject paramChanger;
		public GameObject laserPointer;
		public Vector3    tip;

		public GameObject currentModule;

#if UNITY_XR_MANAGEMENT
		RaycastHit hitInfo;
#endif

		void Awake()
		{
			currentModule = null;

#if UNITY_XR_MANAGEMENT
			hitInfo = new RaycastHit();
#endif
		}
/*
		void Start()
		{
			if (Application.platform != RuntimePlatform.Android)
			{
				if (cardboardButton != null)
				{
					cardboardButton.SetActive(false);
				}
			}

			if (IsXRActive)
			{
				var canvas = transform.Find("Canvas");
				canvas.gameObject.SetActive(false);

				if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
				{
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

					canvas.GetComponent<Canvas>().worldCamera = uiCamera;
				}

				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

				SetupPointer();
			}
			else
			{
				SetupDesktopCanvas();
			}
		}
*/
		void Start()
		{
		    if (Application.platform != RuntimePlatform.Android)
		    {
		        if (cardboardButton != null)
		        {
		            cardboardButton.SetActive(false);
		        }
		    }

		    if (IsXRActive)
		    {
		        var canvas = transform.Find("Canvas");
		        if (canvas != null)
		        {
		            canvas.gameObject.SetActive(false);
		        }

		        var mainCamera = Camera.main;
		        if (mainCamera != null)
		        {
		            var uiCamObj = GameObject.Find("UI Camera");
		            Camera uiCamera = null;

		            if (uiCamObj == null)
		            {
		                uiCamObj = new GameObject("UI Camera");
		                uiCamera = uiCamObj.AddComponent<Camera>();
		                uiCamObj.transform.parent = mainCamera.transform;
		                uiCamObj.transform.localPosition = Vector3.zero;
		                uiCamObj.transform.localRotation = Quaternion.identity;

		                uiCamera.clearFlags = CameraClearFlags.Depth;
		                uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");

		                mainCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));

		                System.Type additionalDataComponentType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
		                if (additionalDataComponentType != null)
		                {
		                    var mainCamData = mainCamera.GetComponent(additionalDataComponentType);

		                    var uiCamData = uiCamObj.AddComponent(additionalDataComponentType);
		                    var renderTypeField = additionalDataComponentType.GetProperty("renderType");
		                    System.Type renderTypeEnum = System.Type.GetType("UnityEngine.Rendering.Universal.CameraRenderType, Unity.RenderPipelines.Universal.Runtime");
		                    if (renderTypeField != null && renderTypeEnum != null)
		                    {
		                        var overlayValue = System.Enum.Parse(renderTypeEnum, "Overlay");
		                        renderTypeField.SetValue(uiCamData, overlayValue);
		                    }

		                    var cameraStackProperty = additionalDataComponentType.GetProperty("cameraStack");
		                    if (cameraStackProperty != null && mainCamData != null)
		                    {
		                        var stack = cameraStackProperty.GetValue(mainCamData) as System.Collections.IList;
		                        if (stack != null)
		                        {
		                            stack.Add(uiCamera);
		                        }
		                    }
		                }
		                else
		                {
		                    uiCamera.depth = mainCamera.depth + 1;
		                }
		            }
		            else
		            {
		                uiCamera = uiCamObj.GetComponent<Camera>();
		            }

		            if (canvas != null)
		            {
		                var canvasComp = canvas.GetComponent<Canvas>();
		                canvasComp.renderMode = RenderMode.WorldSpace;
		                canvasComp.worldCamera = uiCamera;
		            }
		        }

		        SetupPointer();
		    }
		    else
		    {
		        SetupDesktopCanvas();
		    }
		}

		public bool IsXRActive
		{
			get
			{
#if UNITY_XR_MANAGEMENT
				if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
				{
					return XRGeneralSettings.Instance.Manager.isInitializationComplete;
				}
#endif
				return false;
			}
		}

//		void Update()
		void LateUpdate()
		{
#if UNITY_XR_MANAGEMENT
			if (IsXRActive)
			{
				var canvas = transform.Find("Canvas");
				var inputDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);
				foreach (var device in inputDevices)
				{
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
						canvas.gameObject.SetActive(false);
						ButtonA = ButtonState.RELEASED;
					}

					// trigger button (operate VRUI)
					if (!canvas.gameObject.activeSelf)
					{
						if (device.TryGetFeatureValue(CommonUsages.triggerButton, out inputValue) && inputValue)
						{
							if (ButtonTrigger == ButtonState.RELEASED)
							{
								ButtonTrigger = ButtonState.PRESSED;
							}
							else if (ButtonTrigger == ButtonState.PRESSED)
							{
								ButtonTrigger = ButtonState.KEEP_PRESSING;
							}
						}
						else
						{
							if (ButtonTrigger != ButtonState.RELEASED)
							{
								if (currentModule != null && currentModule.name.StartsWith("StreamLines"))
								{
									var streamLines = currentModule.GetComponent<StreamLines>();
									if (streamLines != null)
									{
										Vector3 localTip = streamLines.transform.InverseTransformPoint(tip);
										streamLines.AddSeed(localTip);
									}
								}
							}
							ButtonTrigger = ButtonState.RELEASED;
						}
					}
				}

				// toggle visible state of laser pointer
				if (canvas.gameObject.activeSelf)
				{
					// For VRUI
					if (ButtonA == ButtonState.PRESSED)
					{
						laserPointer.SetActive(true);
					}
					else if (ButtonA == ButtonState.RELEASED)
					{
						laserPointer.SetActive(false);
					}
				}
				else
				{
					if (currentModule == null)
					{
						laserPointer.SetActive(false);
						return;
					}

					// For Streamline module
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
					else
					{
						if (ButtonTrigger == ButtonState.RELEASED)
						{
							laserPointer.SetActive(false);
						}
					}
				}

				DrawPointer();
			}
#endif

			if (Application.platform != RuntimePlatform.Android)
			{
				if (Input.GetKeyDown(KeyCode.F3))
				{
					if (IsXRActive)
					{
						Debug.Log("Switch XR Mode to None.");
						StartCoroutine(LoadDevice("None"));
					}
					else
					{
						StartCoroutine(LoadDevice("Enable"));
					}
				}
			}
		}

		private void SetupDesktopCanvas()
		{
			var canvasTransform = transform.Find("Canvas");

			if (canvasTransform != null)
			{
				var canvas = canvasTransform.GetComponent<Canvas>();

				if (canvas != null)
				{
					canvasTransform.gameObject.SetActive(true);
					canvas.renderMode = RenderMode.ScreenSpaceCamera;
					Camera mainCam = Camera.main;

					if (mainCam != null)
					{
						canvas.worldCamera   = mainCam;
						canvas.planeDistance = mainCam.nearClipPlane * 1.01f;
					}
				}
			}
		}

		IEnumerator LoadDevice(string device)
		{
#if UNITY_XR_MANAGEMENT
			if (device == "None")
			{
				if (IsXRActive)
				{
					XRGeneralSettings.Instance.Manager.StopSubsystems();
					XRGeneralSettings.Instance.Manager.DeinitializeLoader();
				}
				XRState = false;
				SetupDesktopCanvas();
			}
			else
			{
				if (!IsXRActive)
				{
					yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

					if (XRGeneralSettings.Instance.Manager.activeLoader != null)
					{
						XRGeneralSettings.Instance.Manager.StartSubsystems();
						XRState = true;
					}
				}
			}
#else
			yield return null;
#endif
		}

		public void SetXRDevice(int deviceID)
		{
			var deviceName = supportedDevices[deviceID];
			StartCoroutine(LoadDevice(deviceName));
			selectedDeviceID = deviceID;
		}

		public void EnableXR()
		{
			StartCoroutine(LoadDevice("Enable"));
		}

		public void DisableXR()
		{
			StartCoroutine(LoadDevice("None"));
		}

		public void ShowUIManager()
		{
			if (IsXRActive)
			{
				var canvas = transform.Find("Canvas");
				canvas.gameObject.SetActive(true);
//				canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
				var rectTransform = canvas.GetComponent<RectTransform>();
				var camera = Camera.main;
				var rotation = camera.transform.rotation.eulerAngles;
				var direction = Quaternion.AngleAxis(rotation.y, Vector3.up);

				rectTransform.localPosition = camera.transform.position + direction * new Vector3(0f, 0f, 1f);
				rectTransform.localRotation = direction;
				rectTransform.sizeDelta  = new Vector2(100, 100);
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

#if UNITY_XR_MANAGEMENT
		void DrawPointer()
		{
			if (laserPointer != null)
			{
				var centerEyePosition = new Vector3();
				var centerEyeDevices  = new List<InputDevice>();

				InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, centerEyeDevices);

				foreach (var device in centerEyeDevices)
				{
					device.TryGetFeatureValue(CommonUsages.devicePosition, out centerEyePosition);
				}

				var inputDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);

				foreach (var device in inputDevices)
				{
					Vector3 origin;
					Quaternion quaternion;
					device.TryGetFeatureValue(CommonUsages.devicePosition, out origin);
					device.TryGetFeatureValue(CommonUsages.deviceRotation, out quaternion);

					origin -= centerEyePosition - Camera.main.transform.position;

					tip = origin + quaternion * new Vector3(0f, 0f, 1f);
					var renderer = laserPointer.GetComponent<LineRenderer>();
					renderer.useWorldSpace = true;
					renderer.SetPosition(0, origin);
					renderer.SetPosition(1, tip);
					renderer.startWidth = 0.002f;
					renderer.endWidth   = 0.002f;

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
#endif
	}
}