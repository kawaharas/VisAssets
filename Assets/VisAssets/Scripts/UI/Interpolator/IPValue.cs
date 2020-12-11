﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS
{
	public class IPValue : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject slider;
		public  GameObject placeholder;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var inputField = GetComponent<InputField>();
			inputField.onEndEdit.AddListener(OnEndEdit);
		}

		public void OnEndEdit(string str)
		{
			if (target != null)
			{
				var interpolator = target.GetComponent<Interpolator>();
				if (interpolator != null)
				{
					var value = Convert.ToInt16(str);
					int[] dims = new int[3];
					dims[0] = interpolator.idims[0];
					dims[1] = interpolator.idims[1];
					dims[2] = interpolator.idims[2];
					var axis = transform.parent.gameObject.name;
					switch (axis)
					{
						case "X":
							dims[0] = value;
							break;
						case "Y":
							dims[1] = value;
							break;
						case "Z":
							dims[2] = value;
							break;
						default:
							break;
					}
					interpolator.SetDims(dims);
					GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
					slider.GetComponent<Slider>().value = value;
				}
			}
		}
	}
}