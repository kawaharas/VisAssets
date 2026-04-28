using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets
{
#if UNITY_EDITOR
	[CustomEditor(typeof(CtrlOBJ))]
	public class CtrlOBJEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			SerializedProperty iterator = serializedObject.GetIterator();

			bool enterChildren = true;

			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;

				if (iterator.name == "m_Script") continue;

				EditorGUILayout.PropertyField(iterator, true);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class CtrlOBJ : MonoBehaviour
	{
		[Header("Rotation Speeds")]
		public float rotationSpeedMouse = 1200f;
		public float rotationSpeedTouch = 15f;

		[Header("Scale Speeds")]
		public float scaleSpeedMouse = 0.1f;
		public float scaleSpeedTouch = 0.005f;

		[Header("Scale Limits")]
		public float minScale = 0.1f;
		public float maxScale = 10.0f;

		[Header("Interaction Settings")]
		public bool isActive = true;
		[ReadOnly] public bool isDragging = false;

		private float currentScaleRatio = 1.0f;
		private DataField df;
		private bool isLockUI = false;

		void Start()
		{
			df = GetComponent<DataField>();
		}

		void Update()
		{
			if (Input.touchCount == 0 && !Input.GetMouseButton(0))
			{
				isLockUI = false;
				isDragging = false;
			}

			if (!isActive) return;
			if (df != null && !df.dataLoaded) return;

			HandleInput();
		}

		private void HandleInput()
		{
			if (Input.touchCount > 0)
			{
				HandleTouch();
			}
			else
			{
				HandleMouse();
			}
		}

		private bool IsPointerOverUI(Vector2 screenPosition)
		{
			if (EventSystem.current == null) return false;

			PointerEventData eventData = new PointerEventData(EventSystem.current);
			eventData.position = screenPosition;
			List<RaycastResult> results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(eventData, results);

			return results.Count > 0;
		}

		private void HandleMouse()
		{
			if (Input.GetMouseButtonDown(0))
			{
				if (IsPointerOverUI(Input.mousePosition))
//				if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				{
					isLockUI = true;
					return;
				}
				isLockUI = false;
				isDragging = true;
				return;
			}

			if (Input.GetMouseButtonUp(0))
			{
				isDragging = false;
				isLockUI = false;
			}

			if (isDragging && !isLockUI)
			{
				float mouseX = Input.GetAxis("Mouse X") * rotationSpeedMouse * Time.deltaTime;
				float mouseY = Input.GetAxis("Mouse Y") * rotationSpeedMouse * Time.deltaTime;
				RotateObject(mouseX, mouseY);
			}

			float scroll = Input.mouseScrollDelta.y;
			if (scroll != 0 && !isLockUI)
			{
				if (IsPointerOverUI(Input.mousePosition)) return;
//				if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
				ApplyScale(scroll * scaleSpeedMouse);
			}
		}

		private void HandleTouch()
		{
			for (int i = 0; i < Input.touchCount; i++)
			{
				Touch t = Input.GetTouch(i);
				if (t.phase == TouchPhase.Began)
				{
					if (IsPointerOverUI(t.position))
//					if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
					{
						isLockUI = true;
					}
				}
			}

			if (isLockUI) return;

			if (Input.touchCount == 1)
			{
				Touch touch = Input.GetTouch(0);
				if (touch.phase == TouchPhase.Began) isDragging = true;
				if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) isDragging = false;

				if (touch.phase == TouchPhase.Moved && isDragging)
				{
					float touchX = touch.deltaPosition.x * rotationSpeedTouch * Time.deltaTime;
					float touchY = touch.deltaPosition.y * rotationSpeedTouch * Time.deltaTime;
					RotateObject(touchX, touchY);
				}
			}
			else if (Input.touchCount == 2)
			{
				isDragging = false;
				Touch touch0 = Input.GetTouch(0);
				Touch touch1 = Input.GetTouch(1);

				Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
				Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

				float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
				float touchDeltaMag = (touch0.position - touch1.position).magnitude;
				float deltaMagnitudeDiff = touchDeltaMag - prevTouchDeltaMag;

				if (Mathf.Abs(deltaMagnitudeDiff) > 0.01f)
				{
					ApplyScale(deltaMagnitudeDiff * scaleSpeedTouch);
				}
			}
		}

		private void RotateObject(float deltaX, float deltaY)
		{
			if (Camera.main != null)
			{
				transform.Rotate(Camera.main.transform.up, -deltaX, Space.World);
				transform.Rotate(Camera.main.transform.right, deltaY, Space.World);
			}
		}

		private void ApplyScale(float deltaScale)
		{
			float prevRatio = currentScaleRatio;
			currentScaleRatio += deltaScale;
			currentScaleRatio = Mathf.Clamp(currentScaleRatio, minScale, maxScale);

			if (prevRatio > 0)
			{
				float multiplier = currentScaleRatio / prevRatio;
				transform.localScale *= multiplier;
			}
		}
	}
}