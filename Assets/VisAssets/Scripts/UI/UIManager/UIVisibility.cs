using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.EventSystems;

namespace VisAssets
{
	public class UIVisibility : MonoBehaviour
	{
		private float sensitivity = 0.2f;
		private float time_diff;
		public bool IsTapped;
		public bool IsVisible;

		void Start()
		{
		}

		void Update()
		{
			if (IsTapped)
			{
				time_diff += Time.deltaTime;
				if (time_diff < sensitivity)
				{
					if (XRSettings.enabled)
					{
						if (Input.GetMouseButtonDown(0))
						{
							IsTapped = false;
							StartCoroutine(LoadDevice("None"));
							time_diff = 0.0f;
						}
					}
					else
					{
						if (Input.GetMouseButtonDown(0))
						{
							IsTapped = false;
							TogglePanelVisibility();
							time_diff = 0.0f;
						}
					}
				}
				else
				{
					IsTapped = false;
					time_diff = 0.0f;
				}
			}
			else
			{
				if (Input.GetMouseButtonDown(0))
				{
					IsTapped = true;
				}
			}
		}

		void TogglePanelVisibility()
		{
			IsVisible = !IsVisible;
			var target = transform.Find("MainPanel");
			if (target != null)
			{
				target.gameObject.SetActive(IsVisible);
			}
		}

		IEnumerator LoadDevice(string device)
		{
			XRSettings.LoadDeviceByName(device);
			yield return null;
			XRSettings.enabled = false;
			Camera.main.ResetAspect();
		}
	}
}