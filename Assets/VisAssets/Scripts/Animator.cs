using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
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
		private int maximumSteps;

		public override void OnInspectorGUI()
		{
			var animator = target as Animator;

			maximumSteps = animator.maximumSteps - 1;

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.Space();
			animator.timeOut = EditorGUILayout.Slider("Interval:", animator.timeOut, 0, 1f);
			GUILayout.Space(5f);
			var stepStr = animator.currentStep.ToString() + " / " + maximumSteps.ToString();
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
					animator.currentStep = animator.maximumSteps - 1;
				}
			}
			if (GUILayout.Button(buttonStr))
			{
				animator.onPlay = !(animator.onPlay);
			}
			if (GUILayout.Button("+1"))
			{
				animator.currentStep += 1;
				if (animator.currentStep >= animator.maximumSteps)
				{
					animator.currentStep = 0;
				}
			}
			if (GUILayout.Button(">|"))
			{
				animator.currentStep = animator.maximumSteps - 1;
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

	public class Animator : ModuleTemplate
	{
		[Range(0, 1f)]
		public  float timeOut = 0.5f;
		private float timeElapsed = 0.0f;
		public  int   currentStep = 0;
		public  int   maximumSteps = 1;
		public  bool  onPlay = false;

		private void Start()
		{
			SetupUI();
		}

		void FixedUpdate()
		{
			if (onPlay)
			{
				timeElapsed += Time.deltaTime;

				if (timeElapsed >= timeOut)
				{
					currentStep += 1;
					if (currentStep >= maximumSteps)
					{
						currentStep = 0;
					}
					//					SetData(currentStep);
					timeElapsed = 0.0f;
				}
			}
		}

		public void CheckMaximumSteps()
		{
			GameObject[] gos = GameObject.FindGameObjectsWithTag("VisModule");
			for (int i = 0; i < gos.Length; i++)
			{
				var activation = gos[i].GetComponent<Activation>();
				if (activation != null)
				{
					if (activation.moduleType == ModuleTemplate.ModuleType.READING)
					{
						if (gos[i].GetComponent<DataField>() != null)
						{
							if (gos[i].GetComponent<DataField>().elements != null)
							{
								for (int n = 0; n < gos[i].GetComponent<DataField>().elements.Length; n++)
								{
									maximumSteps = Math.Max(maximumSteps, gos[i].GetComponent<DataField>().elements[n].steps);
								}
							}
						}
					}
				}
			}
		}

		public void TogglePlayState()
		{
			onPlay = !onPlay;
		}

		public void SetTimeOut(float value)
		{
			timeOut = value;
		}
	}
}