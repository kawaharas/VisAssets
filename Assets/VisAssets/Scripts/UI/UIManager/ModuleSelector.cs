using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS
{
	public class ModuleSelector : MonoBehaviour
	{
		Dropdown dropdown;

		void Start()
		{
			dropdown = GetComponent<Dropdown>();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(int value)
		{
			var moduleName = dropdown.options[value].text;
			var parent = GameObject.Find("ParamChanger");
			foreach (Transform childTransform in parent.transform)
			{
				childTransform.gameObject.SetActive(false);
			}
			if (moduleName != "None")
			{
//				parent.transform.Find(moduleName).gameObject.SetActive(true);
				var module = parent.transform.Find(moduleName);
				if (module != null)
				{
					module.gameObject.SetActive(true);
				}
			}
		}
	}
}