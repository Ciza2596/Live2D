using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace Live2D.Cubism.Editor
{
	public static class Live2DModel3Reimporter
	{
		[MenuItem("Live2D/Ciza/Reimport All Model3")]
		public static void ReimportAllModel3FromMenu()
		{
			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				Debug.LogWarning("Live2DModel3Reimporter : Unity is compiling or updating. Try again after the editor is idle.");
				return;
			}

			var model3JsonPaths = Directory
				.GetFiles("Assets", "*.model3.json", SearchOption.AllDirectories)
				.Select(path => path.Replace('\\', '/'))
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToArray();

			if (model3JsonPaths.Length == 0)
			{
				Debug.Log("Live2DModel3Reimporter : No .model3.json assets found.");
				return;
			}

			try
			{
				AssetDatabase.StartAssetEditing();

				for (var i = 0; i < model3JsonPaths.Length; ++i)
				{
					var model3JsonPath = model3JsonPaths[i];
					var progress = (float)i / model3JsonPaths.Length;

					EditorUtility.DisplayProgressBar(
						"Reimport All Model3",
						model3JsonPath,
						progress);

					AssetDatabase.ImportAsset(model3JsonPath, ImportAssetOptions.ForceUpdate);
				}
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
				EditorUtility.ClearProgressBar();
			}

			AssetDatabase.SaveAssets();
			Debug.Log("Live2DModel3Reimporter : Reimported " + model3JsonPaths.Length + " .model3.json assets.");
		}
	}
}
