using Live2D.Cubism.Framework;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using UnityEngine;
using UnityEngine.Profiling;

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
			
			model.OnEnable();
		}

		public static void Release(this CubismModel model)
		{
			model.OnDisable();
			
			var maskController = model.GetComponentInChildren<CubismMaskController>();
			maskController?.MaskTexture?.RemoveSource(maskController);
			model.OnDynamicDrawableData -= model.GetComponentInChildren<CubismRenderController>().OnDynamicDrawableData;
		}

		public static void OnUpdate(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.OnUpdate");
			model.ForceUpdateNow();
			Profiler.EndSample();
			
			// Profiler.BeginSample("Cubism.OnUpdate");
			// model.Update();
			// Profiler.EndSample();
		}

		public static void OnLateUpdate(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.OnLateUpdate");
			Profiler.BeginSample("RenderController.OnLateUpdate");
			model.GetComponentInChildren<CubismRenderController>().OnLateUpdate();
			Profiler.EndSample();
			Profiler.BeginSample("MaskController.OnLateUpdate");
			model.GetComponentInChildren<CubismMaskController>()?.OnLateUpdate();
			Profiler.EndSample();
			Profiler.BeginSample("UpdateController.OnLateUpdate");
			model.GetComponentInChildren<CubismUpdateController>()?.LateUpdate();
			Profiler.EndSample();
			Profiler.EndSample();
		}

		public static void Refresh(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.Refresh");
			model.OnUpdate();
			model.OnLateUpdate();
			Profiler.EndSample();
		}
	}
}