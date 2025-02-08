using System;
using UnityEngine;

public class MonoAnimator : MonoBehaviour
{
	protected Animator Animator => GetComponent<Animator>();

	protected void OnEnable()
	{
		Animator.enabled = false;
	}

	protected void Update()
	{
		Animator.Update(Time.deltaTime);
	}
}