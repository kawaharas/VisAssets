using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VIS
{
	[RequireComponent(typeof(Activation))]

	public class MapperModuleTemplate : ModuleTemplate
	{
		private GameObject parent;
		private bool connection;

		[HideInInspector]
		public Activation  activation;
		[SerializeField, HideInInspector]
		public DataField   pdf;

		void Awake()
		{
			activation = this.GetComponent<Activation>();
			if (activation == null)
			{
				activation = this.gameObject.AddComponent<Activation>();
			}
			if (this.GetComponent<MeshFilter>() == null)
			{
				this.gameObject.AddComponent<MeshFilter>();
			}
			if (this.GetComponent<MeshRenderer>() == null)
			{
				this.gameObject.AddComponent<MeshRenderer>();
			}
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
			int parentupdate = activation.GetParentChanged();
			int paramupdate  = activation.GetParameterChanged();
			int update = parentupdate + paramupdate;

			if (update != 0)
			{
				if (!IsDataLoadedToParent()) return;

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
				if (BodyFunc() != 1)
				{
					Debug.Log("ERROR: in mapper module func");
				}
				activation.SetParameterChanged(0);
				activation.SetParentChanged(0);
			}
			IdleFunc();
		}

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

		public virtual void InitModule()
		{
		}

		public virtual int BodyFunc()
		{
			return 0;
		}

		public virtual void IdleFunc()
		{
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

		public void ParameterChanged()
		{
			activation.SetParameterChanged(1);
		}
	}
}