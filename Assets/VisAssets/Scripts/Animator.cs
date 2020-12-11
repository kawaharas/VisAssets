using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VIS
{
#if UNITY_EDITOR
	[CustomEditor(typeof(Animator))]
	public class AnimatorEditor : Editor
	{
		private int totalSteps;

		public override void OnInspectorGUI()
		{
			var animator = target as Animator;

			totalSteps = animator.totalSteps - 1;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.Space();
			animator.timeOut = EditorGUILayout.Slider("Interval:", animator.timeOut, 0, 1f);
			GUILayout.Space(5f);
			var stepStr = animator.currentStep.ToString() + " / " + totalSteps.ToString();
			EditorGUILayout.LabelField("Current Step:", stepStr);
			GUILayout.Space(8f);
			var buttonStr = "Play";
			if (animator.onPlay)
			{
				buttonStr = "Stop";
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("|<"))
			{
				animator.currentStep = 0;
			}
			if (GUILayout.Button("-1"))
			{
				animator.currentStep -= 1;
				if (animator.currentStep < 0)
				{
					animator.currentStep = animator.totalSteps - 1;
				}
			}
			if (GUILayout.Button(buttonStr))
			{
				animator.onPlay = !(animator.onPlay);
			}
			if (GUILayout.Button("+1"))
			{
				animator.currentStep += 1;
				if (animator.currentStep >= animator.totalSteps)
				{
					animator.currentStep = 0;
				}
			}
			if (GUILayout.Button(">|"))
			{
				animator.currentStep = animator.totalSteps - 1;
			}
			GUILayout.EndHorizontal();
			EditorGUILayout.Space();
			if (EditorGUI.EndChangeCheck())
			{
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

//	public class Animator : MonoBehaviour
	public class Animator : ModuleTemplate
	{
		[Range(0, 1f)]
		public  float timeOut = 0.5f;
		private float timeElapsed = 0.0f;
		public  int   currentStep = 0;
		public  int   totalSteps = 5;
		public  bool  onPlay = false;

		private void Start()
		{
			SetupUI();
		}

//		void Update()
		void FixedUpdate()
		{
			if (onPlay)
			{
				timeElapsed += Time.deltaTime;

				if (timeElapsed >= timeOut)
				{
					currentStep += 1;
					if (currentStep >= totalSteps)
					{
						currentStep = 0;
					}
					//					SetData(currentStep);
					timeElapsed = 0.0f;
				}
			}
		}
	}
}