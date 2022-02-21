using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace VisAssets
{
public class SelectModule : MonoBehaviour, IPointerClickHandler
{
	public enum ButtonState
	{
		RELEASED,
		PRESSED,
		KEEP_PRESSING
	}

	public Dropdown dropdown;
	public bool inputValue;
	public ButtonState ButtonTrigger = ButtonState.RELEASED;

	// Update is called once per frame
	void Update()
	{
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
//						dropdown.OnPointerClick(eventData);
//						GetComponent<Canvas>().
//						ExecuteEvents.Execute(GetComponent<Canvas>(), eventData, ExecuteEvents.pointerClickHandler);


//						this.OnPointerClick(eventData);
/*
						var dropdownList = this.transform.Find("Dropdown List");
						if (dropdownList != null)
						{
							var boxCollider = dropdownList.GetComponent<BoxCollider>();
							if (boxCollider == null)
							{
								dropdownList.gameObject.AddComponent<BoxCollider>();
								dropdownList.gameObject.AddComponent<ResizeCollider>();
								dropdownList.gameObject.AddComponent<SelectModule>();
							}
						}
						Debug.Log("Trigger is pressed.");
*/
					}
					else if (ButtonTrigger == ButtonState.PRESSED)
					{
						Debug.Log("Trigger button keep pressing.");
						ButtonTrigger = ButtonState.KEEP_PRESSING;
					}
				}
				else
				{
					ButtonTrigger = ButtonState.RELEASED;
				}
			}
		}
	}

	void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
	{
	}
}
}