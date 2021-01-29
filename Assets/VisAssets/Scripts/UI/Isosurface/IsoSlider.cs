using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Isosurface
{
	using VIS;

	public class IsoSlider : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject inputField;
		public  GameObject placeholder;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var slider = GetComponent<Slider>();
			slider.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(float value)
		{
			if (target != null)
			{
				var isosurface = target.GetComponent<Isosurface>();
				if (isosurface != null)
				{
//					var value = (int)(value * 10.0f);
					isosurface.SetValue(value);
					inputField.GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
				}
			}
		}
	}
}