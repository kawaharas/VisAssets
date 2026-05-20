#if UNITY_EDITOR
using UnityEditor;
using System.IO;

namespace VisAssets.Editor
{
	[InitializeOnLoad]
	public static class TMPAutoImporter
	{
		static TMPAutoImporter()
		{
			if (!Directory.Exists("Assets/TextMesh Pro"))
			{
				EditorApplication.delayCall += () =>
				{
					EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
				};
			}
		}
	}
}
#endif