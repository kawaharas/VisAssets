using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Arrows
{
	using VIS;

	public class SliceSelector : MonoBehaviour
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
				var arrows = target.GetComponent<Arrows>();
				if (arrows != null)
				{
					arrows.SetSlice(value);
					inputField.GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
				}
			}
		}
	}
}