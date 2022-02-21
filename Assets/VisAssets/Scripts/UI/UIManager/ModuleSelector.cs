using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.UI;

namespace VisAssets
{
	public class ModuleSelector : MonoBehaviour
	{
		public enum ButtonState
		{
			RELEASED,
			PRESSED,
			KEEP_PRESSING
		}

		Dropdown dropdown;
		public bool inputValue;
		public ButtonState ButtonTrigger = ButtonState.RELEASED;

		void Start()
		{
			dropdown = GetComponent<Dropdown>();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		void Update()
		{
/*
			if (XRSettings.enabled)
			{
				var inputDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);
				foreach (var device in inputDevices)
				{
					if (device.TryGetFeatureValue(CommonUsages.triggerButton, out inputValue) && inputValue)
					{
						if (ButtonTrigger == ButtonState.RELEASED)
						{
							ButtonTrigger = ButtonState.PRESSED;
							var eventData = new PointerEventData(EventSystem.current);
//							var dropdown = this.transform.Find("Dropdown").gameObject;
//							dropdown.GetComponent<ModuleSelector>().OnClick(eventData);
//							OnClick(eventData);
							dropdown.OnPointerClick(eventData);
							var dropdownList = this.transform.Find("Dropdown List");
							if (dropdownList != null)
							{
								var boxCollider = dropdownList.GetComponent<BoxCollider>();
								if (boxCollider == null)
								{
									dropdownList.gameObject.AddComponent<BoxCollider>();
									dropdownList.gameObject.AddComponent<ResizeCollider>();
									dropdownList.gameObject.AddComponent<SelectModule>();
									dropdownList.gameObject.GetComponent<SelectModule>().dropdown = dropdown;
								}
							}
						}
						else if (ButtonTrigger == ButtonState.PRESSED)
						{
							ButtonTrigger = ButtonState.KEEP_PRESSING;
						}
					}
					else
					{
						ButtonTrigger = ButtonState.RELEASED;
					}
				}
			}
*/
		}

		public void OnValueChanged(int value)
		{
			var moduleName = dropdown.options[value].text;
			var parent = GameObject.Find("ParamChanger");
			foreach (Transform childTransform in parent.transform)
			{
				childTransform.gameObject.SetActive(false);
			}
			if (moduleName != "None")
			{
//				parent.transform.Find(moduleName).gameObject.SetActive(true);
				var module = parent.transform.Find(moduleName);
				if (module != null)
				{
					module.gameObject.SetActive(true);
				}
			}
		}

		public void OnCollisionEnter(Collision collision)
		{
			Debug.Log("Enter");
		}

		public void OnCollisionExit(Collision collision)
		{
			Debug.Log("Exit");
		}

		public void OnClick(PointerEventData eventData)
		{
			dropdown.OnPointerClick(eventData);
		}
	}
}