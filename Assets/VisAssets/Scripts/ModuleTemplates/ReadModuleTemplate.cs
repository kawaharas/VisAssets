using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisAssets
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
		public bool centering;
		public bool autoResize;

		public int currentStep;

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

			currentStep = 0;
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
				currentStep = animator.GetComponent<Animator>().currentStep;
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
			if (step != currentStep)
			{
				currentStep = step;
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

		public void Centering(bool normalize = false)
		{
			// calculate offsets and scale for normalizing
			if (!df.dataLoaded) return;

			float[] offset = new float[3];
			float[] min = new float[3];
			float[] max = new float[3];
			float maxDist = float.MinValue;

			for (int i = 0; i < 3; i++)
			{
				min[i] = float.MaxValue;
				max[i] = float.MinValue;
			}

			for (int n = 0; n < df.elements.Length; n++)
			{
				DataElement element = df.elements[n];

				for (int i = 0; i < 3; i++)
				{
					min[i] = Mathf.Min(min[i], element.coords[i][0]);
					max[i] = Mathf.Max(max[i], element.coords[i][element.dims[i] - 1]);
					maxDist = Mathf.Max(maxDist, max[i] - min[i]);
				}
			}

			for (int i = 0; i < 3; i++)
			{
				offset[i] = min[i] + (max[i] - min[i]) / 2f;
			}

			foreach (Transform child in transform)
			{
				child.gameObject.transform.localPosition =
					new Vector3(-offset[0], -offset[1], -offset[2]);
			}

			if (normalize)
			{
				float scale = 1f / maxDist * 10f;
				transform.localScale = Vector3.Scale(transform.localScale, new Vector3(scale, scale, scale));
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

		public void SetCoordinateSystem()
		{
			if (df.upAxis == DataField.UpAxis.Z)
			{
				transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));
			}
			if (df.coordinateSystem == DataField.CoordinateSystem.RIGHT_HANDED);
			{
				transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1, 1, -1));
			}
		}
	}
}