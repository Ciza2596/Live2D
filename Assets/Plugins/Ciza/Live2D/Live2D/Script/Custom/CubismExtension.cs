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
			model.GetComponentInChildren<CubismRenderController>().SortingMode = sortingMode;
			model.IsAttachedModelUpdate = false;

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

		public static void SetParameter(this CubismModel model, string id, float value)
		{
			var parameter = model.Parameters.FindById(id);
			if (parameter != null)
				parameter.Value = value;
		}

		public static void Tick(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.Tick");
			model.Update();
			Profiler.EndSample();
		}

		public static void ForceTick(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.ForceTick");
			model.ForceUpdateNow();
			Profiler.EndSample();
		}

		public static void LateTick(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.LateTick");
			foreach (var lateUpdatable in model.GetComponentsInChildren<ILateUpdatable>())
			{
				Profiler.BeginSample($"{lateUpdatable.GetType().Name}.OnLateTick");
				lateUpdatable.OnLateUpdate();
				Profiler.EndSample();
			}

			Profiler.EndSample();
		}

		public static void Refresh(this CubismModel model)
		{
			Profiler.BeginSample("Cubism.Refresh");
			model.ForceTick();
			model.LateTick();
			Profiler.EndSample();
		}
	}
}