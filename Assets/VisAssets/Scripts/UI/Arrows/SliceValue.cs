using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Arrows
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
				var arrows = target.GetComponent<Arrows>();
				if (arrows != null)
				{
					var valueInt = Convert.ToInt32(str);
					arrows.SetSlice(valueInt);
					GetComponent<InputField>().text = valueInt.ToString();
					placeholder.GetComponent<Text>().text = valueInt.ToString();
					slider.GetComponent<Slider>().value = Convert.ToSingle(str) / 10.0f;
				}
			}
		}
	}
}