using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VisAssets
{
	using ModuleState = Activation.ModuleState;

	/// <summary>
	/// Base class for all Mapper modules in VisAssets.
	/// Handles data connection validation, module lifecycle, and coordinates parameter updates
	/// with the parent DataField module.
	/// </summary>
	[RequireComponent(typeof(Activation))]
	public class MapperModuleTemplate : ModuleTemplate
	{
		private GameObject parent;
		private bool connection;

		[ReadOnly]
		public ModuleType  moduleType = ModuleType.MAPPING;

		[HideInInspector]
		public Activation  activation;

		[SerializeField, HideInInspector]
		public DataField   pdf;

#if UNITY_EDITOR
		/// <summary>
		/// Called when the component is attached or reset in the Inspector.
		/// Sets up default values, ensuring user modifications are respected at runtime.
		/// </summary>
		protected override void Reset()
		{
			base.Reset();

			if (this.GetComponent<MeshFilter>() == null)
			{
				this.gameObject.AddComponent<MeshFilter>();
			}

			var meshRenderer = this.GetComponent<MeshRenderer>();
			if (meshRenderer == null)
			{
				meshRenderer = this.gameObject.AddComponent<MeshRenderer>();
			}

			// Set default values (executed only during attach/reset to avoid overriding user changes)
			// 1. Disable receiving shadows by default to prevent color distortion caused by self-shadowing.
			meshRenderer.receiveShadows = false;
			// 2. Enable ShadowCastingMode so the ShadowCaster pass runs, which is necessary for VolumeRenderer's depth texture calculations.
			meshRenderer.shadowCastingMode = ShadowCastingMode.On;
		}
#endif

		void Awake()
		{
			activation = this.GetComponent<Activation>();
			if (activation == null)
			{
				activation = this.gameObject.AddComponent<Activation>();
			}
			activation.SetModuleType(ModuleType.MAPPING);

			if (this.GetComponent<MeshFilter>() == null)
			{
				this.gameObject.AddComponent<MeshFilter>();
			}
			if (this.GetComponent<MeshRenderer>() == null)
			{
				this.gameObject.AddComponent<MeshRenderer>();
			}

			var transform = GetComponent<Transform>();
//			transform.hideFlags = HideFlags.HideInInspector;
		}

		void Start()
		{
			connection = false;

			if (transform.parent)
			{
				parent = transform.parent.gameObject;
				pdf = parent.GetComponent<DataField>();

				if (pdf != null)
				{
					if ((pdf.dataType == DataField.DataType.RAW) ||
						(pdf.dataType == DataField.DataType.FILTERED))
					{
						connection = true;
					}
				}
			}

			if (connection)
			{
				GetParameters();

				InitModule();

				SetupUI();
			}
			else
			{
				string errString = "";

				errString += "ERROR: Mapper modules should put under a Read Module or a Filter Module.\n";
				errString += "Module name : " + this.name + " is stoped.";
				Debug.Log(errString);

				gameObject.SetActive(false);
			}
		}

		void Update()
		{
			if (!connection) return;
/*
			if (!connection)
			{
				if (!CheckConnection()) return;
			}
*/
			ModuleState parentupdate = activation.GetParentChanged();
			ModuleState paramupdate  = activation.GetParameterChanged();

			int update = (int)parentupdate + (int)paramupdate;

			if (update != 0)
			{
				if (!IsDataLoadedToParent()) return;

				if (paramupdate == ModuleState.PARAMETER_CHANGED)
				{
					SetParameters();
				}

				if (parentupdate == ModuleState.PARAMETER_CHANGED)
				{
					ReSetParameters();

					ResetUICore();
				}

				GetParameters();

				if (BodyFunc() != 1)
				{
					Debug.Log("ERROR: in mapper module func");
				}

				activation.SetParameterChanged(ModuleState.UNCHANGED);
				activation.SetParentChanged(ModuleState.UNCHANGED);
			}

			IdleFunc();
		}

		/// <summary>
		/// Checks if the module is correctly parented to a valid module.
		/// Initializes the module if the connection is successful.
		/// </summary>
		bool CheckConnection()
		{
			connection = false;

			if (transform.parent)
			{
				parent = transform.parent.gameObject;
				pdf = parent.GetComponent<DataField>();

				if (pdf != null)
				{
					if ((pdf.dataType == DataField.DataType.RAW) |
						(pdf.dataType == DataField.DataType.FILTERED))
					{
						connection = true;
					}
				}
			}

			if (connection)
			{
//				if (!gameObject.activeSelf)
				if (!gameObject.activeInHierarchy)
				{
					gameObject.SetActive(true);
				}

				GetParameters();

				InitModule();

				SetupUI();

				return true;
			}
			else
			{
				string errString = "";

				errString += "ERROR: Mapper modules should put under a Read Module or a Filter Module.\n";
				errString += "Module name : " + this.name + " is stoped.";
				Debug.Log(errString);

				gameObject.SetActive(false);

				return false;
			}
		}

		/// <summary>
		/// Method overridden by derived modules for initialization setup.
		/// </summary>
		public virtual void InitModule()
		{
		}

		/// <summary>
		/// The main execution function overridden by derived modules.
		/// </summary>
		public virtual int BodyFunc()
		{
			return 0;
		}

		/// <summary>
		/// Called every frame when the module is not actively updating its data.
		/// Overridden by modules that require continuous animation (e.g., Particle Flow).
		/// </summary>
		public virtual void IdleFunc()
		{
		}

		/// <summary>
		/// Applies changes made to the module's own UI or Inspector parameters.
		/// Called immediately before execution when the module's local settings are modified.
		/// </summary>
		public virtual void SetParameters()
		{
		}

		/// <summary>
		/// Retrieves current parameter values from the UI to synchronize internal states 
		/// before the main execution (BodyFunc) occurs.
		/// </summary>
		public virtual void GetParameters()
		{
		}

		/// <summary>
		/// Re-initializes parameters and internal states when the upstream parent module 
		/// (e.g., DataField) is updated or new data is loaded.
		/// </summary>
		public virtual void ReSetParameters()
		{
		}

		/// <summary>
		/// Verifies if the parent DataField component has finished loading data.
		/// </summary>
		public bool IsDataLoadedToParent()
		{
			if (pdf == null)
			{
				return false;
			}

			if (!pdf.dataLoaded)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Signals the activation component that a parameter has been modified, triggering an update.
		/// </summary>
		public void ParameterChanged()
		{
			activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}
	}
}