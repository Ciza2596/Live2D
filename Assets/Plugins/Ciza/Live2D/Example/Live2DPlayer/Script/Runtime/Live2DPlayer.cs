using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using UnityEngine;

public class Live2DPlayer : MonoBehaviour
{
	[SerializeField]
	protected bool _isLockedParameter;

	[SerializeField]
	protected string _parameterId = "ParamAngleX";

	[SerializeField]
	protected float _angle = 27;

	[SerializeField]
	protected string _state;

	[Range(0, 1)]
	[SerializeField]
	protected float _normalized;

	[Space]
	[SerializeField]
	protected CubismSortingMode _sortingMode = CubismSortingMode.BackToFrontZ;

	protected Animator Animator => GetComponentInChildren<Animator>();

	protected CubismModel CubismModel => GetComponentInChildren<CubismModel>();

	#region Unity

	protected virtual void OnEnable()
	{
		Initialize();
	}

	protected virtual void OnDisable()
	{
		Release();
	}

	protected virtual void Update()
	{
		SetTime(_state, _normalized);
	}

	#endregion


	public virtual void Initialize()
	{
		CubismModel.InitializeWithDisableAll(_sortingMode);
	}

	public virtual void Release()
	{
		CubismModel.Release();
	}


	public virtual void SetTime(string state, float normalized)
	{
		Animator.Play(state, 0, normalized);
		Animator.Update(0);

		if (_isLockedParameter)
			CubismModel.SetParameter(_parameterId, _angle);
		
		// foreach (var parameter in CubismModel.Parameters)
		// {
		// 	parameter.Value = 0;
		// }
		CubismModel.Refresh();
	}
}