using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.Animations;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.ParticleTracer.UI
{
    public class SetColorMode : MonoBehaviour
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
				target.GetComponent<ParticleTracer>().useMagnitudeColor = value;
			}
		}
	}
}