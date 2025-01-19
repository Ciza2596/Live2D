using Live2D.Cubism.Framework;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using UnityEngine;

namespace Live2D.Cubism.Core
{
	public static class CubismExtension
	{
		public static void InitializeWithDisableAll(this CubismModel model, CubismSortingMode sortingMode = CubismSortingMode.BackToFrontZ)
		{
			foreach (var live2DBehaviour in model.GetComponentsInChildren<Behaviour>())
				live2DBehaviour.enabled = false;
			model.Initialize(sortingMode);
		}

		public static void Initialize(this CubismModel model, CubismSortingMode sortingMode)
		{
			var renderController = model.GetComponentInChildren<CubismRenderController>();
			model.OnDynamicDrawableData += renderController.OnDynamicDrawableData;
			renderController.TryInitializeRenderers();
			renderController.SortingMode = sortingMode;

			var maskController = model.GetComponentInChildren<CubismMaskController>();
			maskController?.MaskTexture?.AddSource(maskController);
			model.GetComponentInChildren<CubismUpdateController>()?.Refresh();
		}

		public static void Release(this CubismModel model)
		{
			var maskController = model.GetComponentInChildren<CubismMaskController>();
			maskController?.MaskTexture?.RemoveSource(maskController);

			model.OnDynamicDrawableData -= model.GetComponentInChildren<CubismRenderController>().OnDynamicDrawableData;
		}
		
		public static void SetSortingMode(this CubismModel model, CubismSortingMode sortingMode) =>
			model.GetComponentInChildren<CubismRenderController>().SortingMode = sortingMode;

		public static void OnUpdate(this CubismModel model)
		{
			model.ForceUpdateNow();
		}

		public static void OnLateUpdate(this CubismModel model)
		{
			model.GetComponentInChildren<CubismRenderController>().OnLateUpdate();
			model.GetComponentInChildren<CubismMaskController>()?.OnLateUpdate();
			model.GetComponentInChildren<CubismUpdateController>()?.LateUpdate();
		}

		public static void Refresh(this CubismModel model)
		{
			model.OnUpdate();
			model.OnLateUpdate();
		}
	}
}