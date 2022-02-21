using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets
{
	public class SetInterval : MonoBehaviour
	{
		private GameObject target = null;

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