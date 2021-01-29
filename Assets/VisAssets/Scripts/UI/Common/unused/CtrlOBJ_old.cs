using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VIS
{
	public class CtrlOBJ_old : MonoBehaviour
	{
		Vector3 prevPos = Vector3.zero;
		Vector3 posDelta = Vector3.zero;
		public bool active = true;
		float zoom;
		float tan0;

		void Start()
		{
			tan0 = Mathf.Tan(Camera.main.fieldOfView * 0.5f / 180 * Mathf.PI);
			zoom = 0;
		}

		void Update()
		{
			if (!active) return;
			if (!GetComponent<DataField>().dataLoaded) return;

			Camera.main.fieldOfView = Mathf.Atan(tan0 / Mathf.Exp(-zoom)) * 180 / Mathf.PI * 2;
			zoom -= Input.mouseScrollDelta.y * 0.1f;

			if (Input.GetMouseButton(0))
			{
				posDelta = Input.mousePosition - prevPos;
				if (Vector3.Dot(transform.up, Vector3.up) >= 0)
				{
					transform.Rotate(transform.up, -Vector3.Dot(posDelta, Camera.main.transform.right), Space.World);
				}
				else
				{
					transform.Rotate(transform.up, Vector3.Dot(posDelta, Camera.main.transform.right), Space.World);
				}
				transform.Rotate(Camera.main.transform.right, Vector3.Dot(posDelta, Camera.main.transform.up), Space.World);
			}
			prevPos = Input.mousePosition;
		}
	}
}