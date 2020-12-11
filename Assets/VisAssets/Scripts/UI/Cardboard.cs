using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace VIS
{
	public class Cardboard : MonoBehaviour
	{
		private bool xrState = false;

		void Start()
		{
			var button = GetComponent<Button>();
			button.onClick.AddListener(OnClick);
		}

		private void Update()
		{
			if (XRSettings.enabled == false)
			{
				Camera.main.GetComponent<Transform>().localRotation =
					InputTracking.GetLocalRotation(XRNode.CenterEye);
			}
		}

		void OnClick()
		{
			//			xrState = !xrState;
			//			XRSettings.enabled = xrState;
//			XRSettings.LoadDeviceByName("Cardboard");
//			XRSettings.enabled = true;
			StartCoroutine(LoadDevice("Cardboard"));
		}

		IEnumerator LoadDevice(string device)
		{
			XRSettings.LoadDeviceByName(device);
			yield return null;
			XRSettings.enabled = true;
		}
	}
}