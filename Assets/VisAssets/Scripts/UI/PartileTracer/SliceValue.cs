using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.ParticleTracer.UI
{
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
				var component = target.GetComponent<ParticleTracer>();
				if (component != null)
				{
					var valueInt = Convert.ToInt32(str);
					component.SetSlice(valueInt);
					GetComponent<InputField>().text = valueInt.ToString();
					placeholder.GetComponent<Text>().text = valueInt.ToString();
					slider.GetComponent<Slider>().value = Convert.ToSingle(str) / 10.0f;
				}
			}
		}
	}
}