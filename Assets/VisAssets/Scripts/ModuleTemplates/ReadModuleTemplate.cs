using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets
{
	using ModuleState = Activation.ModuleState;

#if UNITY_EDITOR
	[CustomEditor(typeof(ReadModuleTemplate), true)]
	public class ReadModuleTemplateEditor : ModuleTemplateEditor
	{
		protected SerializedProperty useStreamingAssets;
		protected SerializedProperty loadAtStartup;
		protected SerializedProperty useUndefMenu;
		protected SerializedProperty useUndef;
		protected SerializedProperty undef;
		protected SerializedProperty centering;
		protected SerializedProperty autoResize;

		protected override void OnEnable()
		{
			base.OnEnable();

			useStreamingAssets = serializedObject.FindProperty("useStreamingAssets");
			loadAtStartup      = serializedObject.FindProperty("loadAtStartup");
			useUndefMenu       = serializedObject.FindProperty("useUndefMenu");
			useUndef           = serializedObject.FindProperty("useUndef");
			undef              = serializedObject.FindProperty("undef");
			centering          = serializedObject.FindProperty("centering");
			autoResize         = serializedObject.FindProperty("autoResize");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			DrawCustomSettingsTop();
			DrawCommonSettings();
			DrawCustomSettingsBottom();

			DrawProp(uiPrefab, "UI Prefab", 5f);

			if (EditorGUI.EndChangeCheck())
			{
				EditorUtility.SetDirty(target);
			}
			serializedObject.ApplyModifiedProperties();
		}

		protected virtual void DrawCustomSettingsTop()
		{
		}

		protected virtual void DrawCustomSettingsBottom()
		{
		}

		protected virtual void DrawCommonSettings()
		{
			GUILayout.Space(5f);

			EditorGUILayout.LabelField("[Common Settings]", EditorStyles.boldLabel);
			
			GUILayout.Space(5f);

			DrawToggle(useStreamingAssets, "Use StreamingAssets", 5f);
			DrawToggle(loadAtStartup, "Load At Startup", 5f);

			if (useUndefMenu != null && useUndefMenu.boolValue && useUndef != null)
			{
				DrawToggle(useUndef, "Use Undef (Exclude specific values)", 5f);
				bool isUndefDisabled = !useUndef.boolValue;
				DrawProp(undef, "Undef Value", 5f, 1, isUndefDisabled);
			}

			if (centering != null)
			{
				DrawToggle(centering, "Centering", 5f);
				bool isAutoResizeDisabled = !centering.boolValue;
				if (isAutoResizeDisabled && autoResize != null)
				{
					autoResize.boolValue = false;
				}
				DrawToggle(autoResize, "Auto Resize", 5f, 1, isAutoResizeDisabled);
			}

			GUILayout.Space(5f);
		}
	}
#endif

	[RequireComponent(typeof(Activation))]
	[RequireComponent(typeof(DataField))]

	public class ReadModuleTemplate : ModuleTemplate
	{
		public enum PRECISION
		{
			SINGLE,
			DOUBLE
		};

		[HideInInspector]
		public Activation activation;

		[HideInInspector]
		public DataField  df;

		[HideInInspector]
		public GameObject animator;

		[SerializeField]
		protected bool useStreamingAssets = true;

		[SerializeField]
		private   bool loadAtStartup = true;
		
		[SerializeField]
		protected bool centering = true;

		[SerializeField]
		protected bool autoResize = true;
		
		[SerializeField]
		protected bool useUndef = false;

		[SerializeField]
		protected float undef = 0f;
		
		[HideInInspector] [SerializeField]
		protected bool useUndefMenu = true;

		[HideInInspector]
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
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		void Update()
		{
			CheckCurrentStep();

			if (activation.GetParameterChanged() == ModuleState.PARAMETER_CHANGED)
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

				activation.SetParameterChanged(ModuleState.UNCHANGED);
			}
		}

		public virtual void SetData(int step)
		{
		}

		public void InitAnimator()
		{
			var animator = GameObject.Find("Animator");
			if (animator != null && animator.tag.Equals("VisModule"))
			{
				animator.GetComponent<Animator>().CheckMaximumSteps();
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
					float startVal = element.coords[i][0];
					float endVal = element.coords[i][element.dims[i] - 1];
					min[i] = Mathf.Min(min[i], Mathf.Min(startVal, endVal));
					max[i] = Mathf.Max(max[i], Mathf.Max(startVal, endVal));
				}
			}

			for (int i = 0; i < 3; i++)
			{
				maxDist = Mathf.Max(maxDist, max[i] - min[i]);
				offset[i] = min[i] + (max[i] - min[i]) / 2f;
				df.offset[i] = offset[i]; 
			}

			if (centering)
			{
				foreach (Transform child in transform)
				{
					child.gameObject.transform.localPosition =
						new Vector3(-offset[0], -offset[1], -offset[2]);
				}
			}
			else
			{
				foreach (Transform child in transform)
				{
					child.gameObject.transform.localPosition = Vector3.zero;
				}
			}

			if (normalize)
			{
				float scale = 1f / maxDist * 10f;
				transform.localScale = new Vector3(scale, scale, scale);
			}
			else
			{
				transform.localScale = Vector3.one;
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
					c.SetParentChanged(ModuleState.PARAMETER_CHANGED);
				}
			}
		}

		public void ParameterChanged()
		{
			activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}

		public void SetCoordinateSystem()
		{
			if (df.upAxis == DataField.UpAxis.Z)
			{
				transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));
			}

			if (df.coordinateSystem == DataField.CoordinateSystem.RIGHT_HANDED)
			{
				transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1, 1, -1));
			}
		}
	}
}