using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.StreamLines.UI
{
	public class ClearAllSeeds : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var button = GetComponent<Button>();
			button.onClick.AddListener(OnClick);
		}

		public void OnClick()
		{
			if (target != null)
			{
				target.GetComponent<StreamLines>().ClearAllSeeds();
			}
		}
	}
}