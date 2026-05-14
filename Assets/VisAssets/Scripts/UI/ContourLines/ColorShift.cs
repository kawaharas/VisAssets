using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.ContourLines.UI
{
	public class ColorShift : MonoBehaviour
	{
		private GameObject target = null;

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
				var component = target.GetComponent<ContourLines>();
				if (component != null)
				{
					component.SetColorShift(value);
				}
			}
		}
	}
}