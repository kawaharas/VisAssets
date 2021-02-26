using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.UI.Slicer
{
	using VisAssets;

	public class ModeSelector : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var dropdown = GetComponent<Dropdown>();
			dropdown.ClearOptions();
			var modeNum = Enum.GetNames(typeof(Slicer.FILTER_MODE)).Length;
			for (int i = 0; i < modeNum; i++)
			{
				var modeString = Enum.GetName(typeof(Slicer.FILTER_MODE), i);
				dropdown.options.Add(new Dropdown.OptionData { text = modeString });
			}
			dropdown.interactable = true;
			dropdown.RefreshShownValue();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(int value)
		{
			var slicer = target.GetComponent<Slicer>();
			if (slicer != null)
			{
				slicer.SetMode(value);
			}
		}
	}
}