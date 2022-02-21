using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.Arrows.UI
{
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
				var arrows = target.GetComponent<Arrows>();
				if (arrows != null)
				{
					switch (selectedLabel)
					{
						case "I":
							arrows.SetAxis(0);
							break;
						case "J":
							arrows.SetAxis(1);
							break;
						case "K":
							arrows.SetAxis(2);
							break;
						default:
							break;
					}
				}
			}
		}
	}
}