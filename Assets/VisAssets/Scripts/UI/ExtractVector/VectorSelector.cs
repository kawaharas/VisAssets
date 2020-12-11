using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace VIS.UI.ExtractVector
{
	using VIS;

	public class VectorSelector : MonoBehaviour
	{
		private GameObject target = null;
		public int vectorID;

		void Start()
		{
			target = GetComponentInParent<UIPanel>().TargetModule;
			var dropdown = GetComponent<Dropdown>();
			dropdown.onValueChanged.AddListener(OnValueChanged);
//			dropdown.interactable = false;
		}

		public void OnValueChanged(int value)
		{
			if (target != null)
			{
				var extractVector = target.GetComponent<ExtractVector>();
				if (extractVector != null)
				{
					extractVector.SetChannel(vectorID, value);
				}
			}
		}
/*
		public void Initialize()
		{
			if (target != null)
			{
				var dropdown = GetComponent<Dropdown>();
				dropdown.ClearOptions();
				var extractVector = target.GetComponent<ExtractVector>();
				for (int i = 0; i < extractVector.df.elements.Length; i++)
				{
					if (extractVector.df.elements[i].varName == "")
					{
						dropdown.options.Add(new Dropdown.OptionData { text = "variable #" + i.ToString() });
					}
					else
					{
						dropdown.options.Add(new Dropdown.OptionData { text = extractVector.df.elements[i].varName });
					}
				}
				dropdown.interactable = true;
				dropdown.RefreshShownValue();
			}
		}
*/
	}
}