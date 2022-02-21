using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets.SciVis.Structured.DataLoader.UI
{
	public class SetData : MonoBehaviour
	{
		private GameObject target = null;
		public GameObject inputField;

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
				string str = inputField.GetComponent<InputField>().text;

/*
				var filename = target.transform.Find("FileBrowser").GetComponent<String_param>();
				if (filename != null)
				{
					// TODO: add routine for error
					filename.SetString(str);
				}
*/
//				target.GetComponent<ReadData>().filename = str;
//				target.GetComponent<ReadData>().Exec();

				var readfield = target.GetComponent<ReadField>();
				if (readfield != null)
				{
					readfield.filename = str;
					readfield.Exec();
				}
				var readV5 = target.GetComponent<ReadV5>();
				if (readV5 != null)
				{
					readV5.filename = str;
					readV5.Exec();
				}
				var readGrADS = target.GetComponent<ReadGrADS>();
				if (readGrADS != null)
				{
					readGrADS.filename = str;
					readGrADS.Exec();
				}
			}
		}
	}
}