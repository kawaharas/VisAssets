using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Slicer
{
	using VIS;

	public class AxisSelector : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject slider = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var toggle = GetComponent<Toggle>();
			toggle.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(bool value)
		{
			if (!value) return;

			if (target != null)
			{
				var selectedLabel =
					this.GetComponentsInChildren<Text>().First(t => t.name == "Label").text;
				var slicer = target.GetComponent<Slicer>();
				if (slicer != null)
				{
					switch (selectedLabel)
					{
						case "I":
							slicer.SetAxis(0);
							break;
						case "J":
							slicer.SetAxis(1);
							break;
						case "K":
							slicer.SetAxis(2);
							break;
						default:
							break;
					}
					if (slider != null)
					{
						slider.GetComponent<Slider>().value = slicer.slice;
					}
				}
			}
		}
	}
}