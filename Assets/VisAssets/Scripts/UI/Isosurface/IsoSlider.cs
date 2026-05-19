using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.Isosurface.UI
{
	public class IsoSlider : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject inputField;
		public  GameObject placeholder;

		private Slider uiSlider;
		private bool isInitialized = false;

		void Start()
		{
			target   = GetComponentInParent<UIPanel>().TargetModule;
			uiSlider = GetComponent<Slider>();
			uiSlider.onValueChanged.AddListener(OnValueChanged);
		}

		void Update()
		{
			if (!isInitialized && target != null)
			{
				var isosurface = target.GetComponent<Isosurface>();

				if (isosurface != null && isosurface.max > isosurface.min)
				{
					uiSlider.minValue = isosurface.min;
					uiSlider.maxValue = isosurface.max;
					uiSlider.value = isosurface.threshold;

					UpdateText(isosurface.threshold);

					isInitialized = true;
				}
			}
		}

		public void OnValueChanged(float value)
		{
			if (target != null)
			{
				var isosurface = target.GetComponent<Isosurface>();

				if (isosurface != null)
				{
					isosurface.SetValue(value);

					UpdateText(value);
				}
			}
		}

		private void UpdateText(float value)
		{
			string formattedValue = (value < 0.001f || value > 1000f) ? value.ToString("E3") : value.ToString("G5");

			if (inputField != null)
			{
				var input = inputField.GetComponent<InputField>();

				if (input != null)
				{
					input.text = formattedValue;
				}
			}
			if (placeholder != null)
			{
				var text = placeholder.GetComponent<Text>();

				if (text != null)
				{
					text.text = formattedValue;
				}
			}
		}
	}
}