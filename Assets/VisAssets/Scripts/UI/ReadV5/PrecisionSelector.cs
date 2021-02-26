using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.UI.ReadV5
{
	using VisAssets;

	public class PrecisionSelector : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var dropdown = GetComponent<Dropdown>();
			dropdown.ClearOptions();
			var modeNum = Enum.GetNames(typeof(ReadV5.PRECISION)).Length;
			for (int i = 0; i < modeNum; i++)
			{
				var modeString = Enum.GetName(typeof(ReadV5.PRECISION), i);
				dropdown.options.Add(new Dropdown.OptionData { text = modeString });
			}
			dropdown.interactable = true;
			dropdown.RefreshShownValue();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(int value)
		{
			var readV5 = target.GetComponent<ReadV5>();
			if (readV5 != null)
			{
				readV5.SetPrecision(value);
			}
		}
	}
}