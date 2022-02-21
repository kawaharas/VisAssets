using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets
{
	public class AnimPlay : MonoBehaviour
	{
		private GameObject target = null;
		private bool playState = false;

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
				playState = !playState;
				if (playState)
				{
					GetComponentInChildren<Text>().text = "Stop";
				}
				else
				{
					GetComponentInChildren<Text>().text = "Play";
				}
				target.GetComponent<Animator>().onPlay = playState;
			}
		}
	}
}