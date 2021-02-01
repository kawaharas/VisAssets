﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Animator
{
	using VIS;

	public class SetInterval : MonoBehaviour
	{
		private GameObject target = null;
		private bool playState = false;

		void Start()
		{
			var slider = GetComponent<Slider>();
			slider.onValueChanged.AddListener(OnValueChanged);

			target = GetComponentInParent<UIPanel>().TargetModule;
			if (target != null)
			{
				slider.value = target.GetComponent<Animator>().timeOut;
			}
		}

		public void OnValueChanged(float value)
		{
			if (target != null)
			{
				target.GetComponent<Animator>().timeOut = value;
			}
		}
	}
}