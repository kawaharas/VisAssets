using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.UI.Isosurface
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
			var modeNum = Enum.GetNames(typeof(Isosurface.SHADING_MODE)).Length;
			for (int i = 0; i < modeNum; i++)
			{
				var modeString = Enum.GetName(typeof(Isosurface.SHADING_MODE), i);
				dropdown.options.Add(new Dropdown.OptionData { text = modeString });
			}
			dropdown.interactable = true;
			dropdown.RefreshShownValue();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		// Update is called once per frame
		public void OnValueChanged(int value)
		{
			var isosurface = target.GetComponent<Isosurface>();
			if (isosurface != null)
			{
				isosurface.SetShadingMode(value);
			}
		}
	}
}