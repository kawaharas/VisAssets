using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VIS
{
	[RequireComponent(typeof(Activation))]
	[RequireComponent(typeof(DataField))]

	public class ReadModuleTemplate : ModuleTemplate
	{
		[HideInInspector]
		public Activation activation;
		[HideInInspector]
		public DataField  df;
		[HideInInspector]
		public GameObject animator;
		[SerializeField]
		public bool loadAtStartup = false;

		public int current_step;

		void Awake()
		{
			activation = this.GetComponent<Activation>();
			if (activation == null)
			{
				activation = this.gameObject.AddComponent<Activation>();
			}
			activation.SetModuleType(ModuleType.READING);

			df = this.GetComponent<DataField>();
			if (df == null)
			{
				df = this.gameObject.AddComponent<DataField>();
			}
			df.dataType = DataField.DataType.RAW;

			current_step = 0;
			var modules = GameObject.FindGameObjectsWithTag("VisModule");
			for (int i = 0; i < modules.Length; i++)
			{
				if (modules[i].name == "Animator")
				{
					animator = modules[i];
					break;
				}
			}
//			animator = GameObject.Find("Animator");
			if (animator != null)
			{
				current_step = animator.GetComponent<Animator>().currentStep;
			}
		}

		void Start()
		{
			GetParameters();
			InitModule();
			SetupUI();

			if (loadAtStartup)
			{
				activation.SetParameterChanged(1);
			}
		}

		void Update()
		{
			CheckCurrentStep();

			if (activation.GetParameterChanged() == 1)
			{
				// turn off flag until data loading is complete
				df.dataLoaded = false;

				GetParameters();
				if (BodyFunc() == 1)
				{
					SetParentChangedIntoAllChildren();
				}
				else
				{
					Debug.Log("ERROR: in read module func");
				}
				activation.SetParameterChanged(0);
			}
		}

		public virtual void SetData(int step)
		{
		}

		public void InitAnimator()
		{
			var animator = GameObject.Find("Animator");
			if (animator != null)
			{
				if (animator.tag.Equals("VisModule"))
				{
					animator.GetComponent<Animator>().CheckMaximumSteps();
				}
			}
		}

		public void SetStep(int step)
		{
			if (step != current_step)
			{
				current_step = step;
				SetData(step);
				SetParentChangedIntoAllChildren();
			}
		}

		public void CheckCurrentStep()
		{
			if (animator != null)
			{
				var step = animator.GetComponent<Animator>().currentStep;
				SetStep(step);
			}
		}

		public virtual void InitModule()
		{
		}

		public virtual int BodyFunc()
		{
			return 0;
		}

		public virtual void GetParameters()
		{
		}

		public void SetParentChangedIntoAllChildren()
		{
			int child_num = this.gameObject.transform.childCount;

			for (int i = 0; i < child_num; i++)
			{
				Transform child = this.gameObject.transform.GetChild(i);
				if (child.GetComponent<Activation>())
				{
					Activation c = child.GetComponent<Activation>();
					c.SetParentChanged(1);
				}
			}
		}

		public void ParameterChanged()
		{
			activation.SetParameterChanged(1);
		}
	}
}