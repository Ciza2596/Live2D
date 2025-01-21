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

			model.GetComponentInChildren<Animator>().enabled = true;
			model.Initialize(sortingMode);
		}

		public static void Initialize(this CubismModel model, CubismSortingMode sortingMode)
		{
			model.GetComponentInChildren<CubismRenderController>().SortingMode = sortingMode;
			//model.IsAttachedModelUpdate = false;

			// Awake
			foreach (var awakable in model.GetComponentsInChildren<IAwakable>())
				awakable.OnAwake();

			// OnEnable
			foreach (var enable in model.GetComponentsInChildren<IEnable>())
				enable.Enable();

			// Reset
			foreach (var resetable in model.GetComponentsInChildren<IResetable>())
				resetable.OnReset();

			// Start
			foreach (var startable in model.GetComponentsInChildren<IStartable>())
				startable.OnStart();

			model.OnEnable();
		}

		public static void Release(this CubismModel model)
		{
			model.OnDisable();

			// Disable
			foreach (var disable in model.GetComponentsInChildren<IDisable>())
				disable.Disable();
		}

		public static void OnUpdate(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.OnUpdate");
			model.ForceUpdateNow();
			Profiler.EndSample();
		}

		public static void OnLateUpdate(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.OnLateUpdate");
			foreach (var lateUpdatable in model.GetComponentsInChildren<ILateUpdatable>())
			{
				Profiler.BeginSample($"{lateUpdatable.GetType().Name}.OnLateUpdate");
				lateUpdatable.OnLateUpdate();
				Profiler.EndSample();
			}

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