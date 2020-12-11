﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.EventSystems;

namespace VIS
{
	public class UIVisibility : MonoBehaviour
	{
		private float sensitivity = 0.3f;
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
//							XRSettings.LoadDeviceByName("None");
//							XRSettings.enabled = false;
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
//					TogglePanelVisibility();
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
			var target = transform.Find("Panels");
			target.gameObject.SetActive(IsVisible);
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