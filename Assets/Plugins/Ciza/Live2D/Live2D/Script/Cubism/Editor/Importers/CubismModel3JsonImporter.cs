/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */


using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Expression;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Framework.Pose;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace Live2D.Cubism.Editor.Importers
{
    /// <summary>
    /// Handles importing of Cubism models.
    /// </summary>
    [Serializable]
    public sealed class CubismModel3JsonImporter : CubismImporterBase
    {
        /// <summary>
        /// <see cref="Model3Json"/> backing field.
        /// </summary>
        [NonSerialized] private CubismModel3Json _model3Json;

        /// <summary>
        ///<see cref="CubismModel3Json"/> asset.
        /// </summary>
        public CubismModel3Json Model3Json
        {
            get
            {
                if (_model3Json == null)
                {
                    _model3Json = CubismModel3Json.LoadAtPath(AssetPath);
                }

#if UNITY_2018_3_OR_NEWER
                if (_modelPrefab == null)
                {
                    _modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath.Replace(".model3.json", ".prefab"));
                    if(_modelPrefab != null)
                    {
                        _modelPrefabGuid = AssetGuid.GetGuid(_modelPrefab);
                    }
                }
#endif

                return _model3Json;
            }
        }


        /// <summary>
        /// Guid of model prefab.
        /// </summary>
        [SerializeField] private string _modelPrefabGuid;

        /// <summary>
        /// <see cref="ModelPrefab"/> backing field.
        /// </summary>
        [NonSerialized] private GameObject _modelPrefab;

        /// <summary>
        /// Prefab of model.
        /// </summary>
        private GameObject ModelPrefab
        {
            get
            {
                if (_modelPrefab == null)
                {
                    _modelPrefab = AssetGuid.LoadAsset<GameObject>(_modelPrefabGuid);
                }


                return _modelPrefab;
            }
            set
            {
                _modelPrefab = value;
                _modelPrefabGuid = AssetGuid.GetGuid(value);
            }
        }


        /// <summary>
        /// Guid of moc.
        /// </summary>
        [SerializeField]
        private string _mocAssetGuid;

        /// <summary>
        /// <see cref="MocAsset"/> backing field.
        /// </summary>
        [NonSerialized]
        private CubismMoc _mocAsset;

        /// <summary>
        /// Moc asset.
        /// </summary>
        private CubismMoc MocAsset
        {
            get
            {
                if (_mocAsset == null)
                {
                    _mocAsset = AssetGuid.LoadAsset<CubismMoc>(_mocAssetGuid);
                }


                return _mocAsset;
            }
            set
            {
                _mocAsset = value;
                _mocAssetGuid = AssetGuid.GetGuid(value);
            }
        }


        /// <summary>
        /// Should import as original workflow.
        /// </summary>
        private bool ShouldImportAsOriginalWorkflow
        {
            get
            {
                return CubismUnityEditorMenu.ShouldImportAsOriginalWorkflow;
            }
        }

        #region Unity Event Handling

        /// <summary>
        /// Registers importer.
        /// </summary>
        [InitializeOnLoadMethod]
        // ReSharper disable once UnusedMember.Local
        private static void RegisterImporter()
        {
            CubismImporter.RegisterImporter<CubismModel3JsonImporter>(".model3.json");
        }

        #endregion

        #region CubismImporterBase

        /// <summary>
        /// Imports the corresponding asset.
        /// </summary>
        public override void Import()
        {
            var isImporterDirty = false;


            // Instantiate model source and model.
            var model = Model3Json.ToModel(CubismImporter.OnPickMaterial, CubismImporter.OnPickTexture, ShouldImportAsOriginalWorkflow);

            if (model == null)
            {
                return;
            }

            var assetPath = AssetPath.Replace(".model3.json", "");
            var modelName = Path.GetFileName(assetPath).Replace(".model3.json", "");

            var moc = model.Moc;
            moc.name = modelName;

            // Create moc asset.
            if (MocAsset == null)
            {
                AssetDatabase.CreateAsset(moc, $"{assetPath}.asset");


                MocAsset = moc;


                isImporterDirty = true;
            }


            // Create model prefab.
            if (ModelPrefab == null)
            {
                // Trigger event.
                CubismImporter.SendModelImportEvent(this, model);


                foreach (var texture in Model3Json.Textures)
                {
                    CubismImporter.SendModelTextureImportEvent(this, model, texture);
                }

                var modelMaskTexture = ScriptableObject.CreateInstance<CubismMaskTexture>();
                modelMaskTexture.name = model.name + "MaskTexture";

                var filePath = string.Format("{0}/{1}.asset", Path.GetDirectoryName(AssetPath), modelMaskTexture.name);

                if (!File.Exists(filePath))
                {
                    AssetDatabase.CreateAsset(modelMaskTexture, filePath);
                }

                ApplyArtMeshModifiers(model, assetPath);

                // Create prefab and trigger saving of changes.
#if UNITY_2018_3_OR_NEWER
                ModelPrefab = PrefabUtility.SaveAsPrefabAsset(model.gameObject, $"{assetPath}.prefab");
#else
                ModelPrefab = PrefabUtility.CreatePrefab($"{assetPath}.prefab", model.gameObject);
#endif

                isImporterDirty = true;
            }


            // Update model prefab.
            else
            {
                var cubismModel = ModelPrefab.FindCubismModel();
                if (cubismModel.Moc == null)
                {
                    CubismModel.ResetMocReference(cubismModel,
                        AssetDatabase.LoadAssetAtPath<CubismMoc>(
                            $"{assetPath}.asset"));
                }


                // Copy all user data over from previous model.
                var source = Object.Instantiate(ModelPrefab).FindCubismModel();


                CopyUserData(source, model);
                Object.DestroyImmediate(source.gameObject, true);


                // Trigger events.
                CubismImporter.SendModelImportEvent(this, model);


                foreach (var texture in Model3Json.Textures)
                {
                    CubismImporter.SendModelTextureImportEvent(this, model, texture);
                }


                // Reset moc reference.
                CubismModel.ResetMocReference(model, MocAsset);

                // Keep layer value.
                model.gameObject.layer = ModelPrefab.layer;

                ApplyArtMeshModifiers(model, assetPath);

                // Replace prefab.
#if UNITY_2018_3_OR_NEWER
                ModelPrefab = PrefabUtility.SaveAsPrefabAsset(model.gameObject, $"{assetPath}.prefab");
#else
                ModelPrefab = PrefabUtility.ReplacePrefab(model.gameObject, ModelPrefab, ReplacePrefabOptions.ConnectToPrefab);
#endif

                // Log event.
                CubismImporter.LogReimport(AssetPath, AssetDatabase.GUIDToAssetPath(_modelPrefabGuid));
            }


            // Clean up.
            Object.DestroyImmediate(model.gameObject, true);


            // Update moc asset.
            if (MocAsset != null)
            {
                EditorUtility.CopySerialized(moc, MocAsset);


                // Revive by force to make instance using the new Moc.
                CubismMoc.ResetUnmanagedMoc(MocAsset);


                EditorUtility.SetDirty(MocAsset);
            }

            // Save state and assets.
            if (isImporterDirty)
            {
                Save();
            }
            else
            {
                AssetDatabase.SaveAssets();
            }
        }

        #endregion

        private struct ArtMeshModifier
        {
            public ArtMeshModifier(Color tintMultiplier, bool isUseGammaInLinear)
            {
                TintMultiplier = tintMultiplier;
                IsUseGammaInLinear = isUseGammaInLinear;
            }

            public Color TintMultiplier { get; private set; }

            public bool IsUseGammaInLinear { get; private set; }
        }

        private static void ApplyArtMeshModifiers(CubismModel model, string assetPath)
        {
            var modifierPath = GetArtMeshModifierPath(assetPath);

            EnsureArtMeshModifierTemplate(modifierPath);

            var modifiers = LoadArtMeshModifiers(modifierPath);

            if (model.Drawables == null)
            {
                return;
            }

            var renderers = model.Drawables.GetComponentsMany<CubismRenderer>();

            if (renderers == null)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; ++i)
            {
                renderers[i].TintMultiplier = Color.white;
                renderers[i].IsUseGammaInLinear = false;
            }

            if (modifiers.Count < 1)
            {
                return;
            }

            var matchedNames = new HashSet<string>();

            for (var i = 0; i < renderers.Length; ++i)
            {
                var renderer = renderers[i];
                ArtMeshModifier modifier;

                if (!modifiers.TryGetValue(renderer.gameObject.name, out modifier))
                {
                    continue;
                }

                renderer.TintMultiplier = modifier.TintMultiplier;
                renderer.IsUseGammaInLinear = modifier.IsUseGammaInLinear;
                EditorUtility.SetDirty(renderer);
                matchedNames.Add(renderer.gameObject.name);
            }

            foreach (var artMeshName in modifiers.Keys)
            {
                if (!matchedNames.Contains(artMeshName))
                {
                    Debug.LogWarningFormat("[Cubism] ArtMesh modifier \"{0}\" in \"{1}\" did not match any generated ArtMesh.", artMeshName, modifierPath);
                }
            }
        }

        private static string GetArtMeshModifierPath(string assetPath)
        {
            var modelFolder = Path.GetDirectoryName(assetPath);
            var parentFolder = string.IsNullOrEmpty(modelFolder)
                ? string.Empty
                : Path.GetDirectoryName(modelFolder);
            var outputFolder = string.IsNullOrEmpty(parentFolder)
                ? modelFolder
                : parentFolder;
            var modelName = Path.GetFileName(assetPath);

            if (string.IsNullOrEmpty(outputFolder))
            {
                return $"{modelName}.ArtMeshModifier.txt";
            }

            return $"{outputFolder}/{modelName}.ArtMeshModifier.txt".Replace('\\', '/');
        }

        private static void EnsureArtMeshModifierTemplate(string modifierPath)
        {
            if (File.Exists(modifierPath))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("; ArtMeshName,TintMultiplier,IsUseGammaInLinear");
            builder.AppendLine("; Only list ArtMeshes that need a modifier.");
            builder.AppendLine("; Format: RRGGBBAlpha, for example FFFFFF255 or FFFFFF128.");
            builder.AppendLine("; Example:");
            builder.AppendLine("; ArtMesh, FFFFFF255, true");

            File.WriteAllText(modifierPath, builder.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(modifierPath);
        }

        private static Dictionary<string, ArtMeshModifier> LoadArtMeshModifiers(string modifierPath)
        {
            var modifiers = new Dictionary<string, ArtMeshModifier>();
            var lines = File.ReadAllLines(modifierPath);

            for (var i = 0; i < lines.Length; ++i)
            {
                var line = lines[i].Trim().TrimStart('\uFEFF').Trim();

                if (string.IsNullOrEmpty(line) || IsArtMeshModifierComment(line))
                {
                    continue;
                }

                var values = line.Split(',');

                if (values.Length != 2 && values.Length != 3)
                {
                    Debug.LogWarningFormat("[Cubism] Malformed ArtMesh modifier at \"{0}\" line {1}: \"{2}\".", modifierPath, i + 1, lines[i]);
                    continue;
                }

                var artMeshName = values[0].Trim();

                if (string.IsNullOrEmpty(artMeshName))
                {
                    Debug.LogWarningFormat("[Cubism] Missing ArtMesh name in modifier at \"{0}\" line {1}.", modifierPath, i + 1);
                    continue;
                }

                Color tintMultiplier;

                if (!TryParseTintMultiplier(values[1].Trim(), out tintMultiplier))
                {
                    Debug.LogWarningFormat("[Cubism] Malformed TintMultiplier value at \"{0}\" line {1}: \"{2}\".", modifierPath, i + 1, values[1].Trim());
                    continue;
                }

                var isUseGammaInLinear = false;

                if (values.Length == 3 && !bool.TryParse(values[2].Trim(), out isUseGammaInLinear))
                {
                    Debug.LogWarningFormat("[Cubism] Malformed IsUseGammaInLinear value at \"{0}\" line {1}: \"{2}\".", modifierPath, i + 1, values[2].Trim());
                    continue;
                }

                modifiers[artMeshName] = new ArtMeshModifier(tintMultiplier, isUseGammaInLinear);
            }

            return modifiers;
        }

        private static bool TryParseTintMultiplier(string value, out Color tintMultiplier)
        {
            tintMultiplier = Color.white;

            if (value.StartsWith("#"))
            {
                value = value.Substring(1);
            }

            if (value.Length <= 6)
            {
                return false;
            }

            var rgbHex = value.Substring(0, 6);
            var alphaValue = value.Substring(6);

            if (!ColorUtility.TryParseHtmlString("#" + rgbHex, out tintMultiplier))
            {
                return false;
            }

            int alpha;

            if (!int.TryParse(alphaValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out alpha))
            {
                return false;
            }

            if (alpha < 0 || alpha > 255)
            {
                return false;
            }

            tintMultiplier.a = alpha / 255f;

            return true;
        }

        private static bool IsArtMeshModifierComment(string line)
        {
            return line.StartsWith(";") || line.StartsWith("#");
        }

        private static void CopyUserData(CubismModel source, CubismModel destination, bool copyComponentsOnly = false)
        {
            // Give parameters, parts, and drawables special treatment.
            CopyUserData(source.Parameters, destination.Parameters, copyComponentsOnly);
            CopyUserData(source.Parts, destination.Parts, copyComponentsOnly);
            CopyUserData(source.Drawables, destination.Drawables, copyComponentsOnly);


            // Copy components.
            foreach (var sourceComponent in source.GetComponents(typeof(Component)))
            {
                // Skip non-movable components.
                if (!sourceComponent.MoveOnCubismReimport(copyComponentsOnly))
                {
                    continue;
                }

                // skip copy original workflow component.
                if(sourceComponent.GetType() == typeof(CubismUpdateController)
                || sourceComponent.GetType() == typeof(CubismFadeController)
                || sourceComponent.GetType() == typeof(CubismExpressionController)
                || sourceComponent.GetType() == typeof(CubismPoseController)
                || sourceComponent.GetType() == typeof(CubismParameterStore))
                {
                    continue;
                }

                // Copy component.
                var destinationComponent = destination.GetOrAddComponent(sourceComponent.GetType());


                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }
        }


        private static void CopyUserData<T>(T[] source, T[] destination, bool copyComponentsOnly) where T : MonoBehaviour
        {
            foreach (var destinationT in destination)
            {
                var sourceT = source.FirstOrDefault(p => p.name == destinationT.name);


                // Skip removed parameters.
                if (sourceT == null)
                {
                    continue;
                }


                // Copy any children.
                foreach (var child in sourceT.transform
                    .GetComponentsInChildren<Transform>()
                    .Where(t => t != sourceT.transform)
                    .Select(t => t.gameObject))
                {
                    Object.Instantiate(child, destinationT.transform);
                }


                // Copy components.
                foreach (var sourceComponent in sourceT.GetComponents(typeof(Component)))
                {
                    // Skip non-movable components.
                    if (!sourceComponent.MoveOnCubismReimport(copyComponentsOnly))
                    {
                        continue;
                    }


                    // Copy component.
                    var destinationComponent = destinationT.GetOrAddComponent(sourceComponent.GetType());


                    EditorUtility.CopySerialized(sourceComponent, destinationComponent);
                }
            }
        }
    }
}
