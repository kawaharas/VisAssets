using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

public class ItemListAttribute : PropertyAttribute
{
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ItemListAttribute))]
public class ItemListAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		/*
				string[] itemNames = GameTable.GetWeaponNames();
				int selectedIndex = System.Array.FindIndex(itemNames, (value) => value == property.stringValue);
				selectedIndex = Mathf.Clamp(selectedIndex, 0, itemNames.Length);
				selectedIndex = EditorGUI.Popup(position, selectedIndex, itemNames);
				property.stringValue = itemNames[selectedIndex];
		*/
	}
}
#endif // UNITY_EDITOR