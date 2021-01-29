using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS.UI.Downsize
{
	using VIS;

	public class IPSlider : MonoBehaviour
	{
		private GameObject target = null;
		public  GameObject inputField;
		public  GameObject placeholder;

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
				var downsize = target.GetComponent<Downsize>();
				if (downsize != null)
				{
					int[] dims = new int[3];
					dims[0] = downsize.idims[0];
					dims[1] = downsize.idims[1];
					dims[2] = downsize.idims[2];
					var axis = transform.parent.gameObject.name;
					switch (axis)
					{
						case "X":
							dims[0] = (int)value;
							break;
						case "Y":
							dims[1] = (int)value;
							break;
						case "Z":
							dims[2] = (int)value;
							break;
						default:
							break;
					}
					downsize.SetDims(dims);
					inputField.GetComponent<InputField>().text = value.ToString();
					placeholder.GetComponent<Text>().text = value.ToString();
				}
			}
		}
	}
}