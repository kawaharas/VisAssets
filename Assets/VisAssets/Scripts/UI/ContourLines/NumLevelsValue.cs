using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.ContourLines.UI
{
	public class NumLevelsValue : MonoBehaviour
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
				var component = target.GetComponent<ContourLines>();
				if (component != null)
				{
					if (int.TryParse(str, out int value))
					{
						component.SetNumLevels(value);
						GetComponent<InputField>().text = value.ToString();
						placeholder.GetComponent<Text>().text = value.ToString();
						slider.GetComponent<Slider>().value = value;
					}
/*
					var value = Convert.ToUInt16(Math.Abs(Convert.ToSingle(str)));
					component.SetNumLevels(value);
					GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
					slider.GetComponent<Slider>().value = value;
*/
				}
			}
		}
	}
}