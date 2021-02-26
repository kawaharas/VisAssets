using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets
{
	public class Visibility : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var toggle = GetComponent<Toggle>();
			toggle.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(bool value)
		{
			if (target != null)
			{
				target.SetActive(value);
			}
		}
	}
}