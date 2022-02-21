using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.Outline.UI
{
	public class OuterMesh : MonoBehaviour
	{
		private GameObject target = null;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var toggle = GetComponent<Toggle>();
			if (target != null)
			{
				toggle.isOn = target.GetComponent<Outline>().drawOuterMesh;
			}
			toggle.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(bool value)
		{
			if (target != null)
			{
				target.GetComponent<Outline>().SetStateOuterMesh(value);
			}
		}
	}
}