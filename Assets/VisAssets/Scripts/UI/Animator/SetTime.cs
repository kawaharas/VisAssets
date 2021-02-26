using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.UI.Animator
{
	using VisAssets;

	public class SetTime : MonoBehaviour
	{
		private GameObject target = null;

		// Start is called before the first frame update
		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var slider = GetComponent<Slider>();
			slider.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnValueChanged(float value)
		{
			if (target != null)
			{
				target.GetComponent<Animator>().timeOut = value;
			}
		}
	}
}