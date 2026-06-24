/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */


using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Rendering.Masking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace Live2D.Cubism.Editor
{
	/// <summary>
	/// Repairs generated Live2D prefab references that Unity may clear during import.
	/// </summary>
	[InitializeOnLoad]
	public sealed class Live2DPrefabReferenceRepair : AssetPostprocessor
	{
		private const string StartupRepairSessionKey = "Ciza.Live2D.PrefabReferenceRepair.StartupDone";

		private static readonly HashSet<string> QueuedPrefabPaths = new HashSet<string>();

		private static bool IsRepairQueued { get; set; }

		private static bool IsRepairRunning { get; set; }


		static Live2DPrefabReferenceRepair()
		{
			EditorApplication.delayCall += QueueStartupPrefabReferenceRepair;
		}


		/// <summary>
		/// Repairs all generated Live2D prefab controller references manually.
		/// </summary>
		[MenuItem("Live2D/Ciza/Repair Prefab Controller References")]
		public static void RepairAllPrefabReferencesFromMenu()
		{
			QueuePrefabReferenceRepair(FindAllGeneratedPrefabPaths());
		}


		private static void QueueStartupPrefabReferenceRepair()
		{
			if (SessionState.GetBool(StartupRepairSessionKey, false))
			{
				return;
			}

			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				EditorApplication.delayCall += QueueStartupPrefabReferenceRepair;
				return;
			}

			SessionState.SetBool(StartupRepairSessionKey, true);
			QueuePrefabReferenceRepair(FindAllGeneratedPrefabPaths());
		}


		private static void OnPostprocessAllAssets(
			string[] importedAssetPaths,
			string[] deletedAssetPaths,
			string[] movedAssetPaths,
			string[] movedFromAssetPaths)
		{
			if (IsRepairRunning)
			{
				return;
			}

			var changedAssetPaths = importedAssetPaths.Concat(movedAssetPaths).ToArray();
			var prefabPaths = changedAssetPaths
				.Where(assetPath => assetPath.EndsWith(".prefab", StringComparison.Ordinal))
				.Where(IsGeneratedModelPrefabPath)
				.Concat(changedAssetPaths
					.Where(assetPath => assetPath.EndsWith(".model3.json", StringComparison.Ordinal))
					.Select(GetPrefabPathFromModel3JsonPath))
				.Distinct()
				.ToArray();

			QueuePrefabReferenceRepair(prefabPaths);
		}


		private static void QueuePrefabReferenceRepair(IEnumerable<string> prefabPaths)
		{
			foreach (var prefabPath in prefabPaths)
			{
				if (!string.IsNullOrEmpty(prefabPath))
				{
					QueuedPrefabPaths.Add(prefabPath);
				}
			}

			if (QueuedPrefabPaths.Count == 0 || IsRepairQueued)
			{
				return;
			}

			IsRepairQueued = true;
			EditorApplication.delayCall += RepairQueuedPrefabReferences;
		}


		private static void RepairQueuedPrefabReferences()
		{
			IsRepairQueued = false;

			if (IsRepairRunning)
			{
				QueuePrefabReferenceRepair(QueuedPrefabPaths.ToArray());
				return;
			}

			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				QueuePrefabReferenceRepair(QueuedPrefabPaths.ToArray());
				return;
			}

			var prefabPaths = QueuedPrefabPaths
				.OrderBy(prefabPath => prefabPath)
				.ToArray();

			QueuedPrefabPaths.Clear();
			IsRepairRunning = true;

			try
			{
				foreach (var prefabPath in prefabPaths)
				{
					if (!File.Exists(ToAbsolutePath(prefabPath)))
					{
						continue;
					}

					RepairPrefabReferences(prefabPath);
				}

				AssetDatabase.SaveAssets();
			}
			finally
			{
				IsRepairRunning = false;
			}
		}


		private static void RepairPrefabReferences(string prefabPath)
		{
			var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
			var hasChanges = false;

			try
			{
				var maskControllers = prefabRoot.GetComponentsInChildren<CubismMaskController>(true);
				var fadeControllers = prefabRoot.GetComponentsInChildren<CubismFadeController>(true);

				if (maskControllers.Length == 0 && fadeControllers.Length == 0)
				{
					return;
				}

				var modelName = prefabRoot.name;
				var prefabFolder = GetAssetFolder(prefabPath);
				var searchedMaskLocations = new List<string>();
				var searchedFadeLocations = new List<string>();
				var maskTexture = FindMaskTexture(modelName, prefabFolder, searchedMaskLocations);
				var fadeMotionList = FindFadeMotionList(modelName, prefabFolder, searchedFadeLocations);

				if (maskControllers.Length > 0 && maskTexture == null)
				{
					Debug.LogWarning("Live2DPrefabReferenceRepair : MaskTexture not found for prefab " + prefabPath + ". Searched: " + string.Join(", ", searchedMaskLocations.ToArray()));
				}

				foreach (var maskController in maskControllers)
				{
					if (maskTexture == null)
					{
						continue;
					}

					if (maskController.MaskTexture != maskTexture)
					{
						maskController.MaskTexture = maskTexture;
						EditorUtility.SetDirty(maskController);
						hasChanges = true;
					}
				}

				if (fadeControllers.Length > 0 && fadeMotionList == null)
				{
					Debug.LogWarning("Live2DPrefabReferenceRepair : .fadeMotionList not found for prefab " + prefabPath + ". Searched: " + string.Join(", ", searchedFadeLocations.ToArray()));
				}

				foreach (var fadeController in fadeControllers)
				{
					if (fadeMotionList == null)
					{
						continue;
					}

					if (fadeController.CubismFadeMotionList != fadeMotionList)
					{
						fadeController.CubismFadeMotionList = fadeMotionList;
						fadeController.Refresh();
						EditorUtility.SetDirty(fadeController);
						hasChanges = true;
					}
				}

				if (!hasChanges)
				{
					return;
				}

				PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
				Debug.Log("Live2DPrefabReferenceRepair : Repaired prefab controller references. " + prefabPath);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}


		private static CubismMaskTexture FindMaskTexture(string modelName, string prefabFolder, ICollection<string> searchedLocations)
		{
			var parentFolder = GetParentAssetFolder(prefabFolder);
			var expectedName = modelName + "MaskTexture";

			return FindMaskTextureInFolder(prefabFolder, expectedName, searchedLocations, true)
				?? FindMaskTextureInFolder(parentFolder, expectedName, searchedLocations, true)
				?? FindMaskTextureInFolder(prefabFolder, expectedName, searchedLocations, false)
				?? FindMaskTextureInFolder(parentFolder, expectedName, searchedLocations, false);
		}


		private static CubismFadeMotionList FindFadeMotionList(string modelName, string prefabFolder, ICollection<string> searchedLocations)
		{
			var parentFolder = GetParentAssetFolder(prefabFolder);
			var expectedName = modelName + ".fadeMotionList";

			return FindFadeMotionListInFolder(prefabFolder, expectedName, searchedLocations, true)
				?? FindFadeMotionListInFolder(parentFolder, expectedName, searchedLocations, true)
				?? FindFadeMotionListInFolder(prefabFolder, expectedName, searchedLocations, false)
				?? FindFadeMotionListInFolder(parentFolder, expectedName, searchedLocations, false);
		}


		private static CubismMaskTexture FindMaskTextureInFolder(string folder, string expectedName, ICollection<string> searchedLocations, bool exactNameOnly)
		{
			foreach (var assetPath in GetAssetPathsInFolder(folder, searchedLocations))
			{
				var maskTexture = AssetDatabase.LoadAssetAtPath<CubismMaskTexture>(assetPath);

				if (maskTexture == null)
				{
					continue;
				}

				if (exactNameOnly && maskTexture.name != expectedName && Path.GetFileNameWithoutExtension(assetPath) != expectedName)
				{
					continue;
				}

				return maskTexture;
			}

			return null;
		}


		private static CubismFadeMotionList FindFadeMotionListInFolder(string folder, string expectedName, ICollection<string> searchedLocations, bool exactNameOnly)
		{
			foreach (var assetPath in GetAssetPathsInFolder(folder, searchedLocations))
			{
				var fadeMotionList = AssetDatabase.LoadAssetAtPath<CubismFadeMotionList>(assetPath);

				if (fadeMotionList == null)
				{
					continue;
				}

				var assetName = Path.GetFileNameWithoutExtension(assetPath);

				if (exactNameOnly && fadeMotionList.name != expectedName && assetName != expectedName)
				{
					continue;
				}

				return fadeMotionList;
			}

			return null;
		}


		private static IEnumerable<string> GetAssetPathsInFolder(string folder, ICollection<string> searchedLocations)
		{
			if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
			{
				return new string[0];
			}

			searchedLocations.Add(folder);

			var absoluteFolder = ToAbsolutePath(folder);

			if (!Directory.Exists(absoluteFolder))
			{
				return new string[0];
			}

			return Directory.GetFiles(absoluteFolder, "*.asset", SearchOption.TopDirectoryOnly)
				.Select(ToAssetPath)
				.OrderBy(assetPath => assetPath)
				.ToArray();
		}


		private static string[] FindAllGeneratedPrefabPaths()
		{
			return AssetDatabase.FindAssets("t:Prefab")
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(assetPath => assetPath.EndsWith(".prefab", StringComparison.Ordinal))
				.Where(IsGeneratedModelPrefabPath)
				.OrderBy(assetPath => assetPath)
				.ToArray();
		}


		private static bool IsGeneratedModelPrefabPath(string prefabPath)
		{
			return File.Exists(ToAbsolutePath(GetModel3JsonPathFromPrefabPath(prefabPath)));
		}


		private static string GetPrefabPathFromModel3JsonPath(string model3JsonPath)
		{
			return model3JsonPath.Replace(".model3.json", ".prefab");
		}


		private static string GetModel3JsonPathFromPrefabPath(string prefabPath)
		{
			return prefabPath.Replace(".prefab", ".model3.json");
		}


		private static string GetAssetFolder(string assetPath)
		{
			return Path.GetDirectoryName(assetPath).Replace("\\", "/");
		}


		private static string GetParentAssetFolder(string assetFolder)
		{
			if (string.IsNullOrEmpty(assetFolder) || assetFolder == "Assets")
			{
				return null;
			}

			var parentFolder = Path.GetDirectoryName(assetFolder);

			return string.IsNullOrEmpty(parentFolder)
				? null
				: parentFolder.Replace("\\", "/");
		}


		private static string ToAssetPath(string absolutePath)
		{
			var projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/") + "/";
			return absolutePath.Replace("\\", "/").Replace(projectRoot, "");
		}


		private static string ToAbsolutePath(string assetPath)
		{
			return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
		}
	}
}
