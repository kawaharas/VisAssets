using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.ContourLines.UI
{
	public class NumLevelsSelector : MonoBehaviour
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
				var component = target.GetComponent<ContourLines>();
				if (component != null)
				{
					int intValue = Mathf.RoundToInt(value);
					component.SetNumLevels(intValue);
					inputField.GetComponent<InputField>().text = intValue.ToString();
					placeholder.GetComponent<Text>().text = intValue.ToString();
				}
			}
		}
	}
}