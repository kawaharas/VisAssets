using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.DataLoader.UI
{
	public class SetDummyData : MonoBehaviour
	{
		private GameObject target = null;
		public GameObject fileSelector;

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
				fileSelector.GetComponent<CanvasGroup>().interactable = !value;
				target.GetComponent<ReadField>().useDummyData = value;
			}
		}
	}
}