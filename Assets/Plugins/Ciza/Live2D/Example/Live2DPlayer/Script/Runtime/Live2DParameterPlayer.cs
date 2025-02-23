using Live2D.Cubism.Core;
using UnityEngine;

public class Live2DParameterPlayer : MonoBehaviour
{
	protected CubismModel CubismModel => GetComponentInChildren<CubismModel>();

	protected void LateUpdate()
	{
		CubismModel.SetParameter("ParamAngleX", 27);
	}
}