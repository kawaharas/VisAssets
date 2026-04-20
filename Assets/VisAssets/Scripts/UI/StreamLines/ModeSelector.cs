using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.StreamLines.UI
{
	public class ModeSelector : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var dropdown = GetComponent<Dropdown>();
			dropdown.ClearOptions();
			var modeNum = Enum.GetNames(typeof(StreamLines.DrawMode)).Length;
			for (int i = 0; i < modeNum; i++)
			{
				var modeString = Enum.GetName(typeof(StreamLines.DrawMode), i);
				dropdown.options.Add(new Dropdown.OptionData { text = modeString });
			}
			dropdown.interactable = true;
			dropdown.RefreshShownValue();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(int value)
		{
			var component = target.GetComponent<StreamLines>();
			if (component != null)
			{
				component.SetMode(value);
			}
		}
	}
}