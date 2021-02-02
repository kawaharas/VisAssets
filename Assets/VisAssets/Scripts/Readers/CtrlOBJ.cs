using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VIS
{
	public class CtrlOBJ : MonoBehaviour
	{
		public GameObject obj;

		// for rotation
		public Vector2 startPosition; // screen position when touched
		Quaternion startRotation;     // rotation when touched
		float w, h, d;                // screen size
		public float tx, ty;

		// for pinch-in and pinch-out
		public float minScale = 1.0f;
		public float maxScale = 5.0f;
		float dist = 0.0f;
		float prev_dist = 0.0f;
		Vector3 initScale;
		public float scale = 1.0f;
		public bool IsDrag = false;
		public float sensitivity = 5f;
		public bool InitScaleSet = false;
		public bool active = true;

		void Start()
		{
			w = Screen.width;
			h = Screen.height;
			d = Mathf.Sqrt(Mathf.Pow(w, 2) + Mathf.Pow(h, 2));
			obj = this.gameObject;
			initScale = obj.transform.localScale;
		}

		void Update()
		{
			if (Application.platform != RuntimePlatform.Android)
			{
				if (!active) return;
			}

			var df = obj.GetComponent<DataField>();

			if (df != null)
			{
				if (df.dataLoaded)
				{
					if (!InitScaleSet)
					{
						initScale = obj.transform.localScale;
						InitScaleSet = true;
					}

					// for mouse operation
					if (Input.GetMouseButtonDown(0))
					{
						startPosition = Input.mousePosition;
						startRotation = obj.transform.rotation;
						IsDrag = true;
					}
					if (Input.GetMouseButtonUp(0))
					{
						IsDrag = false;
					}
					if (IsDrag)
					{
						tx = (Input.mousePosition.x - startPosition.x) / w * sensitivity;
						ty = (Input.mousePosition.y - startPosition.y) / h * sensitivity;
						obj.transform.rotation = startRotation;
						obj.transform.Rotate(new Vector3(90 * ty, -90 * tx, 0), Space.World);
					}
					scale -= Input.mouseScrollDelta.y * 0.1f;
					scale = Mathf.Clamp(scale, minScale, maxScale);
					obj.transform.localScale = initScale * scale;

					// for touch operation
					if (Input.touchCount == 1)
					{
						Touch t1 = Input.GetTouch(0);
						if (t1.phase == TouchPhase.Began)
						{
							startPosition = t1.position;
							startRotation = obj.transform.rotation;
						}
						else if ((t1.phase == TouchPhase.Moved) || (t1.phase == TouchPhase.Stationary))
						{
							tx = (t1.position.x - startPosition.x) / w * sensitivity;
							ty = (t1.position.y - startPosition.y) / h * sensitivity;
							obj.transform.rotation = startRotation;
							obj.transform.Rotate(new Vector3(90 * ty, -90 * tx, 0), Space.World);
						}
					}
					else if (Input.touchCount >= 2)
					{
						Touch t1 = Input.GetTouch(0);
						Touch t2 = Input.GetTouch(1);

						if (t2.phase == TouchPhase.Began)
						{
							prev_dist = Vector2.Distance(t1.position, t2.position);
						}
						else if (((t1.phase == TouchPhase.Moved) || (t1.phase == TouchPhase.Stationary)) &&
								 ((t2.phase == TouchPhase.Moved) || (t2.phase == TouchPhase.Stationary)))
						{
							dist = Vector2.Distance(t1.position, t2.position);
							scale += (dist - prev_dist) / d * 3f;
							prev_dist = dist;
							scale = Mathf.Clamp(scale, minScale, maxScale);
							obj.transform.localScale = initScale * scale;
						}
					}
				}
			}
		}
	}
}