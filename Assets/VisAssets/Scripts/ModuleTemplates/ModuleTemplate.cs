using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisAssets
{
	public class ModuleTemplate : MonoBehaviour
	{
		[SerializeField, ReadOnly]
		public GameObject UIPanel;
		[SerializeField, ReadOnly]
		public string FixedModuleName;
		[SerializeField]
		private GameObject UIPrefab;
		bool disableUI = false;

		public enum ModuleType
		{
			READING,
			FILTERING,
			MAPPING,
			UNDEFINED
		}

		struct ModuleInfo
		{
			public string name;
			public int id;
		}

		public void SetupUI()
		{
//			var UIManager = GameObject.Find("UIManager");
			var UIManager = GameObject.FindAnyObjectByType<UIManager>();
			if (UIManager == null)
			{
				disableUI = true;
				return;
			}

			var moduleInfo = GetModuleInfo();
			if (moduleInfo.id < 0) return;

			int moduleNum = 0;
			var UIManagerComponent = UIManager.GetComponent<UIManager>();
			if (UIManagerComponent != null)
			{
				moduleNum = UIManagerComponent.moduleCounter[moduleInfo.id];
				FixedModuleName = moduleInfo.name;
				if (moduleNum != 0)
				{
					FixedModuleName += " #" + moduleNum.ToString();
				}
				UIManagerComponent.moduleCounter[moduleInfo.id] = moduleNum + 1;
			}

			if (UIPrefab != null)
			{
				UIPanel = Instantiate(UIPrefab, Vector3.zero, Quaternion.identity);
				UIPanel.name = FixedModuleName;
				var paramChanger = UIManager.GetComponent<UIManager>().paramChanger;
				UIPanel.transform.SetParent(paramChanger.transform, false);
				UIPanel.transform.localScale = Vector3.one;

				var UIPanelComponent = UIPanel.GetComponent<UIPanel>();
				if (UIPanelComponent != null)
				{
					UIPanelComponent.TargetModule = this.gameObject;
				}

				UIPanel.SetActive(false);
			}
			else
			{
				Debug.LogWarning($"{gameObject.name}: UI Prefab is not assigned.");
			}

			var ModuleSelector = UIManagerComponent.moduleSelector;
			if (ModuleSelector != null)
			{
				var dropdown = ModuleSelector.transform.Find("Dropdown");
				if (dropdown != null)
				{
					var dropdownComponent = dropdown.GetComponent<Dropdown>();
					if (dropdownComponent != null)
					{
						if (dropdownComponent.options[0].text == "None")
						{
							dropdownComponent.ClearOptions();
							if (UIPrefab != null)
							{
								UIPanel.SetActive(true);
							}
						}
						dropdownComponent.options.Add(new Dropdown.OptionData { text = FixedModuleName });
						dropdownComponent.RefreshShownValue();
					}
				}
			}
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