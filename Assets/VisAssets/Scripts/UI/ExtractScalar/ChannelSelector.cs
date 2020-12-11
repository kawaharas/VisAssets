using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS
{
	public class ChannelSelector : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var dropdown = GetComponent<Dropdown>();
			dropdown.onValueChanged.AddListener(OnValueChanged);
//			dropdown.interactable = false;
		}

		public void OnValueChanged(int value)
		{
			if (target != null)
			{
				var extractScalar = target.GetComponent<ExtractScalar>();
				if (extractScalar != null)
				{
					extractScalar.SetChannel(value);
				}
			}
		}
	}
}