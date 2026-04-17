using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets
{
#if UNITY_EDITOR
	[CustomEditor(typeof(ModuleTemplate), true)]
	public class ModuleTemplateEditor : Editor
	{
		protected SerializedProperty uiPrefab;

		protected virtual void OnEnable()
		{
			uiPrefab = serializedObject.FindProperty("UIPrefab");
		}

		protected void DrawProp(SerializedProperty prop, string label = null, float spaceAfter = 0f, int indent = 0, bool isDisabled = false)
		{
			if (prop == null) return;

			if (isDisabled)
			{
				EditorGUI.BeginDisabledGroup(true);
			}

			if (indent > 0)
			{
				EditorGUI.indentLevel += indent;
			}

			if (string.IsNullOrEmpty(label))
			{
				EditorGUILayout.PropertyField(prop);
			}
			else
			{
				EditorGUILayout.PropertyField(prop, new GUIContent(label));
			}

			if (indent > 0)
			{
				EditorGUI.indentLevel -= indent;
			}

			if (isDisabled)
			{
				EditorGUI.EndDisabledGroup();
			}

			if (spaceAfter > 0f)
			{
				GUILayout.Space(spaceAfter);
			}
		}

		protected void DrawToggle(SerializedProperty prop, string label, float spaceAfter = 0f, int indent = 0, bool isDisabled = false)
		{
			if (prop == null) return;

			if (isDisabled)
			{
				EditorGUI.BeginDisabledGroup(true);
			}

			if (indent > 0)
			{
				EditorGUI.indentLevel += indent;
			}

			prop.boolValue = EditorGUILayout.ToggleLeft(label, prop.boolValue);

			if (indent > 0)
			{
				EditorGUI.indentLevel -= indent;
			}

			if (isDisabled)
			{
				EditorGUI.EndDisabledGroup();
			}

			if (spaceAfter > 0f)
			{
				GUILayout.Space(spaceAfter);
			}
		}
	}
#endif

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

#if UNITY_EDITOR
		/// <summary>
		/// Automatically assigns the required tag to the GameObject when attached or reset.
		/// </summary>
		protected virtual void Reset()
		{
			// Automatically assign the "VisModule" tag.
			this.gameObject.tag = "VisModule";
			try
			{
				this.gameObject.tag = "VisModule";
			}
			catch (System.Exception)
			{
				Debug.LogWarning("[VisAssets] Tag 'VisModule' is not defined in Tag Manager. Please add it to avoid potential issues.");
			}
		}
#endif

		public void SetupUI()
		{
			var UIManager = GameObject.FindAnyObjectByType<UIManager>();
			if (UIManager == null)
			{
				disableUI = true;
				return;
			}

			string moduleName = this.GetType().Name;
			FixedModuleName = moduleName;
			int moduleNum = 0;

			var UIManagerComponent = UIManager.GetComponent<UIManager>();
			if (UIManagerComponent != null)
			{
				if (!UIManagerComponent.moduleCounter.ContainsKey(moduleName))
				{
					UIManagerComponent.moduleCounter[moduleName] = 0;
				}

				moduleNum = UIManagerComponent.moduleCounter[moduleName];

				if (moduleNum != 0)
				{
					FixedModuleName += " #" + moduleNum.ToString();
				}

				UIManagerComponent.moduleCounter[moduleName] = moduleNum + 1;
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
						if (dropdownComponent.options.Count > 0 && dropdownComponent.options[0].text == "None")
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