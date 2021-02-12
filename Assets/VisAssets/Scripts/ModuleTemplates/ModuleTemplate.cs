using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS
{
	public class ModuleTemplate : MonoBehaviour
	{
		[SerializeField, ReadOnly]
		public GameObject UIPanel;
		[SerializeField, ReadOnly]
		public string FixedModuleName;
		GameObject UIPrefab;
		bool disableUI = false;

		public enum ModuleType
		{
			READING,
			FILTERING,
			MAPPING,
			UNDEFINED
		}

		enum ModuleName
		{
			Arrows = 0,
			Animator,
			ReadField,
			ReadGrADS,
			ReadV5,
			ExtractScalar,
			ExtractVector,
			Interpolator,
			Slicer,
			Isosurface,
			Bounds
		}

		struct ModuleInfo
		{
			public string name;
			public int id;
		}

		public void SetupUI()
		{
			var UIManager = GameObject.Find("UIManager");
			if (UIManager == null)
			{
				disableUI = true;
				return;
			}

			var moduleInfo = GetModuleInfo();
			if (moduleInfo.id < 0) return;

			int moduleNum = UIManager.GetComponent<UIManager>().moduleCounter[moduleInfo.id];
			FixedModuleName = moduleInfo.name;

			if (moduleNum != 0)
			{
				FixedModuleName += " #" + moduleNum.ToString();
			}
			UIManager.GetComponent<UIManager>().moduleCounter[moduleInfo.id] = moduleNum + 1;
			UIPrefab = (GameObject)Resources.Load("Prefabs/UIPanels/" + moduleInfo.name);
			if (UIPrefab != null)
			{
				UIPanel = Instantiate(UIPrefab, Vector3.zero, Quaternion.identity);
				UIPanel.name = FixedModuleName;
				var paramChanger = UIManager.GetComponent<UIManager>().paramChanger;
				UIPanel.transform.SetParent(paramChanger.transform, false);
//				UIPanel.transform.SetParent(GameObject.Find("ParamChanger").transform, false);
				UIPanel.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
				UIPanel.GetComponent<UIPanel>().TargetModule = this.gameObject;
				UIPanel.SetActive(false);
			}

			var ModuleSelector = UIManager.GetComponent<UIManager>().moduleSelector;
//			var dropdown = ModuleSelector.Find("Dropdown").GetComponent<Dropdown>();
//			var ModuleSelector = GameObject.Find("ModuleSelector");
			var dropdown = ModuleSelector.transform.Find("Dropdown").GetComponent<Dropdown>();
			if (dropdown.options[0].text == "None")
			{
				dropdown.ClearOptions();
				if (UIPrefab != null)
				{
					UIPanel.SetActive(true);
				}
			}
			dropdown.options.Add(new Dropdown.OptionData { text = FixedModuleName });
			dropdown.RefreshShownValue();
		}

		ModuleInfo GetModuleInfo()
		{
			ModuleInfo info;
			info.name = "";
			info.id = -1;

			var moduleNum = Enum.GetNames(typeof(ModuleName)).Length;
			for (int i = 0; i < moduleNum; ++i)
			{
				var moduleName = Enum.GetName(typeof(ModuleName), i);
				if (this.gameObject.name.StartsWith(moduleName))
				{
					info.name = moduleName;
					info.id = i;
					return info;
				}
			}

			return info;
		}

		public void ResetUICore()
		{
			if (disableUI) return;
			if (UIPrefab == null) return;

			ResetUI();
		}

		public virtual void ResetUI()
		{
		}
	}
}