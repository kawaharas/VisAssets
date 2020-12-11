using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Slicer
{
	using VIS;

	public class SliceValue : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject placeholder;
		public  GameObject slider;

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
				var slicer = target.GetComponent<Slicer>();
				if (slicer != null)
				{
					var value = Convert.ToSingle(str);
					slicer.SetSlice(value);
					GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
					slider.GetComponent<Slider>().value = value;
				}
			}
		}
	}
}