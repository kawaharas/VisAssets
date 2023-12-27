using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.StreamLines.UI
{
	public class SetSeed : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject sliderX;
		public  GameObject sliderY;
		public  GameObject sliderZ;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var button = GetComponent<Button>();
			button.onClick.AddListener(OnClick);
			sliderX.GetComponent<Slider>().onValueChanged.AddListener(OnValueChanged);
			sliderY.GetComponent<Slider>().onValueChanged.AddListener(OnValueChanged);
			sliderZ.GetComponent<Slider>().onValueChanged.AddListener(OnValueChanged);
		}

		public void OnClick()
		{
			if (target != null)
			{
				target.GetComponent<StreamLines>().displayTime = 5f;
				var x = sliderX.GetComponent<Slider>().value;
				var y = sliderY.GetComponent<Slider>().value;
				var z = sliderZ.GetComponent<Slider>().value;
				target.GetComponent<StreamLines>().AddSeed(x, y, z);
			}
		}

		public void OnValueChanged(float value)
		{
			if (target != null)
			{
				var x = sliderX.GetComponent<Slider>().value;
				var y = sliderY.GetComponent<Slider>().value;
				var z = sliderZ.GetComponent<Slider>().value;
				target.GetComponent<StreamLines>().p0[0] = x;
				target.GetComponent<StreamLines>().p0[1] = y;
				target.GetComponent<StreamLines>().p0[2] = z;
				target.GetComponent<StreamLines>().displayTime = 5f;
			}
		}
	}
}