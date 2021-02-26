using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets
{
	public class IsoValue : MonoBehaviour
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
				var isosurface = target.GetComponent<Isosurface>();
				if (isosurface != null)
				{
					var value = Convert.ToSingle(str);
					isosurface.SetValue(value);
					GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
//					slider.GetComponent<Slider>().value = Convert.ToSingle(str) / 10.0f;
				}
			}
		}
	}
}