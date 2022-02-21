//using System.Collections;
//using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.ExtractVector.UI
{
	public class ActivateElements : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject dropdown = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var toggle = GetComponent<Toggle>();
			toggle.onValueChanged.AddListener(OnValueChanged);
			if (dropdown != null)
			{
				dropdown.GetComponent<Dropdown>().interactable = false;
			}
		}

		public void OnValueChanged(bool value)
		{
			if (target == null) return;

			var selectedLabel =
				this.GetComponentsInChildren<Text>().First(t => t.name == "Label").text;
			var extractVector = target.GetComponent<ExtractVector>();
			if (extractVector != null)
			{
				switch (selectedLabel)
				{
					case "U":
						extractVector.SetActive(0, value);
						break;
					case "V":
						extractVector.SetActive(1, value);
						break;
					case "W":
						extractVector.SetActive(2, value);
						break;
					default:
						break;
				}
				dropdown.GetComponent<Dropdown>().interactable = value;
			}
		}
	}
}