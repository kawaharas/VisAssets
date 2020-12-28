using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VIS
{
	[RequireComponent(typeof(Activation))]
	[RequireComponent(typeof(DataField))]

	public class FilterModuleTemplate : ModuleTemplate
	{
		private GameObject parent;
		private bool connection;

		[HideInInspector]
		public Activation activation;
		[HideInInspector]
		public DataField  df;
		[HideInInspector]
		public DataField  pdf;

		void Awake()
		{
			activation = this.GetComponent<Activation>();
			if (activation == null)
			{
				activation = this.gameObject.AddComponent<Activation>();
			}
			activation.SetModuleType(ModuleType.FILTERING);

			df = this.GetComponent<DataField>();
			if (df == null)
			{
				df = this.gameObject.AddComponent<DataField>();
			}
			df.dataType = DataField.DataType.FILTERED;
		}

		void Start()
		{
			connection = false;

			if (transform.parent)
			{
				parent = transform.parent.gameObject;
				pdf = parent.GetComponent<DataField>();
				if (pdf!= null)
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
				errString += "ERROR: Filter Modules should put under a Read Module or a Filter Module.\n";
				errString += "Module name : " + this.name + " is stoped.";
				Debug.Log(errString);
				gameObject.SetActive(false);
			}
		}

		void Update()
		{
			if (!connection) return;

			int parentupdate = activation.GetParentChanged();
			int paramupdate  = activation.GetParameterChanged();
			int update = parentupdate + paramupdate;

			if (update != 0)
			{
				if (!IsDataLoadedToParent()) return;

				// turn off flag until data loading is complete
				df.dataLoaded = false;

				if (paramupdate == 1)
				{
					SetParameters();
				}
				if (parentupdate == 1)
				{
					ReSetParameters();
					ResetUICore();
				}
				GetParameters();
				if (BodyFunc() == 1)
				{
					// turn on flag when data loading is complete
					df.dataLoaded = true;
					SetParentChangedIntoAllChildren();
				}
				else
				{
					// error
//					Debug.Log("ERROR: in filter module func");
				}
				activation.SetParameterChanged(0);
				activation.SetParentChanged(0);
			}
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

			activation.SetParameterChanged(1);
		}

		public virtual void InitModule()
		{
		}

		public virtual int BodyFunc()
		{
			return 0;
		}

		public virtual void SetParameters()
		{
		}

		public virtual void ReSetParameters()
		{
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

		public bool IsDataLoadedToParent()
		{
			if (pdf == null)
			{
				return false;
			}

			if (!pdf.dataLoaded)
			{
				df.dataLoaded = false;
				return false;
			}

			return true;
		}

		public void ParameterChanged()
		{
			activation.SetParameterChanged(1);
		}
	}
}