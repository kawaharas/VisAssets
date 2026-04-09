using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VisAssets
{
	using ModuleState = Activation.ModuleState;

	[RequireComponent(typeof(Activation))]

	public class MapperModuleTemplate : ModuleTemplate
	{
		private GameObject parent;
		private bool connection;

		[ReadOnly]
		public ModuleType moduleType = ModuleType.MAPPING;
		[HideInInspector]
		public Activation  activation;
		[SerializeField, HideInInspector]
		public DataField   pdf;

#if UNITY_EDITOR
		/// <summary>
		/// Called when the component is attached or reset in the Inspector.
		/// Sets up default values ensuring user modifications are respected at runtime.
		/// </summary>
		protected override void Reset()
		{
			// 基底クラスの処理（VisModuleタグの付与など）を実行
			base.Reset();

			// MeshFilter / MeshRenderer が無ければ追加
			if (this.GetComponent<MeshFilter>() == null)
			{
				this.gameObject.AddComponent<MeshFilter>();
			}

			var meshRenderer = this.GetComponent<MeshRenderer>();
			if (meshRenderer == null)
			{
				meshRenderer = this.gameObject.AddComponent<MeshRenderer>();
			}

			// ?? 初期値の設定（アタッチ時・リセット時のみ実行されるため、ユーザーの変更を阻害しない）
			// 1. 自身の影による色の歪みを防ぐため、デフォルトは影を受け取らない
			meshRenderer.receiveShadows = false;
			// 2. VolumeRendererの深度テクスチャ計算を機能させるため、ShadowCasterパスは実行させる
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
			activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
		}
	}
}