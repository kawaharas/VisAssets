using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SimpleFileBrowser;

namespace VisAssets
{
	public class OpenFileBrowser : MonoBehaviour
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
			if (inputField != null)
			{
				StartCoroutine(ShowLoadDialogCoroutine());
			}
		}

		IEnumerator ShowLoadDialogCoroutine()
		{
			yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, "Load File", "Load");
			if (FileBrowser.Success)
			{
				inputField.GetComponent<InputField>().text = FileBrowser.Result[0];
			}
		}
	}
}